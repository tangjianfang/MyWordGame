using System;
using System.IO;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.Persistence
{
    /// <summary>
    /// milestone-4 B2：自动存档服务。每 30s + 退出时收集全部游戏状态落盘。
    /// 双层：<c>level.dat</c>（JSON，玩家/时间/熔炉/掉落物）+ <c>regions/*.mwr</c>（方块改动态）。
    /// 恢复（读档）在 B3 接，本组件只负责写。
    /// </summary>
    public sealed class SaveLoadService : MonoBehaviour
    {
        /// <summary>自动保存间隔（秒）。量小，同步写不卡帧。</summary>
        public const float AutoSaveIntervalSeconds = 30f;

        private World _world;
        private PlayerContext _context;
        private PlayerController _player;
        private long _seed;
        private string _worldDir;
        private float _saveTimer;

        /// <summary>level.dat 完整路径。Bind 之前为 null。</summary>
        public string LevelDataPath => _worldDir == null ? null : Path.Combine(_worldDir, "level.dat");

        /// <summary>region 文件目录。Bind 之前为 null。</summary>
        public string RegionsDir => _worldDir == null ? null : Path.Combine(_worldDir, "regions");

        /// <summary>由 Bootstrap（B4）在世界就绪后调用。
        /// <paramref name="saveRoot"/> 为 <c>worlds/</c> 父目录，服务内部拼 <c>&lt;saveRoot&gt;/&lt;seed&gt;/</c>。</summary>
        public void Bind(World world, PlayerContext context, PlayerController player, long seed, string saveRoot)
        {
            _world = world;
            _context = context;
            _player = player;
            _seed = seed;
            _worldDir = Path.Combine(saveRoot, seed.ToString());
        }

        private void Update()
        {
            _saveTimer += Time.deltaTime;
            if (_saveTimer >= AutoSaveIntervalSeconds)
            {
                _saveTimer = 0f;
                SaveNow();
            }
        }

        private void OnApplicationQuit() => SaveNow();

        /// <summary>收集全部状态并落盘。level.dat 与 region 两层各自容错：
        /// 一层失败不影响另一层，region 失败的脏区块保留下轮重试（见 RegionSaveCoordinator）。</summary>
        public void SaveNow()
        {
            if (_world == null || _context == null) return; // 未 Bind，静默跳过
            Directory.CreateDirectory(_worldDir); // 首次保存时建 <saveRoot>/<seed>/ 目录

            try
            {
                var data = new LevelData
                {
                    Seed = _seed,
                    TimeTick = _context.Time != null ? _context.Time.CurrentTick : 0f,
                    Player = CollectPlayer(),
                    Furnace = _context.FurnaceSystem != null
                        ? SnapshotMappers.SnapshotFurnace(_context.FurnaceSystem)
                        : null,
                    Drops = SnapshotMappers.SnapshotDrops(_context.ItemDrops),
                };
                LevelDataCodec.Save(data, LevelDataPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadService] level.dat 保存失败：{ex.Message}");
            }

            try
            {
                RegionSaveCoordinator.SaveDirty(_world, RegionsDir);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveLoadService] region 保存失败（脏区块保留下轮重试）：{ex.Message}");
            }
        }

        /// <summary>玩家全套快照：位置/速度取 Core 状态（未绑定时为默认值），生命/饥饿/经验/背包取 PlayerContext。</summary>
        private PlayerSnapshot CollectPlayer()
        {
            var state = _player != null ? _player.State : default;
            var health = _context.Health;
            return new PlayerSnapshot
            {
                X = state.Position.X, Y = state.Position.Y, Z = state.Position.Z,
                VX = state.Velocity.X, VY = state.Velocity.Y, VZ = state.Velocity.Z,
                HealthCurrent = health.Current, HealthMax = health.Max,
                Hunger = _context.HungerSystem != null ? _context.HungerSystem.Hunger : HungerFallback,
                Saturation = _context.HungerSystem != null ? _context.HungerSystem.Saturation : SaturationFallback,
                ExpCurrent = _context.Experience.Current, ExpLevel = _context.Experience.Level,
                SelectedHotbarIndex = _context.Inventory != null ? _context.Inventory.SelectedHotbarIndex : 0,
                Slots = _context.Inventory != null ? SnapshotMappers.SnapshotSlots(_context.Inventory) : null,
            };
        }

        private const int HungerFallback = 20;
        private const float SaturationFallback = 5f;
    }
}
