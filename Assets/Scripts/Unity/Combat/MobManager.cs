using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 动物总管理：spawn + tick + 渲染 + despawn（m7 A2：距玩家 &gt;<see cref="DespawnDistance"/> 格移除）。
    /// 玩家 16 米半径内的 chunk 才会生成。
    /// 友好动物白天全天生成，僵尸仅夜晚生成。
    /// <para>
    /// Phase D：刷怪决策由 <see cref="MobSpawnRules"/> 数据驱动（biome + 光照 + seed），
    /// 而不是写死的概率。运行时刷怪点 biome 查 <see cref="WorldGenerator.BiomeAt"/>，
    /// 光照由 <see cref="TimeOfDay.IsNight"/> 派生（白天=15，夜晚=0）。
    /// </para>
    /// </summary>
    public sealed class MobManager : MonoBehaviour
    {
        public int SpawnRadiusChunks = 1;
        public float SpawnChancePerSecond = 0.4f;
        public int MaxMobs = 24;

        /// <summary>
        /// m7 A2：距玩家超过该距离（格）的 mob 被 despawn。
        /// 此前 Update 只加不减，僵尸永不消失——玩家复活/走远后威胁仍挂着，
        /// 是死亡循环威胁侧根因之一。
        /// </summary>
        public const float DespawnDistance = 40f;

        // 白天候选 mob（按优先级排序：先猪，后牛/鸡、村民；m11 P0 追加 9 新被动 kind——
        // 列表顺序即优先级，新 kind 排既有四类之后）。安全性依据：spawn_rules.json 尚未加
        // 这 12 个新名字的条目，PickKind 对未配置 kind 恒 false → 接线后不会真的刷出，
        // 等 W1 代理补条目 + mobs/models/*.json 后自然生效
        private static readonly MobKind[] DayCandidates =
        {
            MobKind.Pig, MobKind.Cow, MobKind.Chicken, MobKind.Villager,
            MobKind.Sheep, MobKind.Rabbit, MobKind.Fox, MobKind.Deer, MobKind.Panda,
            MobKind.Penguin, MobKind.Goat, MobKind.Raccoon, MobKind.Hamster,
        };
        // 夜晚候选 mob（m11 P0 追加骷髅/蜘蛛/苦力怕，Zombie 保持最高优先级；
        // P0 三新敌对 AI 暂等价僵尸，W1-1 替换专属行为）
        private static readonly MobKind[] NightCandidates =
        {
            MobKind.Zombie, MobKind.Skeleton, MobKind.Spider, MobKind.Creeper,
        };

        private readonly List<Mob> _mobs = new List<Mob>();
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();

        // m8 A2：walk phase 驱动状态（键 = EntityId，RemoveMobAt 一并清理）。
        // 相位按帧间水平位移累计：phase += 位移 × WalkPhasePerMeter；
        // 站定（帧间位移 < StandStillDisplacement）时相位向最近的 π 整数倍缓动
        // （m8 A2 fix1：sin(nπ)=0，腿摆回正直立——不再是停在半摆位冻结）。
        private readonly Dictionary<int, MobView> _viewComponents = new Dictionary<int, MobView>();
        private readonly Dictionary<int, WalkDrive> _walkDrives = new Dictionary<int, WalkDrive>();

        /// <summary>腿摆相位随移动距离的累计速率（rad/格）：1.5 m/s 的猪 ≈ 每秒 12 rad ≈ 2 步/秒。</summary>
        private const float WalkPhasePerMeter = 8f;

        /// <summary>站定判定阈值（格/帧）：帧间位移小于它视为没在走，进入缓动归零。</summary>
        private const float StandStillDisplacement = 0.001f;

        /// <summary>站定时相位向最近 π 整数倍的缓动速率（1/秒）：约 0.3s 内腿摆回正直立。</summary>
        private const float WalkPhaseEasePerSecond = 10f;

        private struct WalkDrive
        {
            public Vector3 LastPos; // 上一帧 mob.Position（算帧间位移用）
            public float Phase;     // 累计腿摆相位
        }

        private int _nextEntityId = 1;
        private float _spawnAccum;
        private World _world;
        private WorldGenerator _generator;
        private TimeOfDay _time;
        private Transform _player;
        private MobSpawnRules _rules;

        private void OnEnable()
        {
            CombatEvents.OnDamageTaken += HandleDamageTaken;
        }

        private void OnDisable()
        {
            CombatEvents.OnDamageTaken -= HandleDamageTaken;
        }

        private void HandleDamageTaken(DamageEvent ev)
        {
            // victim=0 表示玩家受伤
            if (ev.VictimEntityId != 0) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;

            // m7 A1 fix1：伤害统一入口。僵尸近战 / 苦力怕爆炸此前在这里直写
            // ctx.Health.Damage，绕过 PlayerController.TakeDamage 的复活无敌帧——
            // 守尸连杀（A1 要解决的核心威胁）对无敌零保护。改经 TakeDamage 后
            // 无敌早退 / 血条 / 死亡画面（含 Death.OnDeath）都在一处结算；
            // 回满血交给 RespawnAtSpawn，不再需要死亡瞬间 ResetToFull 的旧补丁。
            var pc = _player != null
                ? _player.GetComponent<MyWorld.Unity.Player.PlayerController>()
                : null;
            if (pc != null)
            {
                pc.TakeDamage((int)ev.Amount, ev.AttackerEntityId);
                // 经验：被击中也算 1 点（可选）
                ctx.Experience.Add(1);
                return;
            }

            // 兜底：场景里没有 PlayerController（早期 / 纯逻辑场景）时保持旧直写路径
            ctx.Health.Damage(ev.Amount);
            ctx.Experience.Add(1);

            // 触发死亡（仅兜底路径需要：主路径的死亡已由 TakeDamage → DeathScreenUi 处理）
            if (ctx.Health.IsDead && ctx.Death != null && ctx.Death.IsAlive)
            {
                ctx.Death.OnDeath(new MyWorld.Core.Math.Float3(
                    _player != null ? _player.position.x : 0,
                    _player != null ? _player.position.y : 0,
                    _player != null ? _player.position.z : 0));
                ctx.Health.ResetToFull();
            }
        }

        private void Start()
        {
            // 由 Bootstrap 注入；找不到就退化为全局
            _world = WorldBootstrap.CurrentWorld;
            if (PlayerContext.Instance != null) _time = PlayerContext.Instance.Time;
            var pc = FindObjectOfType<MyWorld.Unity.Player.PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        /// <summary>
        /// 由 <see cref="WorldBootstrap"/> 在 Awake 末尾调用，注入依赖。
        /// <paramref name="generator"/> 与 <paramref name="rules"/> 可为 null：
        /// 为 null 时退化为旧硬编码概率路径（保持现有行为）。
        /// </summary>
        public void Bind(World world, TimeOfDay time, Transform player,
            WorldGenerator generator = null, MobSpawnRules rules = null)
        {
            _world = world;
            _time = time;
            _player = player;
            _generator = generator;
            _rules = rules;
        }

        public IReadOnlyList<Mob> ActiveMobs => _mobs;

        private void Update()
        {
            if (_player == null) return;
            float dt = Time.deltaTime;

            // 昼夜判定（m5 A1 单一真源）：AI tick（m7 A2 僵尸白天不追）
            // 与刷怪光照共用同一次计算
            float dayPhase = _time != null ? _time.DayPhase01 : 0.5f;
            bool isNight = IsNightPhase(dayPhase);

            // 1) 清理已死 mob
            for (int i = _mobs.Count - 1; i >= 0; i--)
            {
                var m = _mobs[i];
                if (m.State == MobState.Dead)
                {
                    RemoveMobAt(i);
                }
                else if (m.State == MobState.Dying)
                {
                    m.DeathTimer -= dt;
                    if (m.DeathTimer <= 0) m.State = MobState.Dead;
                    // X1 fix-up：MobAI 写入 mob.LastDrops（m9 A3 起玩家击杀经 TakeHit 死亡分支
                    // 同步写入，Core-only 死亡仍由 MobAI.Tick 兜底）；Unity 侧负责把每条
                    // 实例化为 ItemDropEntity 并加到 PlayerContext.ItemDrops，让玩家可以拾取。
                    // SpawnDropsForMob 自身幂等（清空 LastDrops 后 no-op），所以多次 tick 安全。
                    if (m.LastDrops != null)
                    {
                        SpawnDropsForMob(m);
                    }
                    // m9 A3：击杀经验入账（TakeHit 致死置 KilledByPlayer；GrantKillExperience
                    // 发完复位标记，Dying 倒计时内多帧调用不重发）
                    if (m.KilledByPlayer)
                    {
                        GrantKillExperience(m);
                    }
                }
            }

            // 1.5) m7 A2 despawn：离玩家太远的 mob 直接移除（只加不减的旧账，见 DespawnDistance）
            TickDespawn();

            // 2) tick AI（m7 A2：传 isNight——僵尸白天走 wander 不追）
            //    m8 A2：tick 后按帧间位移驱动腿摆
            for (int i = 0; i < _mobs.Count; i++)
            {
                var m = _mobs[i];
                MobAI.Tick(m, Float3_From(_player.position), _world, _time, dt, isNight);
                DriveWalkPhase(m, dt);
            }

            // 推进玩家死亡状态
            if (PlayerContext.Instance != null)
            {
                PlayerContext.Instance.Death.Tick(dt);
            }

            // 3) spawn：用 _spawnAccum 控制频率，到点调用 TickSpawn 走规则判定
            _spawnAccum += dt * SpawnChancePerSecond;
            while (_spawnAccum >= 1f && _mobs.Count < MaxMobs)
            {
                _spawnAccum -= 1f;
                int seed = unchecked((int)(Time.time * 1000.0f) ^ _nextEntityId);
                TickSpawn(seed, dayPhase);
            }
        }

        /// <summary>
        /// m7 A2：despawn 检查——距玩家超过 <see cref="DespawnDistance"/> 的 mob 直接移除
        /// （置 Dead 后复用 <see cref="RemoveMobAt"/> 死亡清理路径：销毁视图 + 移出列表）。
        /// Update 每帧调用；公开供 EditMode 测试单独驱动。
        /// Dying 中的 mob 不参与（让死亡动画与掉落自然走完）。
        /// </summary>
        public void TickDespawn()
        {
            if (_player == null) return;
            var playerPos = Float3_From(_player.position);
            float despawnSq = DespawnDistance * DespawnDistance;
            for (int i = _mobs.Count - 1; i >= 0; i--)
            {
                var m = _mobs[i];
                if (!m.IsAlive) continue;
                if (MobAI.DistanceSquared(m.Position, playerPos) > despawnSq)
                {
                    m.State = MobState.Dead;
                    RemoveMobAt(i);
                }
            }
        }

        /// <summary>
        /// 把第 <paramref name="index"/> 只 mob 移出世界：销毁视图 GameObject + 移出列表。
        /// 死亡清理与 despawn（<see cref="TickDespawn"/>）共用这一条路径。
        /// EditMode 测试直接调 TickDespawn 时 Destroy 不可用（编辑器不允许延迟销毁），
        /// 按播放状态切换 DestroyImmediate。
        /// </summary>
        private void RemoveMobAt(int index)
        {
            var m = _mobs[index];
            if (_views.TryGetValue(m.EntityId, out var go))
            {
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
                _views.Remove(m.EntityId);
            }
            // m8 A2：walk phase 驱动状态一并清理，字典不随 despawn 泄漏
            _viewComponents.Remove(m.EntityId);
            _walkDrives.Remove(m.EntityId);
            _mobs.RemoveAt(index);
        }

        /// <summary>
        /// m8 A2：腿摆驱动——按 <paramref name="m"/> 的帧间水平位移累计相位
        /// （phase += 位移 × <see cref="WalkPhasePerMeter"/>），推给 MobView.SetWalkPhase。
        /// 站定（帧间位移 &lt; <see cref="StandStillDisplacement"/>）时相位向最近的
        /// π 整数倍缓动归零（fix1）：sin(nπ + LegPhase)=0，四条腿摆回正直立，
        /// 不会冻结在半摆位。第一帧只记录位置不摆腿（没有帧间位移可比）。
        /// </summary>
        private void DriveWalkPhase(Mob m, float dt)
        {
            if (!_viewComponents.TryGetValue(m.EntityId, out var view) || view == null) return;
            var pos = new Vector3(m.Position.X, m.Position.Y, m.Position.Z);
            if (_walkDrives.TryGetValue(m.EntityId, out var drive))
            {
                float moved = new Vector2(pos.x - drive.LastPos.x, pos.z - drive.LastPos.z).magnitude;
                if (moved < StandStillDisplacement)
                {
                    // 站定缓动归零：向最近 π 整数倍指数逼近（fix1，体验优于冻结在半摆位）
                    float target = Mathf.Round(drive.Phase / Mathf.PI) * Mathf.PI;
                    drive.Phase += (target - drive.Phase) * Mathf.Min(1f, dt * WalkPhaseEasePerSecond);
                }
                else
                {
                    drive.Phase += moved * WalkPhasePerMeter;
                }
                drive.LastPos = pos; // 基准推进到本帧（fix1：漏更会让 moved 变成「距出生点的累计距离」，相位二次加速且永不站定）
                _walkDrives[m.EntityId] = drive;
                view.SetWalkPhase(drive.Phase);
            }
            else
            {
                _walkDrives[m.EntityId] = new WalkDrive { LastPos = pos };
            }
        }

        /// <summary>
        /// 昼夜判定（m5 修复）：夜 = tick∈[13000,23000) 即 phase∈[NightStart/24000, NightEnd/24000)。
        /// 旧代码 <c>phase &lt; 0.5</c> 方向反了——正午判成夜晚、真夜晚反而判成白天。
        /// 阈值常量直接从 <see cref="MyWorld.Core.Time.TimeOfDay"/> 取，两处永不漂移。
        /// </summary>
        public static bool IsNightPhase(float dayPhase01)
        {
            const float nightStart = MyWorld.Core.Time.TimeOfDay.NightStartTick / MyWorld.Core.Time.TimeOfDay.DayLengthTicks;
            const float nightEnd = MyWorld.Core.Time.TimeOfDay.NightEndTick / MyWorld.Core.Time.TimeOfDay.DayLengthTicks;
            return dayPhase01 >= nightStart && dayPhase01 < nightEnd;
        }

        /// <summary>
        /// 单次刷怪检查。给定 seed 与昼夜相位（0..1，[NightStart, NightEnd)/24000 区间视作夜晚，
        /// 见 <see cref="IsNightPhase"/>），按
        /// <see cref="MobSpawnRules.PickKind"/> 决策，命中即实例化 <see cref="MobView"/>。
        /// <para>
        /// 测试可直接调用本方法注入确定性参数，绕开 Random.Range / Time.time。
        /// Update() 与外部调用都走这一条路径。
        /// </para>
        /// </summary>
        public void TickSpawn(int seed, float dayNightPhase)
        {
            if (_player == null) return;
            if (_mobs.Count >= MaxMobs) return;

            // 玩家 chunk 坐标 ± SpawnRadius 内随机挑一个 (x, z)
            int pcx = Mathf.FloorToInt(_player.position.x / 16f);
            int pcz = Mathf.FloorToInt(_player.position.z / 16f);
            int rx = Mathf.Abs((seed * 13) % (SpawnRadiusChunks * 2 + 1)) - SpawnRadiusChunks;
            int rz = Mathf.Abs((seed * 17) % (SpawnRadiusChunks * 2 + 1)) - SpawnRadiusChunks;
            int wx = (pcx + rx) * 16 + Mathf.Abs((seed >> 4) % 12) + 2;
            int wz = (pcz + rz) * 16 + Mathf.Abs((seed >> 8) % 12) + 2;
            int surfaceY = _world != null
                ? FindSurfaceY(_world, wx, wz)
                : 70;
            if (surfaceY < 0) return;

            bool isNight = IsNightPhase(dayNightPhase);
            int light = isNight ? 0 : 15;
            Biome biome = _generator != null ? _generator.BiomeAt(wx, wz) : Biome.Plains;

            int type;
            MobKind kind;
            if (_rules != null)
            {
                // 数据驱动路径：按 biome + light + seed 在候选里挑一个能刷的 kind
                MobKind[] candidates = isNight ? NightCandidates : DayCandidates;
                var picked = _rules.PickKind(biome, light, candidates, seed);
                if (!picked.HasValue) return;
                kind = picked.Value;
                type = MobKindToTypeId(kind);
            }
            else
            {
                // 旧路径：硬编码概率（_rules == null 时回退，保证现有场景/测试不受影响）
                if (isNight)
                {
                    // 夜晚：50% 僵尸 / 30% 骷髅 / 20% 苦力怕
                    uint h = unchecked((uint)(seed * 2654435761));
                    float r = (h & 0xFFFF) / 65535f;
                    type = r < 0.5f ? 3 : (r < 0.8f ? 4 : 5);
                }
                else
                {
                    // 白天：50% 猪 / 35% 羊 / 15% 僵尸
                    uint h = unchecked((uint)(seed * 2654435761));
                    float r = (h & 0xFFFF) / 65535f;
                    type = r < 0.5f ? 1 : (r < 0.85f ? 2 : 3);
                }
                // 旧 mobTypeId 1-5 沿用既有 MobView 默认 cube 视觉
                kind = MobKind.Passive; // 仅占位，MobView.Attach 用 MobTypeId 染色
            }

            SpawnMob(type, kind, new Float3(wx + 0.5f, surfaceY + 1f, wz + 0.5f));
        }

        /// <summary>
        /// 实例化 <see cref="Mob"/> 与对应 GameObject + <see cref="MobView"/>。
        /// m8 A2：五生物走 MobModels 部位表拼装（旧 mobTypeId 1-5 走单 cube 由
        /// MobView 默认分支处理）。
        /// </summary>
        private void SpawnMob(int type, MobKind kind, Float3 position)
        {
            var mob = Mob.Create(type, position);
            mob.EntityId = _nextEntityId++;
            _mobs.Add(mob);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Mob_{kind}_{mob.EntityId}";

            if (UsesPartTable(kind))
            {
                // m8 A2：部位表全权负责视觉——host cube 只剩挂载点 + 攻击碰撞体两个职责：
                //   1) Renderer 禁用：拼装部位已覆盖 host 体积，双份渲染只会重合（消灭重合渲染）
                //   2) 缩放归一：部位表坐标以格为单位，host 保留旧体型缩放会把部件一起拉伸变形；
                //      碰撞体改按部位表站高立起（贴模型纵向范围，不再半埋地下）
                float height = MobAssembly.PartTableHeight(kind);
                go.transform.localScale = Vector3.one;
                var box = go.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.size = new Vector3(0.9f, height, 0.9f);
                    box.center = new Vector3(0f, height * 0.5f, 0f);
                }
                go.GetComponent<Renderer>().enabled = false;
            }
            else
            {
                // 旧 mobTypeId 路径（Passive/Hostile/Neutral）：host 就是本体，
                // 体型/材质/染色沿用既有行为。敌对用细高体型，友好用胖短
                go.transform.localScale = (type == 3 || type == 4 || type == 5)
                    ? new Vector3(0.6f, 1.8f, 0.6f)
                    : new Vector3(0.8f, 1.0f, 1.2f);
                var col = go.GetComponent<BoxCollider>();
                if (col != null) col.size = Vector3.one;

                // m5 A3：host cube 换 URP/Lit 材质——裸 CreatePrimitive 的 Default-Material
                // 是 Standard shader，URP 下渲染洋红。旧 kind 的 host 就是本体（MobView 默认
                // 分支的 MPB mobTypeId 染色叠在这层材质上生效）。
                go.GetComponent<Renderer>().sharedMaterial =
                    UrpMaterialFactory.CreateLit(UrpMaterialFactory.MobBodyColor(kind));
            }

            var view = MobView.Attach(go, mob);
            // m9 B1：战斗手感四件套（闪红/击退/死亡缩小）与 MobView 同宿主——
            // CombatController 命中时 GetComponent 取用；旧测试宿主没挂则跳过
            MobHitFeedback.Attach(go, mob);
            _views[mob.EntityId] = go;
            _viewComponents[mob.EntityId] = view; // m8 A2：walk phase 驱动直接取视图组件
        }

        /// <summary>
        /// 五生物（m8）+ m11 12 新生物走 MobModels 部位表拼装；旧三类保持单 cube 既有路径。
        /// m11 P0 预接线：spawn_rules.json 未加新名条目前不会真的刷出，部位表拼装路径
        /// （依赖 mobs/models/*.json，由 W1-1/W1-2 补齐）不会被走到。
        /// </summary>
        private static bool UsesPartTable(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig:
                case MobKind.Cow:
                case MobKind.Chicken:
                case MobKind.Zombie:
                case MobKind.Villager:
                // m11 P0：12 新生物（9 被动 + 骷髅/蜘蛛/苦力怕）
                case MobKind.Sheep:
                case MobKind.Rabbit:
                case MobKind.Fox:
                case MobKind.Deer:
                case MobKind.Panda:
                case MobKind.Penguin:
                case MobKind.Goat:
                case MobKind.Raccoon:
                case MobKind.Hamster:
                case MobKind.Skeleton:
                case MobKind.Spider:
                case MobKind.Creeper:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Phase D MobKind → Mob.Create 用的 int mobTypeId。
        /// 旧 mobTypeId 1-5 由 SetBlock 反向分支处理。
        /// m11 P0：12 新 kind 的 typeId 直接取枚举数值（15-26）——与 Core 测试实体
        /// MobTypeId=(int)kind 同款约定；Mob.Create 的新分支由 W1 代理随 spawn 条目补齐。
        /// </summary>
        private static int MobKindToTypeId(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig: return 6;
                case MobKind.Cow: return 7;
                case MobKind.Chicken: return 8;
                case MobKind.Zombie: return 9;
                case MobKind.Villager: return 10;
                // m11 P0：新 kind typeId = 枚举数值（15-26）
                case MobKind.Sheep: return (int)MobKind.Sheep;
                case MobKind.Rabbit: return (int)MobKind.Rabbit;
                case MobKind.Fox: return (int)MobKind.Fox;
                case MobKind.Deer: return (int)MobKind.Deer;
                case MobKind.Panda: return (int)MobKind.Panda;
                case MobKind.Penguin: return (int)MobKind.Penguin;
                case MobKind.Goat: return (int)MobKind.Goat;
                case MobKind.Raccoon: return (int)MobKind.Raccoon;
                case MobKind.Hamster: return (int)MobKind.Hamster;
                case MobKind.Skeleton: return (int)MobKind.Skeleton;
                case MobKind.Spider: return (int)MobKind.Spider;
                case MobKind.Creeper: return (int)MobKind.Creeper;
                default: return 1;
            }
        }

        /// <summary>
        /// X1 fix-up：把 Dying mob 的 <c>LastDrops</c> 实例化为 <see cref="ItemDropEntity"/>
        /// 并加到 <see cref="PlayerContext.ItemDrops"/>，让玩家能拾起（走 <c>PlayerController.PickupNearbyDrops</c>）。
        /// 调用后把 <c>mob.LastDrops</c> 置 null，实现幂等——Update 每帧调也不会重复出掉。
        /// <para>
        /// 无 PlayerContext（早期 / 测试场景）或 LastDrops 已为 null/空数组时为 no-op。
        /// </para>
        /// </summary>
        public void SpawnDropsForMob(Mob mob)
        {
            if (mob == null || mob.LastDrops == null || mob.LastDrops.Length == 0) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            foreach (var stack in mob.LastDrops)
            {
                if (stack.IsEmpty) continue;
                var drop = new ItemDropEntity(stack, mob.Position);
                drop.SpawnTime = Time.time; // F1 follow-up：spawn 时刻记录，TryPickupBy 据此判定 0.5s grace
                ctx.ItemDrops.Add(drop);
            }
            mob.LastDrops = null;
        }

        /// <summary>
        /// m9 A3：击杀经验常量表（spec §3「击杀经验」）——猪 3 / 牛 5 / 鸡 2 / 僵尸 10。
        /// 不在表内的 kind（旧 Passive/Hostile、Villager）为 0。
        /// </summary>
        public static int KillExperience(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig: return 3;
                case MobKind.Cow: return 5;
                case MobKind.Chicken: return 2;
                case MobKind.Zombie: return 10;
                default: return 0;
            }
        }

        /// <summary>
        /// m9 A3：给玩家入账击杀经验。mob 由 <see cref="MobAI.TakeHit"/> 致死时标记
        /// <see cref="Mob.KilledByPlayer"/>，Update 的 Dying 分支调用本方法——与掉肉
        /// （<see cref="SpawnDropsForMob"/>）同一处观察死亡，两样奖励不漂移。
        /// 与掉落独立结算：僵尸 rotten_flesh 是 50% 概率，可能一滴肉不掉但经验照发。
        /// 入账后复位标记（幂等，Dying 倒计时 0.5s 内每帧调用只发一次）；
        /// 无 PlayerContext（早期/测试场景）时只复位不入账，为 no-op。
        /// </summary>
        public void GrantKillExperience(Mob mob)
        {
            if (mob == null || !mob.KilledByPlayer) return;
            mob.KilledByPlayer = false; // 先复位：入账与否都只发一次
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            int amount = KillExperience(mob.Kind);
            if (amount > 0)
            {
                ctx.Experience.Add(amount);
            }
        }

        private static int FindSurfaceY(World world, int x, int z)
        {
            for (int y = 200; y >= 0; y--)
            {
                if (world.GetBlock(x, y, z) != 0) return y;
            }
            return -1;
        }

        private static Float3 Float3_From(Vector3 v) => new Float3(v.x, v.y, v.z);
    }
}