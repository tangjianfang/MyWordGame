using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Audio;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 鼠标射线选中 → 线框高亮 → 左键挖 / 右键放。
    /// <para>
    /// 数据源全部走 Core（<see cref="VoxelRaycaster"/> + <see cref="BlockPlacement"/> +
    /// <see cref="World.SetBlock"/> + <see cref="DirtySections"/>），Unity 侧只负责鼠标轮询、
    /// 调用顺序、和把脏段交给 <see cref="ChunkViewRegistry"/> 重建。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class BlockInteraction : MonoBehaviour
    {
        [SerializeField] private ushort placeBlockId = BlockIds.Stone;
        [SerializeField] private Material selectionMaterial;

        private PlayerController _player;
        private World _world;
        private BlockRegistry _registry;
        private ChunkViewRegistry _views;
        private SelectionBox _selection;
        private PlayerAudioSystem _audio;
        private BlockDrops _blockDrops;

        public void Bind(World world, BlockRegistry registry, ChunkViewRegistry views, Transform parent)
        {
            _player = GetComponent<PlayerController>();
            _world = world;
            _registry = registry;
            _views = views;
            // 父链上找 PlayerAudioSystem（WorldBootstrap 与 Player 同 GameObject），
            // 找不到也无所谓——_audio 为 null，place/break 时跳过音效即可（nice-to-have）。
            _audio = GetComponentInParent<PlayerAudioSystem>();

            if (selectionMaterial == null)
            {
                // 优先 URP/Unlit（场景已接入 URP），fallback Hidden/Internal-Colored（默认包含）。
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                selectionMaterial = new Material(shader) { color = new Color(1f, 1f, 1f, 1f) };
            }

            _selection = SelectionBox.Create(parent, selectionMaterial);
            _selection.Hide();
        }

        /// <summary>
        /// X2 fix-up：注入方块→物品掉落表。无表时挖方块不产生掉落（保持旧行为）。
        /// 由 <c>WorldBootstrap</c> 在 BlockDefinitionFilesTests + ItemDatabase 配齐后调用。
        /// 传 null 也允许——和没注入表的行为一致——但会打 warning 让上游忘记注入更容易被发现。
        /// </summary>
        public void SetBlockDrops(BlockDrops drops)
        {
            if (drops == null)
            {
                Debug.LogWarning("[BlockInteraction] SetBlockDrops 传入了 null，挖方块时不会产生掉落。");
            }
            _blockDrops = drops;
        }

        private void Update()
        {
            if (_world == null || _player.Eye == null)
            {
                return;
            }

            var source = new WorldSolidSource(_world, _registry);
            Float3 origin = ToFloat3(_player.Eye.position);
            Float3 direction = ToFloat3(_player.Eye.forward);
            float maxDist = _player.Settings.ReachDistance;

            VoxelRayHit hit = VoxelRaycaster.Cast(source, origin, direction, maxDist);

            if (hit.Hit)
            {
                _selection.ShowAt(hit.X, hit.Y, hit.Z);

                if (Input.GetMouseButtonDown(0))
                {
                    // 挖：把命中格设为空气，标脏，重建，并按 BlockDrops spawn ItemDropEntity
                    BreakAt(hit.X, hit.Y, hit.Z);
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    // 放：尝试解算放置位置，合法就 SetBlock + 标脏 + 重建
                    Aabb playerBox = Aabb.FromBottomCenter(_player.State.Position,
                        _player.Settings.Width, _player.Settings.Height);
                    if (BlockPlacement.TryResolve(hit, playerBox, out int x, out int y, out int z))
                    {
                        _world.SetBlock(x, y, z, placeBlockId);
                        _views.MarkBlockChanged(x, y, z);
                        _audio?.PlayPlace();
                    }
                }
            }
            else
            {
                _selection.Hide();
            }
        }

        private static Float3 ToFloat3(Vector3 v) => new Float3(v.x, v.y, v.z);

        /// <summary>
        /// X2 fix-up：在指定坐标挖方块。流程：<see cref="World.SetBlock"/> → 标脏 → 播放 break 音效 →
        /// 按 <see cref="BlockDrops"/> 查询该方块的掉落物条目，每条实例化为
        /// <see cref="MyWorld.Core.Items.ItemDropEntity"/> 并加入 <see cref="PlayerContext.ItemDrops"/>。
        /// <para>
        /// 暴露为 public 是为了让 EditMode 测试不依赖 <c>Input.GetMouseButtonDown</c>；
        /// <c>Update</c> 与外部测试都走同一条路径。
        /// </para>
        /// <para>
        /// 顺序与既有 <c>Update</c> 行为对齐：先清方块 → 标脏（让玩家视觉立刻看到破坏）→ 播音效 →
        /// spawn 掉落。无 PlayerContext / 无 BlockDrops 表 / 挖空气 / drops 表里没条目均 no-op。
        /// </para>
        /// </summary>
        public void BreakAt(int x, int y, int z)
        {
            if (_world == null) return;
            ushort before = _world.GetBlock(x, y, z);
            if (before == BlockIds.Air) return; // 挖空气是 no-op（与 review-final B7 不冲突）

            _world.SetBlock(x, y, z, BlockIds.Air);
            _views?.MarkBlockChanged(x, y, z);
            _audio?.PlayBreak();

            // X2 fix-up：spawn ItemDropEntity。BlockDrops 可能未注入（旧场景 / EditMode
            // 单元测），缺了就 silently no-op，不破坏既有"挖 = 立即空一块"的视觉反馈。
            if (_blockDrops == null) return;
            ItemStack[] drops = _blockDrops.DropsFor(before);
            if (drops == null || drops.Length == 0) return;

            var ctx = PlayerContext.Instance;
            if (ctx == null)
            {
                // 玩家视觉看到方块消失却没掉任何东西，没 log 也找不到原因——
                // 加 warning 让「场景里没挂 PlayerContext」这种装配失误更容易定位。
                Debug.LogWarning("[BlockInteraction] 挖方块掉落需要 PlayerContext，但当前为 null");
                return;
            }

            // 中心 = (x+0.5, y+0.5, z+0.5)，让 1.5m 拾取半径对准方块中心。
            Float3 center = new Float3(x + 0.5f, y + 0.5f, z + 0.5f);
            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i].IsEmpty) continue;
                var drop = new ItemDropEntity(drops[i], center);
                drop.SpawnTime = Time.time; // F1 follow-up：spawn 时刻记录，TryPickupBy 据此判定 0.5s grace
                ctx.ItemDrops.Add(drop);
            }
        }

        /// <summary>
        /// 按 Biome 调整方块的挖掘耗时（秒）。
        /// 山地石头硬 ×2，沙漠沙软 ×0.5，其它默认 1f。
        /// <para>
        /// 静态纯函数：EditMode 测试可直接调用验证契约，
        /// 与 MonoBehaviour 实例化 / <see cref="World"/> / <see cref="PlayerController"/>
        /// 等上下文完全解耦。当前 <c>Update</c> 里仍是瞬时挖矿——以后接真实 MiningTimed
        /// 逻辑时这个返回值就是「按住 LMB 的目标持续时间」。
        /// </para>
        /// </summary>
        public static float BreakTime(int blockId, Biome biome)
        {
            float base_ = 1f;
            if (blockId == BlockIds.Stone)
            {
                base_ = (biome == Biome.Mountains) ? 2f : 1f;
            }
            else if (blockId == BlockIds.Sand)
            {
                base_ = (biome == Biome.Desert) ? 0.5f : 1f;
            }

            return base_;
        }
    }
}
