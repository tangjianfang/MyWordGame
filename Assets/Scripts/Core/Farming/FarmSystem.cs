using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Farming
{
    /// <summary>作物种类（m11 W1-6）。三作物共用 stage0→1→2 三阶段模型。</summary>
    public enum CropKind
    {
        /// <summary>小麦：产物 wheat，种子 seeds_wheat（另可由小麦手工合成）。</summary>
        Wheat = 0,

        /// <summary>甜菜：产物 beet，种子 seeds_beet（仅收获自然掉）。</summary>
        Beet = 1,

        /// <summary>绿豆：产物 mung_bean，种子 seeds_mung（仅收获自然掉）。三作物里最快熟。</summary>
        Mung = 2,
    }

    /// <summary>
    /// 农田系统（m11 W1-6）：锄地 / 播种 / 确定性生长 / 骨粉催熟 / 成熟收获。
    /// <para>
    /// <b>确定性</b>：单级生长时长 = 作物基准 + [0, 基准/4] 的整数哈希抖动（世界坐标 + seed 派生，
    /// 不持随机数对象，与 <see cref="BlockDrops.RollCount"/> 同风格）；步进判定基于绝对累计 tick 的
    /// 阈值比较，与 Tick 调用粒度无关——同 seed 同累计 tick 必得同一结果，replay 可复现。
    /// </para>
    /// <para>
    /// <b>湿地加速</b>：作物脚下是 <c>farmland_wet</c> 时单级时长减半（<see cref="WetGrowthSpeedup"/>）。
    /// 干湿转换（邻水判定）由 Unity 侧接线负责，本系统只读方块 id。
    /// </para>
    /// <para>
    /// <b>序列化</b>：作物状态导出为 <c>"x,y,z" → "crop:stage"</c>（与
    /// <see cref="MyWorld.Core.Persistence.LevelData.FarmStates"/> 及 LevelDataSchemaTests 样例一致；
    /// 解析侧额外容忍 <c>crop:stage:任意</c> 第三段以便未来扩展）。Unity 侧右键路由
    /// （锄/种子/骨粉/收获）在集成点②接线，本类零 Unity 依赖。
    /// </para>
    /// </summary>
    public sealed class FarmSystem
    {
        // ── 方块 numericId 常量（与 blocks/*.json 手动保持一致，照 BlockIds 的惯例；
        //    守卫测试 FarmJsonGuardTests 逐个断言一致）。刻意挑 1050 起的独立段，
        //    与第 1 波其它代理的 1013+ 顺延段错开，避免并行合并时 numericId 撞车。──
        public const ushort FarmlandId = 1050;
        public const ushort FarmlandWetId = 1051;
        public const ushort WheatStage0Id = 1052;
        public const ushort WheatStage1Id = 1053;
        public const ushort WheatStage2Id = 1054;
        public const ushort BeetStage0Id = 1055;
        public const ushort BeetStage1Id = 1056;
        public const ushort BeetStage2Id = 1057;
        public const ushort MungStage0Id = 1058;
        public const ushort MungStage1Id = 1059;
        public const ushort MungStage2Id = 1060;

        /// <summary>阶段总数（0 发芽 / 1 半熟 / 2 成熟）。</summary>
        public const int StageCount = 3;

        /// <summary>成熟阶段号（收获判定线）。</summary>
        public const int MatureStage = StageCount - 1;

        /// <summary>湿耕地生长加速倍率：单级时长 ÷ 2。</summary>
        public const float WetGrowthSpeedup = 2f;

        /// <summary>作物单级生长基准 tick（一天 24000 tick / 时钟 Speed 60 → 一天 400 实秒）。
        /// 小麦半白天熟、绿豆最快——节奏照「孩子一节课能收一茬」定。</summary>
        public static int BaseStageTicks(CropKind crop)
        {
            switch (crop)
            {
                case CropKind.Wheat: return 4000;
                case CropKind.Beet: return 3600;
                case CropKind.Mung: return 3000;
                default: throw new ArgumentOutOfRangeException(nameof(crop), crop, "未知作物");
            }
        }

        /// <summary>作物的收获产物 itemId（跨表引用，构造时校验已注册）。</summary>
        public static string ProductItemId(CropKind crop)
        {
            switch (crop)
            {
                case CropKind.Wheat: return "wheat";
                case CropKind.Beet: return "beet";
                case CropKind.Mung: return "mung_bean";
                default: throw new ArgumentOutOfRangeException(nameof(crop), crop, "未知作物");
            }
        }

        /// <summary>作物的种子 itemId（甜菜/绿豆种子只从收获来；小麦种子另可合成）。</summary>
        public static string SeedItemId(CropKind crop)
        {
            switch (crop)
            {
                case CropKind.Wheat: return "seeds_wheat";
                case CropKind.Beet: return "seeds_beet";
                case CropKind.Mung: return "seeds_mung";
                default: throw new ArgumentOutOfRangeException(nameof(crop), crop, "未知作物");
            }
        }

        /// <summary>阶段 → 方块 numericId。</summary>
        public static ushort StageBlockId(CropKind crop, int stage)
        {
            if (stage < 0 || stage >= StageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "阶段号必须在 [0, StageCount)");
            }
            return (crop, stage) switch
            {
                (CropKind.Wheat, 0) => WheatStage0Id,
                (CropKind.Wheat, 1) => WheatStage1Id,
                (CropKind.Wheat, 2) => WheatStage2Id,
                (CropKind.Beet, 0) => BeetStage0Id,
                (CropKind.Beet, 1) => BeetStage1Id,
                (CropKind.Beet, 2) => BeetStage2Id,
                (CropKind.Mung, 0) => MungStage0Id,
                (CropKind.Mung, 1) => MungStage1Id,
                (CropKind.Mung, 2) => MungStage2Id,
                _ => throw new ArgumentOutOfRangeException(nameof(crop), crop, "未知作物"),
            };
        }

        /// <summary>方块 numericId → 作物与阶段（任一作物方块都能解回）。</summary>
        public static bool TryParseStageBlock(ushort blockId, out CropKind crop, out int stage)
        {
            switch (blockId)
            {
                case WheatStage0Id: crop = CropKind.Wheat; stage = 0; return true;
                case WheatStage1Id: crop = CropKind.Wheat; stage = 1; return true;
                case WheatStage2Id: crop = CropKind.Wheat; stage = 2; return true;
                case BeetStage0Id: crop = CropKind.Beet; stage = 0; return true;
                case BeetStage1Id: crop = CropKind.Beet; stage = 1; return true;
                case BeetStage2Id: crop = CropKind.Beet; stage = 2; return true;
                case MungStage0Id: crop = CropKind.Mung; stage = 0; return true;
                case MungStage1Id: crop = CropKind.Mung; stage = 1; return true;
                case MungStage2Id: crop = CropKind.Mung; stage = 2; return true;
                default: crop = default; stage = 0; return false;
            }
        }

        /// <summary>种子 itemId → 作物。不是种子的物品（含未注册 id）返回 false。</summary>
        public static bool TryResolveSeed(string itemId, out CropKind crop)
        {
            switch (itemId)
            {
                case "seeds_wheat": crop = CropKind.Wheat; return true;
                case "seeds_beet": crop = CropKind.Beet; return true;
                case "seeds_mung": crop = CropKind.Mung; return true;
                default: crop = default; return false;
            }
        }

        /// <summary>锄头能翻的地：草方块或泥土（含雪下视为泥土的场合由调用方换算，这里只认 id）。</summary>
        public static bool CanTill(ushort blockId)
            => blockId == BlockIds.Grass || blockId == BlockIds.Dirt;

        private sealed class CropState
        {
            public CropKind Crop;
            public int Stage;
            /// <summary>进入当前阶段的绝对累计 tick（阈值判定用，存档不导出、读档重置）。</summary>
            public float StageEnteredAtTicks;
        }

        private readonly ItemDatabase _items;
        private readonly int _seed;
        private readonly Dictionary<string, CropState> _crops = new Dictionary<string, CropState>();

        // Tick 是逐帧热路径：复用暂存列表，不在每帧 new 容器（照 ChunkStreamer 的 GC 纪律）
        private readonly List<string> _scratchRemoved = new List<string>();
        private readonly List<string> _scratchKeys = new List<string>();

        /// <summary>累计生长 tick（由 Unity 侧每帧喂 TimeOfDay 的推进量）。</summary>
        public float TotalTicks { get; private set; }

        /// <summary>当前追踪中的作物数（调试/测试用）。</summary>
        public int TrackedCropCount => _crops.Count;

        /// <summary>构造时校验作物表引用的全部产物/种子都已注册（照 BlockDrops 的跨表引用契约）。</summary>
        public FarmSystem(ItemDatabase items, int seed)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _seed = seed;

            foreach (CropKind crop in Enum.GetValues(typeof(CropKind)))
            {
                if (!items.TryGetById(ProductItemId(crop), out _))
                {
                    throw new InvalidDataException($"农田系统引用的产物未注册：{ProductItemId(crop)}");
                }
                if (!items.TryGetById(SeedItemId(crop), out _))
                {
                    throw new InvalidDataException($"农田系统引用的种子未注册：{SeedItemId(crop)}");
                }
            }
        }

        // ─── 锄地 ─────────────────────────────────────────────────────

        /// <summary>锄右键：草/泥土 → 干耕地。非可锄方块返回 false 且不动世界。</summary>
        public bool Till(World world, int x, int y, int z)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!CanTill(world.GetBlock(x, y, z))) return false;
            world.SetBlock(x, y, z, FarmlandId);
            return true;
        }

        // ─── 播种 ─────────────────────────────────────────────────────

        /// <summary>种子右键：在耕地正上方的空气位生成 stage0 作物。目标位非空 / 脚下非耕地 / 物品不是种子 → false。</summary>
        public bool TryPlant(World world, int x, int y, int z, string seedItemId)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!TryResolveSeed(seedItemId, out CropKind crop)) return false;
            if (world.GetBlock(x, y, z) != BlockIds.Air) return false;

            ushort below = world.GetBlock(x, y - 1, z);
            if (below != FarmlandId && below != FarmlandWetId) return false;

            world.SetBlock(x, y, z, StageBlockId(crop, 0));
            _crops[Key(x, y, z)] = new CropState
            {
                Crop = crop,
                Stage = 0,
                StageEnteredAtTicks = TotalTicks,
            };
            return true;
        }

        // ─── 骨粉 ─────────────────────────────────────────────────────

        /// <summary>骨粉右键：作物催熟一级。成熟或非作物 → false（不消耗调用方扣的骨粉）。</summary>
        public bool ApplyBoneMeal(World world, int x, int y, int z)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!TryParseStageBlock(world.GetBlock(x, y, z), out CropKind crop, out int stage)) return false;
            if (stage >= MatureStage) return false;

            int next = stage + 1;
            world.SetBlock(x, y, z, StageBlockId(crop, next));
            SetState(x, y, z, crop, next);
            return true;
        }

        // ─── 收获 ─────────────────────────────────────────────────────

        /// <summary>目标位是否任一作物方块（成熟与否都算）。</summary>
        public bool IsCropAt(World world, int x, int y, int z)
            => TryParseStageBlock(world.GetBlock(x, y, z), out _, out _);

        /// <summary>目标位是否成熟作物（收获判定线）。</summary>
        public bool IsMature(World world, int x, int y, int z)
            => TryParseStageBlock(world.GetBlock(x, y, z), out _, out int stage) && stage == MatureStage;

        /// <summary>
        /// 收获成熟作物：清方块、清状态、返回确定性掉落（产物 1-2 + 种子区间，
        /// 数量用 <see cref="BlockDrops.RollCount"/> 的整数哈希掷骰）。未成熟返回空数组且不动世界。
        /// </summary>
        public ItemStack[] Harvest(World world, int x, int y, int z)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!TryParseStageBlock(world.GetBlock(x, y, z), out CropKind crop, out int stage)
                || stage != MatureStage)
            {
                return Array.Empty<ItemStack>();
            }

            // 每条掉落线独立掷骰：盐值区分产物/种子，同 seed 同坐标 replay 一致
            int productCount = BlockDrops.RollCount(
                HashPosition(_seed, x, y, z, (int)crop * 2 + 101), min: 1, max: 2);
            int seedMin = crop == CropKind.Wheat ? 0 : 1; // 甜菜/绿豆种子只从收获来，必掉保循环
            int seedCount = BlockDrops.RollCount(
                HashPosition(_seed, x, y, z, (int)crop * 2 + 102), min: seedMin, max: 2);

            world.SetBlock(x, y, z, BlockIds.Air);
            _crops.Remove(Key(x, y, z));

            var drops = new List<ItemStack>(2);
            if (productCount > 0)
            {
                drops.Add(new ItemStack(_items.GetById(ProductItemId(crop)).NumericId, productCount));
            }
            if (seedCount > 0)
            {
                drops.Add(new ItemStack(_items.GetById(SeedItemId(crop)).NumericId, seedCount));
            }
            return drops.ToArray();
        }

        // ─── 生长 ─────────────────────────────────────────────────────

        /// <summary>作物脚下是否湿耕地（生长加速判定）。</summary>
        public bool IsWetBelow(World world, int x, int y, int z)
            => world.GetBlock(x, y - 1, z) == FarmlandWetId;

        /// <summary>
        /// 单级生长时长（tick）= 基准 + [0, 基准/4] 世界坐标哈希抖动；湿耕地再 ÷ <see cref="WetGrowthSpeedup"/>。
        /// 纯函数：同 seed 同参数必得同值。
        /// </summary>
        public float StageDurationTicks(CropKind crop, int x, int y, int z, int stage, bool wetFarmland)
        {
            int baseTicks = BaseStageTicks(crop);
            int jitter = GrowthJitter(_seed, x, y, z, crop, stage);
            float ticks = baseTicks + jitter;
            return wetFarmland ? ticks / WetGrowthSpeedup : ticks;
        }

        /// <summary>
        /// 生长推进。每帧调用：先按世界方块同步/清理状态（作物被挖、耕地被拆），
        /// 再对每株做「绝对时间 ≥ 阈值」的步进（可连跳多级，粒度无关）。
        /// </summary>
        public void Tick(World world, float deltaTicks)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            TotalTicks += deltaTicks;
            if (_crops.Count == 0) return;

            _scratchKeys.Clear();
            foreach (KeyValuePair<string, CropState> pair in _crops)
            {
                _scratchKeys.Add(pair.Key);
            }

            foreach (string key in _scratchKeys)
            {
                if (!_crops.TryGetValue(key, out CropState state)) continue;

                if (!CropPosition.TryParse(key, out int x, out int y, out int z))
                {
                    _scratchRemoved.Add(key); // 不可能：自家导出的键必然合法，防御式清理
                    continue;
                }

                ushort block = world.GetBlock(x, y, z);
                if (!TryParseStageBlock(block, out CropKind worldCrop, out int worldStage)
                    || worldCrop != state.Crop)
                {
                    // 方块被外部换成非本作物（挖掉/替换）→ 以世界为准清状态
                    _scratchRemoved.Add(key);
                    continue;
                }

                ushort below = world.GetBlock(x, y - 1, z);
                if (below != FarmlandId && below != FarmlandWetId)
                {
                    // 脚下耕地没了 → 作物弹掉（照 MC：耕地被拆作物破坏）
                    world.SetBlock(x, y, z, BlockIds.Air);
                    _scratchRemoved.Add(key);
                    continue;
                }

                // 世界阶段与本系统状态失配（例如读档后方块先恢复）→ 以世界为准回写状态
                if (worldStage != state.Stage)
                {
                    state.Stage = worldStage;
                    state.StageEnteredAtTicks = TotalTicks;
                }

                // 绝对时间阈值步进：一次大步进允许连跳多级（每级重新看干湿）
                while (state.Stage < MatureStage)
                {
                    bool wet = below == FarmlandWetId;
                    float needed = StageDurationTicks(state.Crop, x, y, z, state.Stage, wet);
                    if (TotalTicks - state.StageEnteredAtTicks < needed) break;

                    state.Stage++;
                    state.StageEnteredAtTicks += needed;
                    world.SetBlock(x, y, z, StageBlockId(state.Crop, state.Stage));
                }
            }

            foreach (string key in _scratchRemoved)
            {
                _crops.Remove(key);
            }
            _scratchRemoved.Clear();
        }

        // ─── 状态序列化（LevelData.FarmStates）────────────────────────

        /// <summary>查询某格的作物状态（内存态；世界方块可能已变，以 Tick 的同步为准）。</summary>
        public bool TryGetCropState(int x, int y, int z, out CropKind crop, out int stage)
        {
            if (_crops.TryGetValue(Key(x, y, z), out CropState state))
            {
                crop = state.Crop;
                stage = state.Stage;
                return true;
            }
            crop = default;
            stage = 0;
            return false;
        }

        /// <summary>导出为 LevelData.FarmStates：<c>"x,y,z" → "crop:stage"</c>。</summary>
        public Dictionary<string, string> ExportFarmStates()
        {
            var result = new Dictionary<string, string>(_crops.Count);
            foreach (KeyValuePair<string, CropState> pair in _crops)
            {
                result[pair.Key] = CropName(pair.Value.Crop) + ":" + pair.Value.Stage.ToString(CultureInfo.InvariantCulture);
            }
            return result;
        }

        /// <summary>
        /// 从存档恢复（读容忍：键/值解析失败的条目跳过，不抛——坏一条不能挡住整个读档）。
        /// 阶段计时重置为当前时刻（存档不记进度，读档后当前阶段从头计）。
        /// 值额外容忍 <c>crop:stage:extra</c> 的第三段（未来扩展位）。
        /// </summary>
        public void ImportFarmStates(Dictionary<string, string> states)
        {
            if (states == null) return;
            foreach (KeyValuePair<string, string> pair in states)
            {
                if (!CropPosition.TryParse(pair.Key, out int x, out int y, out int z)) continue;
                if (pair.Value == null) continue;

                string[] parts = pair.Value.Split(':');
                if (parts.Length < 2) continue;
                if (!TryParseCropName(parts[0], out CropKind crop)) continue;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stage)) continue;
                if (stage < 0 || stage > MatureStage) continue;

                _crops[Key(x, y, z)] = new CropState
                {
                    Crop = crop,
                    Stage = stage,
                    StageEnteredAtTicks = TotalTicks,
                };
            }
        }

        // ─── 内部工具 ─────────────────────────────────────────────────

        private void SetState(int x, int y, int z, CropKind crop, int stage)
        {
            _crops[Key(x, y, z)] = new CropState
            {
                Crop = crop,
                Stage = stage,
                StageEnteredAtTicks = TotalTicks,
            };
        }

        private static string Key(int x, int y, int z)
            => string.Concat(
                x.ToString(CultureInfo.InvariantCulture), ",",
                y.ToString(CultureInfo.InvariantCulture), ",",
                z.ToString(CultureInfo.InvariantCulture));

        private static string CropName(CropKind crop)
        {
            switch (crop)
            {
                case CropKind.Wheat: return "wheat";
                case CropKind.Beet: return "beet";
                case CropKind.Mung: return "mung";
                default: throw new ArgumentOutOfRangeException(nameof(crop), crop, "未知作物");
            }
        }

        private static bool TryParseCropName(string name, out CropKind crop)
        {
            switch (name)
            {
                case "wheat": crop = CropKind.Wheat; return true;
                case "beet": crop = CropKind.Beet; return true;
                case "mung": crop = CropKind.Mung; return true;
                default: crop = default; return false;
            }
        }

        /// <summary>生长抖动：[0, 基准/4] 的确定性整数哈希（世界坐标 + seed + 作物 + 阶段派生）。</summary>
        private static int GrowthJitter(int seed, int x, int y, int z, CropKind crop, int stage)
        {
            int span = BaseStageTicks(crop) / 4 + 1;
            int h = HashPosition(seed, x, y, z, (int)crop * 8 + stage);
            return h % span;
        }

        /// <summary>
        /// 世界坐标整数哈希（与 SaplingGrowth.Hash2D / ValueNoise2D 同款风格，跨机器一致）。
        /// extra 用来给「同格子的不同用途」（不同阶段 / 产物 vs 种子）派生独立通道。
        /// </summary>
        private static int HashPosition(int seed, int x, int y, int z, int extra)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ x;
                h = (h * 397) ^ y;
                h = (h * 397) ^ z;
                h = (h * 397) ^ extra;
                h ^= h >> 13;
                h *= 0x5BD1E995;
                h ^= h >> 15;
                return h & 0x7FFFFFFF;
            }
        }

        /// <summary>"x,y,z" 键解析（InvariantCulture，负坐标安全——只按 ',' 切分，不做除法取模）。</summary>
        private static class CropPosition
        {
            public static bool TryParse(string key, out int x, out int y, out int z)
            {
                x = y = z = 0;
                if (string.IsNullOrEmpty(key)) return false;

                string[] parts = key.Split(',');
                if (parts.Length != 3) return false;

                return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
                    && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out z);
            }
        }
    }
}
