using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 村民总管理：白天生成 3-5 只一群 + 每帧 tick + 渲染 cube。
    /// 复用 MobView 的视觉风格（cube + PropertyBlock），但走 <see cref="VillagerView"/> 标记身份。
    /// <para>
    /// X3 fix-up：刷怪决策改走 <see cref="MobSpawnRules.PickKind"/>（biome + 光照 + seed 数据驱动），
    /// 而不是写死的 <c>UnityEngine.Random.Range</c>。与 <see cref="MobManager.TickSpawn"/> 共享同一份
    /// spawn_rules.json，自然遵守 Villager.biomes = Plains + Forest 限制。
    /// </para>
    /// <para>
    /// 交易列表委托给 Core <see cref="VillagerOffers.Build"/>：按 (worldX, worldZ, profession)
    /// 哈希挑 3-5 条（spec D7 要求），dotnet 链可独立验证。
    /// </para>
    /// </summary>
    public sealed class VillagerManager : MonoBehaviour
    {
        public int SpawnRadiusChunks = 1;
        public float SpawnChancePerSecond = 0.1f;
        public int MaxVillagers = 6;

        private readonly List<Villager> _villagers = new List<Villager>();
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private int _nextEntityId = 1;
        private float _spawnAccum;
        private World _world;
        private TimeOfDay _time;
        private Transform _player;
        private WorldGenerator _generator;
        private MobSpawnRules _rules;

        public IReadOnlyList<Villager> ActiveVillagers => _villagers;

        private void Start()
        {
            _world = WorldBootstrap.CurrentWorld;
            if (PlayerContext.Instance != null) _time = PlayerContext.Instance.Time;
            var pc = FindObjectOfType<MyWorld.Unity.Player.PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        /// <summary>
        /// 由 <see cref="WorldBootstrap"/> 在 Awake 末尾调用，注入依赖。
        /// <paramref name="generator"/> 与 <paramref name="rules"/> 可为 null：
        /// generator=null 时回退 <see cref="Biome.Plains"/>；rules=null 时跳过 biome 校验。
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

        private void Update()
        {
            if (_player == null) return;
            float dt = Time.deltaTime;

            for (int i = _villagers.Count - 1; i >= 0; i--)
            {
                var v = _villagers[i];
                if (!v.IsAlive)
                {
                    if (_views.TryGetValue(v.EntityId, out var go)) { Destroy(go); _views.Remove(v.EntityId); }
                    _villagers.RemoveAt(i);
                }
                else
                {
                    VillagerAI.Tick(v, Float3_From(_player.position), _time, dt);
                }
            }

            _spawnAccum += dt * SpawnChancePerSecond;
            bool isNight = _time != null && _time.IsNight;
            if (!isNight && _spawnAccum >= 1f && _villagers.Count < MaxVillagers)
            {
                _spawnAccum -= 1f;
                // seed 用 _nextEntityId 保证：同帧内多次调用各自产生不同结果，但跨帧可重现
                TrySpawnOne(seed: _nextEntityId);
            }
        }

        /// <summary>
        /// 单次刷村民检查。给定 <paramref name="seed"/>（外部注入，便于测试确定性），
        /// 按 <see cref="MobSpawnRules.PickKind"/> 数据驱动决策：Plains+Forest 通过、Desert/Mountains 拒绝。
        /// <para>
        /// 测试可直接调用本方法注入确定性 seed，绕开 <c>Time.deltaTime</c> 与 <c>UnityEngine.Random</c>。
        /// </para>
        /// </summary>
        public void TrySpawnOne(int seed)
        {
            if (_player == null) return;

            // 1) 确定性坐标：基于 (玩家 chunk + seed) 哈希挑一个 chunk 内偏移
            int pcx = Mathf.FloorToInt(_player.position.x / 16f);
            int pcz = Mathf.FloorToInt(_player.position.z / 16f);
            int rx = Mathf.Abs((seed * 13) % (SpawnRadiusChunks * 2 + 1)) - SpawnRadiusChunks;
            int rz = Mathf.Abs((seed * 17) % (SpawnRadiusChunks * 2 + 1)) - SpawnRadiusChunks;
            int wx = (pcx + rx) * 16 + Mathf.Abs((seed >> 4) % 12) + 2;
            int wz = (pcz + rz) * 16 + Mathf.Abs((seed >> 8) % 12) + 2;
            int surfaceY = _world != null ? FindSurfaceY(_world, wx, wz) : 70;
            if (surfaceY < 0) return;

            // 2) 数据驱动 biome 检查：Villager.biomes = Plains+Forest → Desert/Mountains 拒绝
            Biome biome = _generator != null ? _generator.BiomeAt(wx, wz) : Biome.Plains;
            if (_rules != null)
            {
                var picked = _rules.PickKind(biome, lightLevel: 15,
                    new[] { MobKind.Villager }, seed);
                if (!picked.HasValue) return; // 该 biome 不允许 Villager
            }

            // 3) 确定性职业：哈希 (wx, wz) → 0..2 = Farmer/Librarian/Blacksmith
            // （旧 Random.Range(1,4) 会取到 None=3；新实现直接 mod 3，永远不会 None）
            uint h = unchecked((uint)(wx * 73856093 ^ wz * 19349663));
            var profession = (VillagerProfession)(int)((h >> 4) % 3);

            // 4) 3-5 条交易（Core VillagerOffers，按 (wx, wz, profession) 哈希挑）
            var offers = VillagerOffers.Build(profession, wx, wz);

            var v = Villager.Create(_nextEntityId++, profession,
                new Float3(wx + 0.5f, surfaceY + 1f, wz + 0.5f), offers);

            _villagers.Add(v);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Villager_{profession}_{v.EntityId}";
            go.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            VillagerView.Attach(go, v);
            _views[v.EntityId] = go;
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