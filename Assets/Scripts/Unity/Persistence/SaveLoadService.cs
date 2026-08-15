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
    /// milestone-4 B2/B3 + m5 C3：存档服务。写：每 30s + 退出时收集全部游戏状态落盘。
    /// 双层：<c>level.dat</c>（JSON，玩家/时间/熔炉/掉落物）+ <c>regions/*.mwr</c>（方块改动态）。
    /// 读：<see cref="TryRestore"/>（B3），由 Bootstrap 在世界就绪后调用。
    /// <para>m5 C3 起写盘移后台线程：主线程只做快照收集（纯数据冻结），JSON 序列化 +
    /// Deflate + 文件 IO 全部在 <see cref="WriteExecutor"/>（默认 Task.Run）执行，
    /// 30s 自动保存不再顿挫。退出走 <see cref="SaveNow(bool)"/>(async:false) 同步落盘。</para>
    /// </summary>
    public sealed class SaveLoadService : MonoBehaviour
    {
        /// <summary>自动保存间隔（秒）。m5 C3 起写盘在后台线程，自动保存不再卡帧。</summary>
        public const float AutoSaveIntervalSeconds = 30f;

        private World _world;
        private PlayerContext _context;
        private PlayerController _player;
        private long _seed;
        private string _worldDir;
        private float _saveTimer;

        /// <summary>后台写盘进行中标记。volatile：后台线程 finally 清零即「结果已发布」，
        /// 主线程看到 false 后读取 _pendingClear/_pendingErrors 是安全的（release/acquire）。</summary>
        private volatile bool _writeInProgress;

        /// <summary>上一轮后台写盘待应用的清脏清单（主线程消费，见 ApplyPendingClears）。</summary>
        private List<(ChunkPos Pos, long Version)> _pendingClear;

        /// <summary>上一轮后台写盘攒下的错误消息（后台线程不碰 UnityEngine API，日志由主线程补发）。</summary>
        private List<string> _pendingErrors;

        /// <summary>写盘执行器：默认 Task.Run 后台执行。测试注入同步执行器（<c>a =&gt; a()</c>）
        /// 获得「SaveNow 返回即落盘」的确定性时机。</summary>
        internal Action<Action> WriteExecutor = a => System.Threading.Tasks.Task.Run(a);

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
            ApplyPendingClears(); // 消化上一轮后台写盘的结果（若已完成）
            _saveTimer += Time.deltaTime;
            if (_saveTimer >= AutoSaveIntervalSeconds)
            {
                _saveTimer = 0f;
                SaveNow();
            }
        }

        private void OnApplicationQuit() => SaveNow(async: false);

        /// <summary>异步保存（自动保存 / 手动保存）：主线程冻结快照，写盘交 <see cref="WriteExecutor"/>。</summary>
        public void SaveNow() => SaveNow(async: true);

        /// <summary>收集状态并落盘。level.dat 与 region 两层各自容错：一层失败不影响另一层，
        /// region 失败的脏区块保留下轮重试（见 RegionSaveCoordinator）。
        /// <para><paramref name="async"/> = true：重叠保护——上一轮后台写盘未完成时整轮跳过；
        /// false：同步落盘（退出路径），先等在途后台写完成再内联执行（保证单写者，File.Replace 不被并发破坏）。</para></summary>
        public void SaveNow(bool async)
        {
            if (_world == null || _context == null) return; // 未 Bind，静默跳过
            ApplyPendingClears();
            if (async && _writeInProgress) return; // 重叠保护：上一轮还在写，本轮跳过（30s 后再来）
            if (!async)
            {
                // 同步路径（退出前落盘）必须等在途后台写完成：两个写者并发 File.Replace/File.Move
                // 同一批文件会互相破坏——「原子写」的前提是单写者
                while (_writeInProgress) System.Threading.Thread.Sleep(1);
            }

            // ── 主线程快照阶段：全部冻结为纯数据 / 冻结引用，后台零 UnityEngine API ──
            var data = CollectLevelData();
            var chunkSnapshot = FreezeDirtyChunks(out var versions);
            string worldDir = _worldDir;
            string levelPath = LevelDataPath;
            string regionsDir = RegionsDir;

            void Write()
            {
                var written = new List<ChunkPos>();
                var errors = new List<string>();
                try
                {
                    try
                    {
                        Directory.CreateDirectory(worldDir); // 首次保存时建 <saveRoot>/<seed>/ 目录
                        LevelDataCodec.Save(data, levelPath);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"level.dat 保存失败：{ex.Message}");
                    }

                    try
                    {
                        RegionSaveCoordinator.SaveDirty(chunkSnapshot, regionsDir, written);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"region 保存失败（脏区块保留下轮重试）：{ex.Message}");
                    }

                    // 发布结果：先赋值后清 volatile 标志，主线程看到 false 时清单必然完整可见
                    var pending = new List<(ChunkPos Pos, long Version)>(written.Count);
                    foreach (ChunkPos pos in written) pending.Add((pos, versions[pos]));
                    _pendingClear = pending;
                    _pendingErrors = errors.Count > 0 ? errors : null;
                }
                finally
                {
                    _writeInProgress = false;
                }
            }

            _writeInProgress = true;
            if (async)
            {
                WriteExecutor(Write);
            }
            else
            {
                Write(); // 同步路径不经执行器，内联落盘
                ApplyPendingClears(); // 内联写已完成，清脏立即生效
            }
        }

        /// <summary>消化上一轮后台写盘的结果（只在主线程跑）：先补发错误日志
        /// （Debug.Log 虽号称线程安全，但本项目红线是后台零 UnityEngine API），
        /// 再按版本守卫清脏——保存窗口内又被改过的区块保留脏标记，下轮保存重写。</summary>
        internal void ApplyPendingClears()
        {
            if (_writeInProgress) return; // 结果尚未发布完，等下一帧
            if (_pendingErrors != null)
            {
                foreach (string message in _pendingErrors) Debug.LogError($"[SaveLoadService] {message}");
                _pendingErrors = null;
            }
            if (_pendingClear != null && _world != null)
            {
                foreach ((ChunkPos pos, long version) in _pendingClear) _world.ClearDirtyIfUnchanged(pos, version);
                _pendingClear = null;
            }
        }

        /// <summary>主线程冻结脏区块：(位置 → 区块列引用) + 每列的编辑版本号。
        /// 后台线程只读这份冻结数据，绝不触碰 world 的可变集合——_chunks/_dirtyChunks
        /// 都是 Dictionary/HashSet，跨线程读写会破坏其内部结构。
        /// 区块列<b>本体</b>允许后台写盘期间读到主线程的新改动（torn read）：版本守卫
        /// （ClearDirtyIfUnchanged）会让这些区块保持脏，下轮保存重写覆盖，不会丢改动。
        /// 已卸载的脏区块没有数据可写，stale 标记在主线程当场清。</summary>
        private Dictionary<ChunkPos, ChunkColumn> FreezeDirtyChunks(out Dictionary<ChunkPos, long> versions)
        {
            var snapshot = new Dictionary<ChunkPos, ChunkColumn>();
            versions = new Dictionary<ChunkPos, long>();
            foreach (ChunkPos pos in _world.DirtyChunks)
            {
                if (_world.TryGetChunk(pos, out ChunkColumn column))
                {
                    snapshot[pos] = column;
                    versions[pos] = _world.GetEditVersion(pos);
                }
                else
                {
                    _world.ClearDirty(pos); // 主线程清 stale，后台只写不清
                }
            }
            return snapshot;
        }

        /// <summary>主线程收集 level.dat 全量快照（LevelData/PlayerSnapshot 等均为纯 C# 数据，
        /// 构造完成即与游戏状态解耦，后台线程写盘期间的状态变化不会混入）。</summary>
        private LevelData CollectLevelData()
        {
            return new LevelData
            {
                Seed = _seed,
                TimeTick = _context.Time != null ? _context.Time.CurrentTick : 0f,
                Player = CollectPlayer(),
                Furnace = _context.FurnaceSystem != null
                    ? SnapshotMappers.SnapshotFurnace(_context.FurnaceSystem)
                    : null,
                Drops = SnapshotMappers.SnapshotDrops(_context.ItemDrops),
            };
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
