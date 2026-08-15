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
    /// 动物总管理：spawn + tick + 渲染。
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

        // 白天候选 mob（按优先级排序：先猪，后牛/鸡、村民）
        private static readonly MobKind[] DayCandidates = { MobKind.Pig, MobKind.Cow, MobKind.Chicken, MobKind.Villager };
        // 夜晚候选 mob（只有 Zombie）
        private static readonly MobKind[] NightCandidates = { MobKind.Zombie };

        private readonly List<Mob> _mobs = new List<Mob>();
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
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
            ctx.Health.Damage(ev.Amount);
            // 经验：被击中也算 1 点（可选）
            ctx.Experience.Add(1);

            // 触发死亡
            if (ctx.Health.IsDead && ctx.Death.IsAlive)
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

            // 1) 清理已死 mob
            for (int i = _mobs.Count - 1; i >= 0; i--)
            {
                var m = _mobs[i];
                if (m.State == MobState.Dead)
                {
                    if (_views.TryGetValue(m.EntityId, out var go))
                    {
                        Destroy(go);
                        _views.Remove(m.EntityId);
                    }
                    _mobs.RemoveAt(i);
                }
                else if (m.State == MobState.Dying)
                {
                    m.DeathTimer -= dt;
                    if (m.DeathTimer <= 0) m.State = MobState.Dead;
                    // X1 fix-up：MobAI.Tick 写入 mob.LastDrops；Unity 侧负责把每条
                    // 实例化为 ItemDropEntity 并加到 PlayerContext.ItemDrops，让玩家可以拾取。
                    // SpawnDropsForMob 自身幂等（清空 LastDrops 后 no-op），所以多次 tick 安全。
                    if (m.LastDrops != null)
                    {
                        SpawnDropsForMob(m);
                    }
                }
            }

            // 2) tick AI
            for (int i = 0; i < _mobs.Count; i++)
            {
                var m = _mobs[i];
                MobAI.Tick(m, Float3_From(_player.position), _world, _time, dt);
            }

            // 推进玩家死亡状态
            if (PlayerContext.Instance != null)
            {
                PlayerContext.Instance.Death.Tick(dt);
            }

            // 3) spawn：用 _spawnAccum 控制频率，到点调用 TickSpawn 走规则判定
            _spawnAccum += dt * SpawnChancePerSecond;
            float dayPhase = _time != null ? _time.DayPhase01 : 0.5f;
            while (_spawnAccum >= 1f && _mobs.Count < MaxMobs)
            {
                _spawnAccum -= 1f;
                int seed = unchecked((int)(Time.time * 1000.0f) ^ _nextEntityId);
                TickSpawn(seed, dayPhase);
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
        /// Phase D kind 走 Body+Head 双段（旧 mobTypeId 1-5 走单 cube 由 MobView 默认分支处理）。
        /// </summary>
        private void SpawnMob(int type, MobKind kind, Float3 position)
        {
            var mob = Mob.Create(type, position);
            mob.EntityId = _nextEntityId++;
            _mobs.Add(mob);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Mob_{kind}_{mob.EntityId}";

            // 体型按 kind 调；MobView.Setup 也会按 kind 切视觉，缩放与之对齐
            switch (kind)
            {
                case MobKind.Pig:
                    go.transform.localScale = new Vector3(0.9f, 0.6f, 1.2f);
                    break;
                case MobKind.Cow:
                    go.transform.localScale = new Vector3(1.0f, 0.8f, 1.4f);
                    break;
                case MobKind.Chicken:
                    go.transform.localScale = new Vector3(0.4f, 0.4f, 0.5f);
                    break;
                case MobKind.Zombie:
                    go.transform.localScale = new Vector3(0.6f, 1.8f, 0.4f);
                    break;
                case MobKind.Villager:
                    // Task D6：人形（与 Zombie 同体型），稍宽一点显示袍的剪影。
                    go.transform.localScale = new Vector3(0.6f, 1.8f, 0.4f);
                    break;
                default:
                    // 旧 mobTypeId 路径（Passive/Hostile）：敌对用细高体型，友好用胖短
                    go.transform.localScale = (type == 3 || type == 4 || type == 5)
                        ? new Vector3(0.6f, 1.8f, 0.6f)
                        : new Vector3(0.8f, 1.0f, 1.2f);
                    break;
            }

            var col = go.GetComponent<BoxCollider>();
            if (col != null) col.size = Vector3.one;

            // m5 A3：host cube 换 URP/Lit 材质——裸 CreatePrimitive 的 Default-Material
            // 是 Standard shader，URP 下渲染洋红。旧 kind 的 host 就是本体（MobView 默认
            // 分支的 MPB mobTypeId 染色叠在这层材质上生效）。
            go.GetComponent<Renderer>().sharedMaterial =
                UrpMaterialFactory.CreateLit(UrpMaterialFactory.MobBodyColor(kind));

            MobView.Attach(go, mob);
            _views[mob.EntityId] = go;
        }

        /// <summary>
        /// Phase D MobKind → Mob.Create 用的 int mobTypeId。
        /// 旧 mobTypeId 1-5 由 SetBlock 反向分支处理。
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