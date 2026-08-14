using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.Persistence
{
    /// <summary>
    /// milestone-4 B2/B3：存档服务。写：每 30s + 退出时收集全部游戏状态落盘。
    /// 双层：<c>level.dat</c>（JSON，玩家/时间/熔炉/掉落物）+ <c>regions/*.mwr</c>（方块改动态）。
    /// 读：<see cref="TryRestore"/>（B3），由 Bootstrap 在世界就绪后调用。
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
                // Experience 是 struct，不存在 null 态；缺省即 0/0，无需 ?. 防御
                ExpCurrent = _context.Experience.Current, ExpLevel = _context.Experience.Level,
                SelectedHotbarIndex = _context.Inventory != null ? _context.Inventory.SelectedHotbarIndex : 0,
                Slots = _context.Inventory != null ? SnapshotMappers.SnapshotSlots(_context.Inventory) : null,
            };
        }

        private const int HungerFallback = 20;
        private const float SaturationFallback = 5f;

        // ─── 启动恢复（B3）──────────────────────────────────────────────

        /// <summary>启动恢复。恢复顺序 = spec：时间 → 玩家 → 熔炉 → 掉落物。
        /// <para>降级策略（读容忍）：level.dat 损坏/为空 → 重命名 <c>.corrupt</c> 留案、全新开始返回 false；
        /// seed 不符 → 防串档，整档忽略但**不**重命名；level.dat 不存在 → 全新开始。</para>
        /// 返回是否真的恢复了状态。</summary>
        public bool TryRestore()
        {
            if (_context == null || LevelDataPath == null || !File.Exists(LevelDataPath)) return false;

            LevelData data;
            try
            {
                data = LevelDataCodec.Load(LevelDataPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveLoadService] level.dat 损坏，降级全新开始：{ex.Message}");
                TryRenameCorrupt();
                return false;
            }
            if (data == null)
            {
                // 合法 JSON 但反序列化成 null（如文件内容是 "null"）——同样按坏档处理
                Debug.LogWarning("[SaveLoadService] level.dat 内容为空，降级全新开始");
                TryRenameCorrupt();
                return false;
            }
            if (data.Seed != _seed)
            {
                Debug.LogWarning($"[SaveLoadService] level.dat seed={data.Seed} 与当前 seed={_seed} 不符，整档忽略");
                return false;
            }

            ApplyTime(data.TimeTick);
            ApplyPlayer(data.Player);
            ApplyFurnace(data.Furnace);
            ApplyDrops(data.Drops);
            return true;
        }

        private void ApplyTime(float timeTick)
        {
            if (_context.Time != null) _context.Time.CurrentTick = timeTick;
        }

        /// <summary>玩家全套恢复：Core 运动状态走 RestoreCoreState（整体替换 + 同步 transform），
        /// 生命/饥饿/经验/背包写回 PlayerContext。快照缺字段时各构造参数取缺省值。</summary>
        private void ApplyPlayer(PlayerSnapshot p)
        {
            if (p == null) return;
            if (_player != null)
            {
                // IsGrounded 不入档（LevelData 无该字段），恢复为着地——重力下一步会自行校正
                _player.RestoreCoreState(new PlayerState(
                    new Float3(p.X, p.Y, p.Z),
                    new Float3(p.VX, p.VY, p.VZ),
                    isGrounded: true));
            }

            var health = new Health(p.HealthMax > 0f ? p.HealthMax : 20f);
            health.Current = Mathf.Clamp(p.HealthCurrent, 0f, health.Max);
            _context.Health = health;

            if (_context.HungerSystem != null)
            {
                _context.HungerSystem.Hunger = Mathf.Clamp(p.Hunger, 0, HungerSystem.MaxHunger);
                _context.HungerSystem.Saturation = Mathf.Clamp(p.Saturation, 0f, HungerSystem.MaxSaturation);
            }

            // Experience 是 struct，无 null 态；快照缺字段时构造缺省 0（同 CollectPlayer 侧）
            _context.Experience = new Experience(p.ExpCurrent, p.ExpLevel);

            if (_context.Inventory != null)
            {
                _context.Inventory.SelectedHotbarIndex = p.SelectedHotbarIndex;
                SnapshotMappers.RestoreSlots(_context.Inventory, p.Slots);
            }
        }

        private void ApplyFurnace(FurnaceSnapshot snapshot)
        {
            // RestoreFurnace 内部处理 null snapshot（不动）
            if (_context.FurnaceSystem != null) SnapshotMappers.RestoreFurnace(_context.FurnaceSystem, snapshot);
        }

        private void ApplyDrops(List<DropSnapshot> snapshots)
        {
            // RestoreDrops 重建的实体 SpawnTime=0，这里统一赋当前 Time.time：
            // 宽限期重新计时（spec F1 语义），避免读档瞬间掉落物立刻被拾取判定收走
            _context.ItemDrops.Clear();
            foreach (ItemDropEntity drop in SnapshotMappers.RestoreDrops(snapshots))
            {
                drop.SpawnTime = Time.time;
                _context.ItemDrops.Add(drop);
            }
        }

        /// <summary>坏档改名 <c>level.dat → level.dat.corrupt</c> 留案。
        /// File.Move 无 overwrite——目标已存在先删旧的。改名失败只告警，不影响全新开始。</summary>
        private void TryRenameCorrupt()
        {
            try
            {
                string corrupt = LevelDataPath + ".corrupt";
                if (File.Exists(corrupt)) File.Delete(corrupt);
                File.Move(LevelDataPath, corrupt);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveLoadService] 坏档重命名失败（不影响全新开始）：{ex.Message}");
            }
        }
    }
}
