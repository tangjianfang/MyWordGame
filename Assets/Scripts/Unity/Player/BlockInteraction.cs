using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Audio;
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
                    // 挖：把命中格设为空气，标脏，重建
                    _world.SetBlock(hit.X, hit.Y, hit.Z, BlockIds.Air);
                    _views.MarkBlockChanged(hit.X, hit.Y, hit.Z);
                    _audio?.PlayBreak();
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
