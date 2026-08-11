using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 动物总管理：spawn + tick + 渲染。
    /// 玩家 16 米半径内的 chunk 才会生成。
    /// 友好动物白天全天生成，僵尸仅夜晚生成。
    /// </summary>
    public sealed class MobManager : MonoBehaviour
    {
        public int SpawnRadiusChunks = 1;
        public float SpawnChancePerSecond = 0.4f;
        public int MaxMobs = 24;

        private readonly List<Mob> _mobs = new List<Mob>();
        private readonly Dictionary<int, GameObject> _views = new Dictionary<int, GameObject>();
        private int _nextEntityId = 1;
        private float _spawnAccum;
        private World _world;
        private TimeOfDay _time;
        private Transform _player;

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

        public void Bind(World world, TimeOfDay time, Transform player)
        {
            _world = world;
            _time = time;
            _player = player;
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

            // 3) spawn
            _spawnAccum += dt * SpawnChancePerSecond;
            while (_spawnAccum >= 1f && _mobs.Count < MaxMobs)
            {
                _spawnAccum -= 1f;
                TrySpawnOne();
            }
        }

        private void TrySpawnOne()
        {
            // 玩家 chunk 坐标 ± SpawnRadius 内随机挑一个 (x, z)
            int pcx = Mathf.FloorToInt(_player.position.x / 16f);
            int pcz = Mathf.FloorToInt(_player.position.z / 16f);
            int rx = Random.Range(-SpawnRadiusChunks, SpawnRadiusChunks + 1);
            int rz = Random.Range(-SpawnRadiusChunks, SpawnRadiusChunks + 1);
            int wx = (pcx + rx) * 16 + Random.Range(2, 14);
            int wz = (pcz + rz) * 16 + Random.Range(2, 14);
            int surfaceY = _world != null
                ? FindSurfaceY(_world, wx, wz)
                : 70;
            if (surfaceY < 0) return;

            bool isNight = _time != null && _time.IsNight;
            int type;
            if (isNight)
            {
                // 夜晚：50% 僵尸 / 30% 骷髅 / 20% 苦力怕
                float r = Random.value;
                type = r < 0.5f ? 3 : (r < 0.8f ? 4 : 5);
            }
            else
            {
                // 白天：50% 猪 / 35% 羊 / 15% 僵尸
                float r = Random.value;
                type = r < 0.5f ? 1 : (r < 0.85f ? 2 : 3);
            }
            var mob = Mob.Create(type, new Float3(wx + 0.5f, surfaceY + 1f, wz + 0.5f));
            mob.EntityId = _nextEntityId++;
            _mobs.Add(mob);

            // 创建 GameObject（简单 cube）
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Mob_{type}_{mob.EntityId}";
            // 敌对 mob 用细高一些的体型（人型），友好 mob 用胖短体型
            go.transform.localScale = (type == 3 || type == 4 || type == 5)
                ? new Vector3(0.6f, 1.8f, 0.6f)
                : new Vector3(0.8f, 1.0f, 1.2f);
            // 移除 BoxCollider 之外不需要的东西
            var col = go.GetComponent<BoxCollider>();
            col.size = Vector3.one;
            MobView.Attach(go, mob);
            _views[mob.EntityId] = go;
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
