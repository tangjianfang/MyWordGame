using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 箭实体宿主（m11 W1-1 集成点②）：订阅 <see cref="MobAI.OnProjectileFired"/>，
    /// 把骷髅射出的 <see cref="ProjectileEntity"/> 接进 Unity——tick 推进弹道、
    /// 0.25 格箭棕小方块视觉（<see cref="ItemDropView"/> 同款管线与尺寸）、
    /// 命中方块转可拾取掉落（进 <see cref="PlayerContext.ItemDrops"/>，
    /// <see cref="MyWorld.Unity.Items.ItemDropViewRegistry"/> 自动接手渲染与吸附）。
    /// <para>
    /// 箭伤在 <see cref="ProjectileEntity.Tick"/> 内经 <see cref="CombatEvents.RaiseTaken"/>
    /// 结算，由 <see cref="MobManager"/> 转发 <c>PlayerController.TakeDamage</c>——
    /// 与僵尸近战/苦力怕爆炸同一条唯一伤害入口，本类不重复扣血。
    /// </para>
    /// <para>
    /// 视觉不带悬浮/自转（那是地面掉落物的语义）：只把实体位置映射到 transform，
    /// 并让小方块朝向速度方向——飞行的箭读得出弹道方向。碰撞体必须删
    /// （<see cref="ItemDropView.Create"/> 同因：会挡玩家移动与挖掘射线）。
    /// </para>
    /// </summary>
    public sealed class ProjectileManager : MonoBehaviour
    {
        /// <summary>箭视觉边长（格），与 <see cref="ItemDropView.VisualSize"/> 同值。</summary>
        public const float VisualSize = 0.25f;

        /// <summary>箭体棕色（木杆+箭羽均值，#8B5C2E）。</summary>
        public static readonly Color ArrowBrown = new Color(0.545f, 0.361f, 0.180f);

        private World _world;
        private Transform _player;
        private PlayerContext _context;
        private Transform _parent;

        private readonly List<ProjectileEntity> _arrows = new List<ProjectileEntity>();
        private readonly List<Transform> _views = new List<Transform>();

        /// <summary>当前在飞的箭数（测试 / 调试用）。</summary>
        public int ActiveArrowCount => _arrows.Count;

        /// <summary>注入依赖（WorldBootstrap 挂载后调用）。parent = 世界根节点（视觉挂它下面）。</summary>
        public void Bind(World world, Transform player, PlayerContext context, Transform parent)
        {
            _world = world;
            _player = player;
            _context = context;
            _parent = parent;
        }

        private void OnEnable()
        {
            MobAI.OnProjectileFired += HandleProjectileFired;
        }

        private void OnDisable()
        {
            MobAI.OnProjectileFired -= HandleProjectileFired;
            ClearAll();
        }

        /// <summary>
        /// 骷髅开火回调：箭实体入列表 + 建视觉。不在这里 tick——统一由 Update 步进，
        /// EditMode 测试可经 <see cref="TickManually"/> 手动驱动同一套逻辑。
        /// </summary>
        private void HandleProjectileFired(ProjectileEntity arrow)
        {
            if (arrow == null) return;
            _arrows.Add(arrow);
            _views.Add(CreateView(arrow));
        }

        private void Update()
        {
            if (_player == null) return;
            TickManually(Time.deltaTime);
        }

        /// <summary>
        /// 推进一帧弹道（公开供 EditMode 测试注入固定 dt）。终局处理：
        /// 命中方块（Stuck）→ <see cref="ProjectileEntity.ToPickup"/> 转掉落物；
        /// 命中玩家 / 超时（Dead）→ 直接移除（伤害已在 Tick 内经事件结算）。
        /// </summary>
        public void TickManually(float dt)
        {
            if (_player == null) return;
            var playerPos = new Float3(_player.position.x, _player.position.y, _player.position.z);

            for (int i = _arrows.Count - 1; i >= 0; i--)
            {
                var arrow = _arrows[i];
                bool ended = arrow.Tick(_world, playerPos, dt);

                // 视觉同步（Stuck 后停在命中前一步，继续显示直到转掉落）
                SyncView(i, arrow);

                if (arrow.State == ProjectileState.Dead
                    || (ended && arrow.State == ProjectileState.Stuck))
                {
                    if (arrow.State == ProjectileState.Stuck)
                    {
                        SpawnPickupDrop(arrow);
                    }
                    RemoveAt(i);
                }
            }
        }

        /// <summary>Stuck 的箭转可拾取掉落：进 PlayerContext.ItemDrops 即由既有
        /// 掉落物管线（视图 + 吸附 + 入包）接管，本类不再另做拾取判定。</summary>
        private void SpawnPickupDrop(ProjectileEntity arrow)
        {
            var context = _context ?? PlayerContext.Instance;
            if (context == null) return;

            var pickup = arrow.ToPickup();
            if (pickup == null) return;

            var drop = new ItemDropEntity(pickup.Value, arrow.Position);
            drop.SpawnTime = Time.time; // 0.5s 拾取宽限期从落地起算
            context.ItemDrops.Add(drop);
        }

        private Transform CreateView(ProjectileEntity arrow)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "箭";
            // 碰撞体必须删：拾取/命中判定全在实体数据层，留着会挡玩家与射线
            var collider = cube.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            if (_parent != null) cube.transform.SetParent(_parent, false);
            cube.transform.localScale = new Vector3(VisualSize, VisualSize, VisualSize);
            cube.GetComponent<Renderer>().sharedMaterial = UrpMaterialFactory.CreateLit(ArrowBrown);
            cube.transform.position = new Vector3(arrow.Position.X, arrow.Position.Y, arrow.Position.Z);
            return cube.transform;
        }

        /// <summary>视觉同步：位置贴实体；朝向速度方向（速度归零的 Stuck 箭保持最后朝向）。</summary>
        private void SyncView(int index, ProjectileEntity arrow)
        {
            if (index >= _views.Count || _views[index] == null) return;
            var t = _views[index];
            t.position = new Vector3(arrow.Position.X, arrow.Position.Y, arrow.Position.Z);
            var v = arrow.Velocity;
            if (v.X * v.X + v.Z * v.Z > 1e-6f)
            {
                t.rotation = Quaternion.LookRotation(new Vector3(v.X, v.Y, v.Z), Vector3.up);
            }
        }

        private void RemoveAt(int index)
        {
            _arrows.RemoveAt(index);
            var view = _views[index];
            _views.RemoveAt(index);
            if (view != null)
            {
                if (Application.isPlaying) Destroy(view.gameObject);
                else DestroyImmediate(view.gameObject);
            }
        }

        private void ClearAll()
        {
            for (int i = _arrows.Count - 1; i >= 0; i--)
            {
                RemoveAt(i);
            }
        }
    }
}
