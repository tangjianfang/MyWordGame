using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.Persistence
{
    /// <summary>
    /// milestone-4 B2/B3 + m5 C3：存档服务。写：每 30s + 退出时收集全部游戏状态落盘。
    /// 双层：<c>level.dat</c>（JSON，玩家/时间/熔炉/掉落物/任务链进度[m6 C4]）+ <c>regions/*.mwr</c>（方块改动态）。
    /// 读：<see cref="TryRestore"/>（B3），由 Bootstrap 在世界就绪后调用。
    /// <para>m5 C3 起写盘移后台线程：主线程只做快照收集（纯数据冻结），JSON 序列化 +
    /// Deflate + 文件 IO 全部在 <see cref="WriteExecutor"/>（默认 Task.Run）执行，
    /// 30s 自动保存不再顿挫。退出走 <see cref="SaveNow(bool)"/>(async:false) 同步落盘，
    /// m7 A4 fix1 起返回成败（失败原因经 <see cref="LastSaveError"/>）——
    /// 帮助菜单「保存并退出」据它决定退出还是留下重试，IO 失败不静默吞掉后照退。</para>
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

        /// <summary>异步保存（自动保存 / 手动保存）：主线程冻结快照，写盘交 <see cref="WriteExecutor"/>。
        /// 返回本轮是否成功调度（撞重叠保护 / 调度失败为 false；写盘本身的成败稍后经
        /// <see cref="LastSaveError"/> 与错误日志发布）。既有调用方（30s 自动保存等）忽略返回值，语义不变。</summary>
        public bool SaveNow() => SaveNow(async: true);

        /// <summary>收集状态并落盘。level.dat 与 region 两层各自容错：一层失败不影响另一层，
        /// region 失败的脏区块保留下轮重试（见 RegionSaveCoordinator）。
        /// <para><paramref name="async"/> = true：重叠保护——上一轮后台写盘未完成时整轮跳过；
        /// false：同步落盘（退出路径），先等在途后台写完成再内联执行（保证单写者，File.Replace 不被并发破坏）。</para>
        /// <para>m7 A4 fix1 起返回本轮保存是否成功（void → bool，源兼容，既有调用方照旧）：
        /// async=false 返回「同步写完且两层都无错误」——帮助菜单「保存并退出」靠它决定
        /// 退出还是留下重试，IO 失败不再被静默吞掉后照退（丢档）。async=true 只承诺
        /// 「本轮成功调度」（写盘在后台，成败看稍后的错误日志 / <see cref="LastSaveError"/>）。
        /// 未 Bind 直接返回 true：无事发生，不算失败。</para></summary>
        public bool SaveNow(bool async)
        {
            if (_world == null || _context == null) return true; // 未 Bind，静默跳过
            ApplyPendingClears();
            if (async && _writeInProgress) return false; // 重叠保护：上一轮还在写，本轮跳过（30s 后再来）
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

            bool failed = false; // Write() 闭包写入；async 路径调度完即返回不读它，无跨线程读

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
                        failed = true;
                        errors.Add($"level.dat 保存失败：{ex.Message}");
                    }

                    try
                    {
                        // 评审 05 T-B1：单个 region 的 IO/坏档失败在协调器内部按批捕获，
                        // 错误经清单带回这里并入 failed——region 层失败同样让 SaveNow 返回
                        // false，「保存并退出」如实报错可重试，不再误报成功照退。
                        var regionErrorTexts = new List<string>();
                        RegionSaveCoordinator.SaveDirty(chunkSnapshot, regionsDir, written, regionErrorTexts);
                        if (regionErrorTexts.Count > 0)
                        {
                            failed = true;
                            foreach (string regionError in regionErrorTexts) errors.Add(regionError);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed = true;
                        errors.Add($"region 保存失败（脏区块保留下轮重试）：{ex.Message}");
                    }

                    // 发布结果：先赋值后清 volatile 标志，主线程看到 false 时清单必然完整可见
                    var pending = new List<(ChunkPos Pos, long Version)>(written.Count);
                    foreach (ChunkPos pos in written) pending.Add((pos, versions[pos]));
                    _pendingClear = pending;
                    _pendingErrors = errors.Count > 0 ? errors : null;
                    _lastSaveErrors = errors.Count > 0 ? errors : null;
                }
                finally
                {
                    _writeInProgress = false;
                }
            }

            _writeInProgress = true;
            if (async)
            {
                try
                {
                    WriteExecutor(Write);
                }
                catch (Exception ex)
                {
                    // 执行器<b>本身</b>抛（Task.Run 调度失败 / 注入的坏执行器）：Write 的 finally
                    // 不会跑，这里必须复位标志——否则重叠保护会把之后所有自动保存静默跳过（存档失效），
                    // 退出路径的等待在途写也会死等。脏区块语义不变：本轮没写成，保留下轮重试。
                    _writeInProgress = false;
                    Debug.LogError($"[SaveLoadService] 写盘调度失败（本轮跳过，脏区块保留下轮重试）：{ex.Message}");
                    return false;
                }
                return true; // 调度成功即返回（写盘成败稍后发布）
            }

            Write(); // 同步路径不经执行器，内联落盘
            ApplyPendingClears(); // 内联写已完成，清脏立即生效
            return !failed; // 写盘已内联完成，成败立即可知
        }

        /// <summary>最近一轮<b>完成写盘</b>的保存错误清单（空 = 无错误）。与
        /// <see cref="_pendingClear"/> 同一处发布（后台线程写、主线程读，volatile 标志保证可见性），
        /// 撞重叠保护被跳过的轮次不会更新它。m7 A4 fix1：帮助菜单「保存并退出」
        /// 在 SaveNow 返回 false 时经 <see cref="LastSaveError"/> 把原因亮给玩家。</summary>
        private List<string> _lastSaveErrors;

        /// <summary>最近一轮完成写盘的保存错误全文（两层错误「；」连接）；无错误为 null。</summary>
        public string LastSaveError =>
            _lastSaveErrors != null && _lastSaveErrors.Count > 0
                ? string.Join("；", _lastSaveErrors)
                : null;

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
            var data = new LevelData
            {
                Seed = _seed,
                TimeTick = _context.Time != null ? _context.Time.CurrentTick : 0f,
                Player = CollectPlayer(),
                Furnace = _context.FurnaceSystem != null
                    ? SnapshotMappers.SnapshotFurnace(_context.FurnaceSystem)
                    : null,
                Drops = SnapshotMappers.SnapshotDrops(_context.ItemDrops),
                // m6 C4：任务进度从全局总线拿绑定的 QuestSystem（无链 / 无总线 → null，
                // 恢复侧按 null 全新开始）。SaveState 只读纯数据，主线程冻结语义与其余层一致
                Quest = QuestEventBus.Instance?.Quests?.SaveState(),
                // m11 W2-4：多章节任务书全量进度（每章一个 QuestState）。Quest 字段继续写
                // 「当前活动章」快照（旧版读档兼容）；新读档路径优先用本字段整本恢复
                QuestChapters = QuestEventBus.Instance?.Campaign?.SaveAll(),
            };

            // m11 第 1 波（集成点②）：农田 / 箱子 / 床三层。各系统自持导出逻辑——
            // 可空（数据表缺失时 WorldBootstrap 没建实例）跳过该层，LevelData 对应字段
            // 留 null，读档侧同样按 null 全新开始，与熔炉/任务链同一兼容策略。
            if (_context.FarmSystem != null) data.FarmStates = _context.FarmSystem.ExportFarmStates();
            _context.ChestSystem?.SaveTo(data);
            _context.BedSystem?.SaveTo(data);
            // m11 W2-2 C4：装备附魔整表往返（照 Farm/Chest 挂法）。附魔存全局
            // EnchantStore.Default（PlayerContext 挂不了字段，W2-2 的取舍），
            // 空表不写字段（留 null，旧档语义一致）
            if (MyWorld.Core.Enchanting.EnchantStore.Default.Count > 0)
            {
                data.PlayerEnchantments = MyWorld.Core.Enchanting.EnchantStore.Default.ToSaveDictionary();
            }
            // m11 W3-3：Boss 图腾已用登记（照 PlayerEnchantments 挂法：全局单例
            // BossSummonState.Default，空表不写字段留 null，旧档语义一致）
            if (MyWorld.Core.Entities.BossSummonState.Default.Count > 0)
            {
                data.UsedBossTotems = MyWorld.Core.Entities.BossSummonState.Default.Export();
            }
            // m12 第 1 波：成就进度 + 图鉴解锁集共用 Stats 字典（空系统 / 全零跳过留 null）
            if (_context.Achievements != null || _context.Codex != null)
            {
                var stats = new System.Collections.Generic.Dictionary<string, int>();
                _context.Achievements?.ExportStats(stats);
                _context.Codex?.ExportStats(stats);
                if (stats.Count > 0)
                {
                    data.Stats = stats;
                }
            }
            return data;
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
                // m11 W2-1：穿戴栏 4 槽（ArmorSlots 是 readonly 字段初始化，永不为 null，
                // 防御式判空只为与上面 Inventory 同构）
                ArmorSlots = _context.ArmorSlots != null ? SnapshotMappers.SnapshotArmor(_context.ArmorSlots) : null,
            };
        }

        private const int HungerFallback = 20;
        private const float SaturationFallback = 5f;

        // ─── 启动恢复（B3）──────────────────────────────────────────────

        /// <summary>启动恢复。恢复顺序 = spec：时间 → 玩家 → 熔炉 → 掉落物 → 任务链（m6 C4 追加）
        /// → 农田 → 箱子 → 床（m11 第 1 波集成点②追加）。
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
            ApplyQuest(data);

            // m12 第 1 波：成就 / 图鉴进度恢复（Stats 由 Codec 归一非空；缺键 = 全新开始）
            _context.Achievements?.ImportStats(data.Stats);
            _context.Codex?.ImportStats(data.Stats);
            // m11 第 1 波（集成点②）：农田 → 箱子 → 床。各层自带 null 容忍
            //（旧档无字段 / 系统实例未建都跳过），坏一层不挡其余层
            ApplyFarm(data.FarmStates);
            ApplyChest(data);
            ApplyBed(data);
            // m11 W2-2 C4：附魔恢复（农田/箱子/床之后）。坏行由 FromSaveDictionary
            // 逐条读容忍跳过；旧档无字段 = null = 清空（全新开始）
            ApplyEnchants(data.PlayerEnchantments);
            // m11 W3-3：Boss 图腾登记恢复（附魔之后）。Import 自带 null/坏行容忍；
            // null（旧档 / 未召唤过）= 清空 = 全部图腾重新可用
            MyWorld.Core.Entities.BossSummonState.Default.Import(data.UsedBossTotems);
            return true;
        }

        /// <summary>农田作物状态恢复：ImportFarmStates 自带逐条读容忍（坏键跳过），
        /// null（旧档 / FarmSystem 未建）= 全新开始。恢复的阶段计时从当前时刻重计
        ///（存档不记阶段进度，Core 侧既定取舍）。</summary>
        private void ApplyFarm(Dictionary<string, string> states)
        {
            _context.FarmSystem?.ImportFarmStates(states);
        }

        /// <summary>箱子内容恢复：LoadFrom 全量替换 + null 容忍（旧档空字典归一由 Codec 兜）。</summary>
        private void ApplyChest(LevelData data)
        {
            _context.ChestSystem?.LoadFrom(data);
        }

        /// <summary>床数据恢复：LoadFrom 全量替换（末条 = 当前重生点，Core 侧既定约定）。</summary>
        private void ApplyBed(LevelData data)
        {
            _context.BedSystem?.LoadFrom(data);
        }

        /// <summary>装备附魔恢复（m11 W2-2 C4）：存档字典 → 新实例 → 灌回全局 Default。
        /// 坏行跳过（<see cref="MyWorld.Core.Enchanting.EnchantStore.FromSaveDictionary"/>
        /// 逐条读容忍），null = 清空全新开始。</summary>
        private void ApplyEnchants(Dictionary<string, string> saved)
        {
            MyWorld.Core.Enchanting.EnchantStore.Default.ReplaceAllFrom(
                MyWorld.Core.Enchanting.EnchantStore.FromSaveDictionary(saved));
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

            // m11 W2-1：穿戴栏恢复。旧档无 armorSlots 键经 Codec 归一为空数组 = 空穿戴
            //（I3 兼容策略）；null（ArmorSlots 字段缺失且 Player 节点没归一到的非常规档）
            // 同样清空。恢复不校验部位——与 RestoreSlots 同态度，档里是什么收什么。
            if (_context.ArmorSlots != null)
            {
                SnapshotMappers.RestoreArmor(_context.ArmorSlots, p.ArmorSlots);
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

        /// <summary>任务进度恢复（m6 C4；m11 W2-4 起多章节）。总线引用从全局
        /// <see cref="QuestEventBus"/> 拿（WorldBootstrap 装配顺序：总线先 Bind、本服务再 TryRestore）。
        /// 优先走 <see cref="LevelData.QuestChapters"/>（新档整本恢复）；旧档只有单章
        /// <see cref="LevelData.Quest"/> 字段时回退 <see cref="QuestCampaign.RestoreLegacy"/>
        /// （第一章接续、后续章节全新开始）。无总线 / 无任务书时本层整体跳过（任务链全新开始）。</summary>
        private void ApplyQuest(LevelData data)
        {
            QuestCampaign campaign = QuestEventBus.Instance?.Campaign;
            if (campaign == null) return; // 无总线 / 无链：无从恢复，任务链保持全新
            if (data.QuestChapters != null)
            {
                // 新档：整本恢复。逐章的坏状态（换章内容读旧档）由 RestoreAll 内部
                // 按章降级（该章全新开始），不会拖垮其余章，也无需这里再捕获
                campaign.RestoreAll(data.QuestChapters);
                return;
            }
            // 旧档：单章字段恢复进第一章（null = 更老的档，整本全新开始）
            campaign.RestoreLegacy(data.Quest);
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
