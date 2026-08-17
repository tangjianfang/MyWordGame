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
    /// m11 ②C：玩家射的箭（<see cref="ProjectileEntity.OwnerEntityId"/>=0）在宿主侧
    /// 反向判 <b>mob</b> 命中（骷髅箭只判玩家命中，见 <see cref="TryHitMob"/>）——
    /// 伤害统一走 Core <see cref="MobAI.TakeHit"/>，击杀的掉落/经验由 MobManager
    /// 的 Dying 分支按既有死亡序列结算，本类不另写一条。
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

        /// <summary>玩家箭命中 mob 的判定半径（格）：箭心与 mob 锚点
        /// （<see cref="MyWorld.Core.Entities.Mob.Position"/>，脚底中心）距离小于它即命中。
        /// 锚点语义与骷髅箭的 <see cref="ProjectileEntity.PlayerHitRadius"/>（玩家 transform
        /// 根位置）一致；0.9 贴猪碰撞盒宽度（0.9×身高×0.9），从脚底向上罩住躯干。</summary>
        public const float MobHitRadius = 0.9f;

        /// <summary>箭体棕色（木杆+箭羽均值，#8B5C2E）。</summary>
        public static readonly Color ArrowBrown = new Color(0.545f, 0.361f, 0.180f);

        private World _world;
        private Transform _player;
        private PlayerContext _context;
        private Transform _parent;

        /// <summary>宿主 MobManager（玩家箭 mob 命中判定的数据源）。惰性解析、命中即缓存——
        /// WorldBootstrap 在同一宿主上挂它（步骤 12 先于本组件的步骤 32），运行时必有，
        /// 首只玩家箭起飞后第一帧就解析到并缓存。没有宿主的测试场景解析不到时
        /// 不缓存 null（下帧重试）：玩家箭只飞不中，行为安全；FindObjectOfType 的
        /// 重试开销只出现在「有玩家箭却没 MobManager」的场景，生产中不存在。</summary>
        private MobManager _mobManager;

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
        /// 命中玩家 / 超时（Dead）→ 直接移除（伤害已在 Tick 内经事件结算）；
        /// 玩家箭命中 mob（m11 ②C）→ <see cref="MobAI.TakeHit"/> 扣血后箭消亡。
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

                // m11 ②C（任务 1）：玩家箭（owner=0）的 mob 命中——只在仍 Flying 时判
                // （同帧先 Stuck 进墙的箭归方块赢，箭没到 mob 跟前）。命中调
                // MobAI.TakeHit（mob 受伤唯一入口），伤害读箭的 Damage（玩家弓按蓄力
                // 注入 1-4）；致死的掉落/经验走 TakeHit 死亡序列 + MobManager 的
                // Dying 分支（与近战同一条链），本类不另写。命中后箭消亡（Dead，
                // 不转可拾取——命中实体的箭按折损处理，可捡的箭由 Stuck 路径出）。
                if (arrow.State == ProjectileState.Flying
                    && TryHitMob(arrow, playerPos))
                {
                    arrow.State = ProjectileState.Dead;
                    RemoveAt(i);
                    continue;
                }

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

        /// <summary>
        /// 玩家箭（<see cref="ProjectileEntity.OwnerEntityId"/>==0）mob 命中判定：
        /// 直接遍历宿主 <see cref="MobManager.ActiveMobs"/>，箭心与 mob 锚点距离
        /// &lt; <see cref="MobHitRadius"/> 即命中（首个命中者，插入序确定性）。
        /// 直接在 <see cref="MobManager"/> 的列表上只读遍历——不复制、零分配
        /// （热路径纪律）；命中把 mob 交给 <see cref="MobAI.TakeHit"/>，attackerPos
        /// 传玩家位置（逃跑/击退的方向基准取射手，与近战同语义）。
        /// 骷髅箭（owner≠0）不进本判定——它打的是玩家（ProjectileEntity.Tick 内），
        /// 骷髅误伤其它 mob 不在本批范围。
        /// </summary>
        /// <returns>true = 命中了一只 mob（调用方应让箭消亡）。</returns>
        private bool TryHitMob(ProjectileEntity arrow, Float3 attackerPos)
        {
            if (arrow.OwnerEntityId != 0) return false;

            if (_mobManager == null)
            {
                _mobManager = FindObjectOfType<MobManager>();
                if (_mobManager == null) return false;
            }
            var mobs = _mobManager.ActiveMobs;

            float radiusSq = MobHitRadius * MobHitRadius;
            for (int i = 0; i < mobs.Count; i++)
            {
                var m = mobs[i];
                if (!m.IsAlive) continue; // 尸体（Dying/Dead）不吃箭，让箭穿过去

                float dx = arrow.Position.X - m.Position.X;
                float dy = arrow.Position.Y - m.Position.Y;
                float dz = arrow.Position.Z - m.Position.Z;
                if (dx * dx + dy * dy + dz * dz < radiusSq)
                {
                    // m11 W2-4 B5：弓杀的死亡事件出口——近战致死走 CombatController.DoAttack
                    // 的 RaiseDied，弓箭致死原先没人发（KillKind 任务/死亡观察全漏）。
                    // DamageSource.Projectile 会被 QuestEventBus.HandleEntityDied 翻译成
                    // Weapon="bow"（chapter2 ch2_07「用弓击败骷髅」的条件限定）
                    bool killed = MobAI.TakeHit(m, attackerPos, arrow.Damage);
                    if (killed)
                    {
                        CombatEvents.RaiseDied(new DamageEvent(
                            DamageSource.Projectile, arrow.Damage, attacker: 0,
                            victim: m.EntityId, hit: arrow.Position));
                    }
                    return true;
                }
            }
            return false;
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
