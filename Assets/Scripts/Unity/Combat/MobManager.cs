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
using MyWorld.Unity.UI;
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
            // m12 W4：飞行 3 种白天刷（猫头鹰夜间，见 NightCandidates）
            MobKind.Sparrow, MobKind.Parrot, MobKind.Butterfly,
        };
        // 夜晚候选 mob（m11 P0 追加骷髅/蜘蛛/苦力怕，Zombie 保持最高优先级；
        // P0 三新敌对 AI 暂等价僵尸，W1-1 替换专属行为）
        private static readonly MobKind[] NightCandidates =
        {
            MobKind.Zombie, MobKind.Skeleton, MobKind.Spider, MobKind.Creeper,
            // m12 W4：猫头鹰夜行性
            MobKind.Owl,
        };

        // m12 W4：水生候选——只在「落点地表块是水」的水柱里刷（TickSpawn 查
        // GetBlock == Water 分流；PickKind 的 biome/light 判定照走，水生条目
        // 配了全群系——水的约束由这一层落点判定承担，取舍注释见 spawn_rules）
        private static readonly MobKind[] AquaticCandidates =
        {
            MobKind.Cod, MobKind.Salmon, MobKind.TropicalFish, MobKind.Pufferfish, MobKind.Turtle,
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

        // m11 W1-4（集成点②）：真实光照采样器——非空时刷怪光照从体积采样
        // （白天 max(天光,方块光)、夜间纯方块光），空时退回昼夜相位近似（白天 15/夜晚 0）
        private MyWorld.Unity.Lighting.ChunkLightSystem _lightSampler;

        /// <summary>已消费过的最近一次爆炸产物（引用比对去重，见 <see cref="TickExplosionDrops"/>）。</summary>
        private MyWorld.Core.Combat.ExplosionResult _lastHandledExplosion;

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
                // m11 W2-3：受伤 -N 红字飘字。挂载点选这里（既有事件挂载点，与
                // DamageFlashUi 订阅同一条 CombatEvents 通道）——WorldBootstrap 本波禁改，
                // FloatTextUi 首次触发时自挂，不需要装配步骤
                FloatTextUi.ShowDamage(ev.Amount);
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

        /// <summary>
        /// m11 W1-4（集成点②）：注入光照采样器（WorldBootstrap 挂 ChunkLightSystem 后调用）。
        /// null（未挂 / 测试）时刷怪光照退回昼夜相位近似，既有行为不变。
        /// </summary>
        public void BindLightSampler(MyWorld.Unity.Lighting.ChunkLightSystem lightSampler)
        {
            _lightSampler = lightSampler;
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
                // av W3-13：生物 idle 叫声——按确定性哈希间隔，距玩家 > 16 格不播
                if (m.IsAlive && _views.TryGetValue(m.EntityId, out var go)
                    && go != null)
                {
                    var mobAudio = go.GetComponent<MyWorld.Unity.Audio.MobAudioSystem>();
                    if (mobAudio != null)
                    {
                        float dist = Vector3.Distance(go.transform.position, _player.position);
                        mobAudio.TickIdle(dt, dist);
                    }
                }
            }

            // 推进玩家死亡状态
            if (PlayerContext.Instance != null)
            {
                PlayerContext.Instance.Death.Tick(dt);
            }

            // 2.5) m11 W1-1（集成点②）：消费爆炸产物——苦力怕在 MobAI.Tick 里起爆
            //     （Core 侧 Detonate 破坏方块 + 滚掉落），这里把掉落实例化成
            //     ItemDropEntity 让玩家能捡（与 SpawnDropsForMob 同一条管线）
            TickExplosionDrops();

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
        /// m11 W1-1（集成点②）：消费 <see cref="MyWorld.Core.Combat.Explosion.LastResult"/>——
        /// 苦力怕起爆在 Core 侧结算（Detonate 破坏方块 + 按 BoundDrops 滚掉落），
        /// Unity 侧负责把掉落清单实例化成 <see cref="ItemDropEntity"/> 进
        /// <see cref="PlayerContext.ItemDrops"/>（掉落物视图/吸附/入包全走既有管线）。
        /// <para>
        /// 幂等：按<b>引用</b>比对最近已消费的产物（每次 Detonate 都新建 result 实例），
        /// Update 每帧调用只发一次；掉落位置在破坏方块清单上确定性轮转
        /// （<c>i % Count</c>，不持随机数对象，与掉落表的确定性纪律一致）。
        /// 无掉落（BoundDrops 未注入 = 方块直接消失）或无 PlayerContext 时 no-op。
        /// </para>
        /// </summary>
        public void TickExplosionDrops()
        {
            var result = MyWorld.Core.Combat.Explosion.LastResult;
            if (result == null || ReferenceEquals(result, _lastHandledExplosion)) return;
            _lastHandledExplosion = result;

            var context = PlayerContext.Instance;
            if (context == null || result.Drops.Count == 0 || result.DestroyedBlocks.Count == 0) return;

            for (int i = 0; i < result.Drops.Count; i++)
            {
                var stack = result.Drops[i];
                if (stack.IsEmpty) continue;

                var (bx, by, bz) = result.DestroyedBlocks[i % result.DestroyedBlocks.Count];
                var drop = new ItemDropEntity(stack, new Float3(bx + 0.5f, by + 0.5f, bz + 0.5f));
                drop.SpawnTime = Time.time; // 0.5s 拾取宽限期从爆心落地起算
                context.ItemDrops.Add(drop);
            }
        }

        /// <summary>
        /// m11 W1-6（集成点②）：外部系统（繁殖幼崽）在指定位置刷一只 mob。
        /// 不占 TickSpawn 的 MaxMobs 名额（玩家主动经营的结果不该挤掉自然刷新），
        /// 返回 null 仅在防御性路径（列表为空）出现——正常必然返回新刷的实体。
        /// </summary>
        public Mob SpawnMobAt(MobKind kind, Float3 position)
        {
            SpawnMob(MobKindToTypeId(kind), kind, position);
            return _mobs.Count > 0 ? _mobs[_mobs.Count - 1] : null;
        }

        /// <summary>按 EntityId 找 mob（繁殖系统长大恢复缩放等按 id 反查）。找不到返回 false。</summary>
        public bool TryGetMobById(int entityId, out Mob mob)
        {
            for (int i = 0; i < _mobs.Count; i++)
            {
                if (_mobs[i].EntityId == entityId)
                {
                    mob = _mobs[i];
                    return true;
                }
            }
            mob = null;
            return false;
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
            // m11 W1-4（集成点②）：光照优先从 ChunkLightSystem 的体积采样
            // （白天 max(天光,方块光)、夜间纯方块光——火把圈夜间也 ≥9，被动生物可亮处刷新）；
            // 采样器未挂 / 落点在体积外退回昼夜相位近似（白天 15 / 夜晚 0，既有行为）
            int light = isNight ? 0 : 15;
            if (_lightSampler != null
                && _lightSampler.TrySampleLight(wx, surfaceY + 1, wz, isNight, out int sampledLight))
            {
                light = sampledLight;
            }
            Biome biome = _generator != null ? _generator.BiomeAt(wx, wz) : Biome.Plains;

            // m12 W4：水柱分流——落点地表块是水（湖/海/水洼顶格）→ 只从水生候选里
            // 挑（PickKind 的 biome/light 判定照走，水的约束由这层落点判定承担）；
            // 水生出生在水顶格内部（+0.5），陆生照旧地表 +1
            bool isWaterColumn = _world != null
                && _world.GetBlock(wx, surfaceY, wz) == MyWorld.Core.Voxel.BlockIds.Water;

            int type;
            MobKind kind;
            if (_rules != null)
            {
                // 数据驱动路径：按 biome + light + seed 在候选里挑一个能刷的 kind
                MobKind[] candidates = isWaterColumn
                    ? AquaticCandidates
                    : (isNight ? NightCandidates : DayCandidates);
                var picked = _rules.PickKind(biome, light, candidates, seed);
                if (!picked.HasValue) return;
                kind = picked.Value;
                // m11 集成点④：村庄半径内村民偏向——首次没挑中村民就重掷一次且
                // 只认 Villager（等价权重×3 的简化实现，白天村庄附近村民成群）
                if (!isNight && kind != MobKind.Villager
                    && _generator != null && _generator.IsInVillageRadius(wx, wz))
                {
                    var retry = _rules.PickKind(biome, light,
                        new[] { MobKind.Villager }, seed ^ 0x5EED);
                    if (retry.HasValue) kind = retry.Value;
                }
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

            // m11 W3-5：和平模式门——开关开时敌对 kind 全不刷（被动照刷）。
            // 简化：只挡「新增」，已在场的敌对 mob 不因开关被删除（让它们自然
            // despawn / 被玩家击杀），关掉开关后下一轮夜间刷新立即恢复。
            if (PeaceMode.Enabled && IsHostileSpawn(kind, type)) return;

            SpawnMob(type, kind, new Float3(
                wx + 0.5f, surfaceY + (isWaterColumn ? 0.5f : 1f), wz + 0.5f));
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
            // av W3-13：生物叫声（idle 哈希间隔 + hurt on hit）
            MyWorld.Unity.Audio.MobAudioSystem.Attach(go, mob.Kind);
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
                // m11 W3-3：Boss 走部位表拼装（2.5 格紫金机甲，mobs/models/machine_guardian.json）。
                // 不进上面的昼夜候选数组——只经图腾召唤（BlockInteraction 召唤路由）
                case MobKind.MachineGuardian:
                // m12 W4：水生 5 + 飞行 4（models JSON 同批入库）
                case MobKind.Cod:
                case MobKind.Salmon:
                case MobKind.TropicalFish:
                case MobKind.Pufferfish:
                case MobKind.Turtle:
                case MobKind.Sparrow:
                case MobKind.Parrot:
                case MobKind.Owl:
                case MobKind.Butterfly:
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
                // m11 W3-3：Boss typeId = 枚举数值 27（只经图腾召唤，不经 TickSpawn）
                case MobKind.MachineGuardian: return (int)MobKind.MachineGuardian;
                // m12 W4：新 kind typeId = 枚举数值（28-36，同 15-26 约定）
                case MobKind.Cod: return (int)MobKind.Cod;
                case MobKind.Salmon: return (int)MobKind.Salmon;
                case MobKind.TropicalFish: return (int)MobKind.TropicalFish;
                case MobKind.Pufferfish: return (int)MobKind.Pufferfish;
                case MobKind.Turtle: return (int)MobKind.Turtle;
                case MobKind.Sparrow: return (int)MobKind.Sparrow;
                case MobKind.Parrot: return (int)MobKind.Parrot;
                case MobKind.Owl: return (int)MobKind.Owl;
                case MobKind.Butterfly: return (int)MobKind.Butterfly;
                default: return 1;
            }
        }

        /// <summary>
        /// m11 W3-5：这次刷怪结果是否敌对（和平模式门 <see cref="TickSpawn"/> 用）。
        /// 数据驱动路径看 kind（四敌对枚举 + 旧 Hostile）；旧路径 kind 恒为
        /// <see cref="MobKind.Passive"/> 占位，改看 mobTypeId 3/4/5（与
        /// <see cref="SpawnMob"/> 里「敌对细高体型」同一组判定值，两条路径都覆盖）。
        /// </summary>
        private static bool IsHostileSpawn(MobKind kind, int mobTypeId)
        {
            switch (kind)
            {
                case MobKind.Hostile:
                case MobKind.Zombie:
                case MobKind.Skeleton:
                case MobKind.Spider:
                case MobKind.Creeper:
                    return true;
                default:
                    return mobTypeId == 3 || mobTypeId == 4 || mobTypeId == 5;
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
        /// m11 W1-1 扩 12 新生物：敌对照卡片（骷髅 8 / 蜘蛛 6 / 苦力怕 9，量级贴僵尸 10）；
        /// 被动照猪/牛/鸡量级按血量缩放（羊2 兔1 狐2 鹿2 熊猫4 企鹅1 山羊2 浣熊1 仓鼠1）。
        /// m11 W3-3 扩 Boss：机元守卫 50（血 60 的对手局 + netherite 链守门人，
        /// 量级 = 全部自然生物之冠——僵尸 10 的 5 倍）。
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
                // m11 W1-1 三敌对（远程/群战/自爆的风险位次：骷髅最高、苦力怕次之、蜘蛛最低）
                case MobKind.Skeleton: return 8;
                case MobKind.Spider: return 6;
                case MobKind.Creeper: return 9;
                // m11 W3-3 Boss（血 60 / 伤 6 / 三招 + 半血召唤——经验照卡片 50）
                case MobKind.MachineGuardian: return 50;
                // m11 W1-1 九被动（血量量级：仓鼠 2 血最不值钱、熊猫 15 血与牛同档）
                case MobKind.Sheep: return 2;
                case MobKind.Rabbit: return 1;
                case MobKind.Fox: return 2;
                case MobKind.Deer: return 2;
                case MobKind.Panda: return 4;
                case MobKind.Penguin: return 1;
                case MobKind.Goat: return 2;
                case MobKind.Raccoon: return 1;
                case MobKind.Hamster: return 1;
                // m12 W4：水生飞行 9 种（小型动物 1-2、龟 3——量级贴鸡/兔）
                case MobKind.Cod: return 1;
                case MobKind.Salmon: return 1;
                case MobKind.TropicalFish: return 1;
                case MobKind.Pufferfish: return 2;
                case MobKind.Turtle: return 3;
                case MobKind.Sparrow: return 1;
                case MobKind.Parrot: return 2;
                case MobKind.Owl: return 2;
                case MobKind.Butterfly: return 1;
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
                // m11 W2-3：经验 +N 绿字飘字（m9 spec「+N 飘字」的欠账）。就在入账处触发，
                // 只对击杀经验飘字——被击中的 +1 不飘（HandleDamageTaken 里不调 Show），
                // 与 spec「击杀经验显示」语义一致；FloatTextUi 自挂，无需装配
                FloatTextUi.ShowExperience(amount);
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