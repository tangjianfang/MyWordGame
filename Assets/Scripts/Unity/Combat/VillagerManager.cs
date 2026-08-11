using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 村民总管理：白天生成 3-5 只一群 + 每帧 tick + 渲染 cube。
    /// 复用 MobView 的视觉风格（cube + PropertyBlock），但走 <see cref="VillagerView"/> 标记身份。
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

        public IReadOnlyList<Villager> ActiveVillagers => _villagers;

        private void Start()
        {
            _world = WorldBootstrap.CurrentWorld;
            if (PlayerContext.Instance != null) _time = PlayerContext.Instance.Time;
            var pc = FindObjectOfType<MyWorld.Unity.Player.PlayerController>();
            if (pc != null) _player = pc.transform;
        }

        public void Bind(World world, TimeOfDay time, Transform player)
        {
            _world = world;
            _time = time;
            _player = player;
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
                TrySpawnOne();
            }
        }

        private void TrySpawnOne()
        {
            int pcx = Mathf.FloorToInt(_player.position.x / 16f);
            int pcz = Mathf.FloorToInt(_player.position.z / 16f);
            int wx = (pcx + Random.Range(-SpawnRadiusChunks, SpawnRadiusChunks + 1)) * 16 + Random.Range(2, 14);
            int wz = (pcz + Random.Range(-SpawnRadiusChunks, SpawnRadiusChunks + 1)) * 16 + Random.Range(2, 14);
            int surfaceY = _world != null ? FindSurfaceY(_world, wx, wz) : 70;
            if (surfaceY < 0) return;

            // 随机职业 + 交易
            var profession = (VillagerProfession)Random.Range(1, 4);   // 跳过 None
            var offers = BuildOffersFor(profession);
            var v = Villager.Create(_nextEntityId++, profession, new Float3(wx + 0.5f, surfaceY + 1f, wz + 0.5f), offers);

            _villagers.Add(v);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Villager_{profession}_{v.EntityId}";
            go.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
            VillagerView.Attach(go, v);
            _views[v.EntityId] = go;
        }

        private static IEnumerable<TradeOffer> BuildOffersFor(VillagerProfession p)
        {
            // 简化的 3-4 条交易：emerald → 物品
            switch (p)
            {
                case VillagerProfession.Farmer:
                    return new[]
                    {
                        new TradeOffer("emerald", 1, "beet", 4, 0, 8),
                        new TradeOffer("emerald", 3, "mung_bean_soup", 1, 0, 4),
                    };
                case VillagerProfession.Librarian:
                    return new[]
                    {
                        new TradeOffer("emerald", 5, "enchanted_book", 1, 0, 3),
                        new TradeOffer("book", 3, "emerald", 1, 0, 16),
                    };
                case VillagerProfession.Blacksmith:
                    return new[]
                    {
                        new TradeOffer("emerald", 10, "diamond_sword", 1, 0, 1),
                        new TradeOffer("emerald", 3, "iron_ingot", 1, 0, 16),
                    };
                default:
                    return new[]
                    {
                        new TradeOffer("emerald", 1, "log", 4, 0, 16),
                    };
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