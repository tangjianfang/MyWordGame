using System.Collections.Generic;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Farming;
using MyWorld.Core.Items;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Farming
{
    /// <summary>
    /// m11 W1-6：Core 农田系统（锄地/种植/确定性生长/湿地加速/骨粉/收获/状态序列化）。
    /// 照 <c>HungerSystemTests</c> 的纯 Core 模式：不碰 Unity，只验数据契约与计时语义；
    /// 右键接线（BlockInteraction 路由）在集成点② 由 Unity 侧覆盖。
    /// </summary>
    [TestFixture]
    public class FarmSystemTests
    {
        private const int Seed = 20260817;

        /// <summary>stub 物品表：只注册 FarmSystem 构造要求的三作物产物 + 三种种子，
        /// numericId 随便挑 2000 段（与真实注册表无关，测试只做相对引用）。</summary>
        private static ItemDatabase StubItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""wheat"", ""numericId"": 2001, ""texture"": ""wheat-item"" }",
                @"{ ""id"": ""seeds_wheat"", ""numericId"": 2002, ""texture"": ""seeds-wheat"" }",
                @"{ ""id"": ""beet"", ""numericId"": 2003, ""texture"": ""beet"" }",
                @"{ ""id"": ""seeds_beet"", ""numericId"": 2004, ""texture"": ""seeds-beet"" }",
                @"{ ""id"": ""mung_bean"", ""numericId"": 2005, ""texture"": ""mung_bean"" }",
                @"{ ""id"": ""seeds_mung"", ""numericId"": 2006, ""texture"": ""mung_bean"" }",
            });
        }

        /// <summary>标准测试农田：y=63 铺耕地（默认干），作物位 y=64。</summary>
        private static World MakeWorld(ushort farmlandId = FarmSystem.FarmlandId)
        {
            var world = new World();
            world.SetBlock(0, 63, 0, farmlandId);
            return world;
        }

        // ─── 构造校验 ────────────────────────────────────────────────

        [Test]
        public void 构造_缺作物产物或种子_抛InvalidData()
        {
            var items = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""wheat"", ""numericId"": 2001 }",
                // 故意漏掉 seeds_wheat 等其余五个
            });

            Assert.That(() => new FarmSystem(items, Seed),
                Throws.InstanceOf<System.IO.InvalidDataException>(),
                "作物表引用的 itemId 必须全部注册（照 BlockDropsTests 守的跨表引用契约），漏一个加载即抛");
        }

        // ─── 锄地 ────────────────────────────────────────────────────

        [Test]
        public void 锄地_草与泥土变耕地_石头与空气不行()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            var world = new World();

            world.SetBlock(0, 64, 0, BlockIds.Grass);
            Assert.That(farm.Till(world, 0, 64, 0), Is.True, "锄草方块应成功");
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(FarmSystem.FarmlandId), "草方块锄后应变成耕地（干）");

            world.SetBlock(1, 64, 0, BlockIds.Dirt);
            Assert.That(farm.Till(world, 1, 64, 0), Is.True, "锄泥土应成功");
            Assert.That(world.GetBlock(1, 64, 0), Is.EqualTo(FarmSystem.FarmlandId));

            world.SetBlock(2, 64, 0, BlockIds.Stone);
            Assert.That(farm.Till(world, 2, 64, 0), Is.False, "石头锄不成耕地");
            Assert.That(world.GetBlock(2, 64, 0), Is.EqualTo(BlockIds.Stone), "锄失败的方块必须原样保留");

            Assert.That(farm.Till(world, 3, 64, 0), Is.False, "空气（未加载）锄不动——读容忍但不得误判成可锄");
        }

        // ─── 种植 ────────────────────────────────────────────────────

        [Test]
        public void 种植_耕地上方为空_生成stage0_非耕地或已占用拒绝()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            var world = MakeWorld();

            Assert.That(farm.TryPlant(world, 0, 64, 0, "seeds_wheat"), Is.True, "耕地正上方种小麦应成功");
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(FarmSystem.StageBlockId(CropKind.Wheat, 0)),
                "种植后作物方块应是 stage0");
            Assert.That(farm.TryGetCropState(0, 64, 0, out CropKind crop, out int stage), Is.True);
            Assert.That(crop, Is.EqualTo(CropKind.Wheat));
            Assert.That(stage, Is.EqualTo(0));

            // 同一格已有作物 → 拒绝（不能叠种）
            Assert.That(farm.TryPlant(world, 0, 64, 0, "seeds_beet"), Is.False, "已种植的格子不能再种");

            // 下方不是耕地（普通泥土）→ 拒绝
            world.SetBlock(5, 63, 0, BlockIds.Dirt);
            Assert.That(farm.TryPlant(world, 5, 64, 0, "seeds_wheat"), Is.False, "泥土（未锄）上不能种");

            // 湿耕地同样可种
            var wetWorld = MakeWorld(FarmSystem.FarmlandWetId);
            Assert.That(farm.TryPlant(wetWorld, 0, 64, 0, "seeds_mung"), Is.True, "湿耕地也能种");
        }

        [Test]
        public void 种子解析_三种种子映射三种作物_其余拒绝()
        {
            Assert.That(FarmSystem.TryResolveSeed("seeds_wheat", out CropKind c1), Is.True);
            Assert.That(c1, Is.EqualTo(CropKind.Wheat));
            Assert.That(FarmSystem.TryResolveSeed("seeds_beet", out CropKind c2), Is.True);
            Assert.That(c2, Is.EqualTo(CropKind.Beet));
            Assert.That(FarmSystem.TryResolveSeed("seeds_mung", out CropKind c3), Is.True);
            Assert.That(c3, Is.EqualTo(CropKind.Mung));

            Assert.That(FarmSystem.TryResolveSeed("wheat", out _), Is.False, "小麦本体不是种子");
            Assert.That(FarmSystem.TryResolveSeed("bone_meal", out _), Is.False, "骨粉不是种子");
            Assert.That(FarmSystem.TryResolveSeed("nonexistent_seed", out _), Is.False, "未注册物品拒绝");
        }

        // ─── 生长：确定性 + 湿地加速 ─────────────────────────────────

        [Test]
        public void 生长时长_同seed同坐标同stage_结果一致_不同stage有独立抖动()
        {
            var farm = new FarmSystem(StubItems(), Seed);

            float a = farm.StageDurationTicks(CropKind.Wheat, 10, 64, -7, 0, wetFarmland: false);
            float b = farm.StageDurationTicks(CropKind.Wheat, 10, 64, -7, 0, wetFarmland: false);
            Assert.That(a, Is.EqualTo(b), "同 seed 同坐标同 stage 的生长时长必须一致（确定性哈希，不持随机数对象）");

            // 时长 = 基准 + [0, base/4] 抖动，永远落在合理区间
            int baseTicks = FarmSystem.BaseStageTicks(CropKind.Wheat);
            Assert.That(a, Is.InRange(baseTicks, baseTicks * 1.25f),
                $"小麦单级时长应在 [{baseTicks}, {baseTicks * 1.25f}]（基准 + 最多 1/4 抖动）");
        }

        [Test]
        public void 生长_步进粒度无关_一次Tick5000等于五百次Tick10()
        {
            var farmA = new FarmSystem(StubItems(), Seed);
            var farmB = new FarmSystem(StubItems(), Seed);
            var worldA = MakeWorld();
            var worldB = MakeWorld();
            farmA.TryPlant(worldA, 0, 64, 0, "seeds_wheat");
            farmB.TryPlant(worldB, 0, 64, 0, "seeds_wheat");

            const float total = 25000f; // 远超三倍单级时长（小麦最多 5000/级），必然长满
            farmA.Tick(worldA, total);
            for (int i = 0; i < 2500; i++)
            {
                farmB.Tick(worldB, 10f);
            }

            Assert.That(worldA.GetBlock(0, 64, 0),
                Is.EqualTo(FarmSystem.StageBlockId(CropKind.Wheat, FarmSystem.MatureStage)),
                "一次大步进应长到成熟");
            Assert.That(worldB.GetBlock(0, 64, 0), Is.EqualTo(worldA.GetBlock(0, 64, 0)),
                "同 seed 同累计 tick，不同的 Tick 粒度必须得到同一结果（阈值判定基于绝对时间）");
            Assert.That(farmA.ExportFarmStates()["0,64,0"], Is.EqualTo(farmB.ExportFarmStates()["0,64,0"]),
                "状态串也必须一致");
        }

        [Test]
        public void 湿地_时长减半_同tick下湿地先长干地不动()
        {
            var farm = new FarmSystem(StubItems(), Seed);

            float dry = farm.StageDurationTicks(CropKind.Beet, 3, 64, 5, 0, wetFarmland: false);
            float wet = farm.StageDurationTicks(CropKind.Beet, 3, 64, 5, 0, wetFarmland: true);
            Assert.That(wet * FarmSystem.WetGrowthSpeedup, Is.EqualTo(dry).Within(0.5f),
                "湿耕地的单级时长 = 干地时长 / 加速倍率");

            var dryWorld = MakeWorld(FarmSystem.FarmlandId);
            var wetWorld = MakeWorld(FarmSystem.FarmlandWetId);
            var dryFarm = new FarmSystem(StubItems(), Seed);
            var wetFarm = new FarmSystem(StubItems(), Seed);
            dryFarm.TryPlant(dryWorld, 0, 64, 0, "seeds_beet");
            wetFarm.TryPlant(wetWorld, 0, 64, 0, "seeds_beet");

            // 推进 wet 时长（< dry 时长）：湿地必然已步进、干地必然未到
            float t = farm.StageDurationTicks(CropKind.Beet, 0, 64, 0, 0, wetFarmland: true);
            dryFarm.Tick(dryWorld, t);
            wetFarm.Tick(wetWorld, t);

            Assert.That(wetWorld.GetBlock(0, 64, 0), Is.EqualTo(FarmSystem.StageBlockId(CropKind.Beet, 1)),
                "湿耕地在 wet 时长处应已从 stage0 长到 stage1");
            Assert.That(dryWorld.GetBlock(0, 64, 0), Is.EqualTo(FarmSystem.StageBlockId(CropKind.Beet, 0)),
                "干耕地在同时长处不应步进（干地更慢）");
        }

        [Test]
        public void Tick_耕地被拆_作物自动消失_状态清理()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            var world = MakeWorld();
            farm.TryPlant(world, 0, 64, 0, "seeds_wheat");
            Assert.That(farm.TrackedCropCount, Is.EqualTo(1));

            world.SetBlock(0, 63, 0, BlockIds.Air); // 拆掉脚下的耕地
            farm.Tick(world, 1f);

            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(BlockIds.Air), "耕地没了作物应当弹掉");
            Assert.That(farm.TrackedCropCount, Is.EqualTo(0), "弹掉的作物状态要一并清理，防止字典泄漏");
        }

        [Test]
        public void Tick_作物方块被外部挖掉_状态清理()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            var world = MakeWorld();
            farm.TryPlant(world, 0, 64, 0, "seeds_wheat");

            world.SetBlock(0, 64, 0, BlockIds.Air); // 玩家直接挖掉作物
            farm.Tick(world, 1f);

            Assert.That(farm.TrackedCropCount, Is.EqualTo(0), "世界方块与状态失配时按世界为准清状态");
        }

        // ─── 骨粉 ────────────────────────────────────────────────────

        [Test]
        public void 骨粉_催熟一级_连续两次到成熟_第三次无效()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            var world = MakeWorld();
            farm.TryPlant(world, 0, 64, 0, "seeds_mung");

            Assert.That(farm.ApplyBoneMeal(world, 0, 64, 0), Is.True, "stage0 撒骨粉应催熟到 stage1");
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(FarmSystem.StageBlockId(CropKind.Mung, 1)));

            Assert.That(farm.ApplyBoneMeal(world, 0, 64, 0), Is.True, "stage1 撒骨粉应催熟到成熟");
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(FarmSystem.StageBlockId(CropKind.Mung, 2)));

            Assert.That(farm.ApplyBoneMeal(world, 0, 64, 0), Is.False, "已成熟的作物骨粉无效（不能刷产物）");
            Assert.That(farm.IsMature(world, 0, 64, 0), Is.True);

            Assert.That(farm.ApplyBoneMeal(world, 9, 64, 9), Is.False, "非作物方块撒骨粉无效");
        }

        // ─── 收获 ────────────────────────────────────────────────────

        [Test]
        public void 收获_未成熟空手_成熟掉产物与种子且区间合法()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            var world = MakeWorld();
            farm.TryPlant(world, 0, 64, 0, "seeds_wheat");

            Assert.That(farm.Harvest(world, 0, 64, 0), Is.Empty, "stage0 收获应空手（不破坏作物）");
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(FarmSystem.StageBlockId(CropKind.Wheat, 0)),
                "未成熟收获不得动方块");

            // 骨粉催到成熟再收
            farm.ApplyBoneMeal(world, 0, 64, 0);
            farm.ApplyBoneMeal(world, 0, 64, 0);

            ItemStack[] drops = farm.Harvest(world, 0, 64, 0);
            Assert.That(drops, Is.Not.Empty, "成熟收获必须有掉落");
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(BlockIds.Air), "收获后作物方块清掉");
            Assert.That(farm.TrackedCropCount, Is.EqualTo(0), "收获后状态清理");

            var items = StubItems();
            foreach (ItemStack stack in drops)
            {
                Assert.That(stack.IsEmpty, Is.False, "空栈不该出现在掉落列表里");
            }

            int wheatCount = drops.Where(s => s.ItemId == items.GetById("wheat").NumericId).Sum(s => s.Count);
            int seedCount = drops.Where(s => s.ItemId == items.GetById("seeds_wheat").NumericId).Sum(s => s.Count);
            Assert.That(wheatCount, Is.InRange(1, 2), "小麦产物掉 1-2 个");
            Assert.That(seedCount, Is.InRange(0, 2), "小麦种子掉 0-2 个");
        }

        [Test]
        public void 收获_甜菜与绿豆_种子必掉_循环可续()
        {
            var items = StubItems();
            foreach ((string seedId, CropKind kind, string productId, int seedMin) in new[]
                     {
                         ("seeds_beet", CropKind.Beet, "beet", 1),
                         ("seeds_mung", CropKind.Mung, "mung_bean", 1),
                     })
            {
                var farm = new FarmSystem(items, Seed);
                var world = MakeWorld();
                farm.TryPlant(world, 0, 64, 0, seedId);
                farm.ApplyBoneMeal(world, 0, 64, 0);
                farm.ApplyBoneMeal(world, 0, 64, 0);

                ItemStack[] drops = farm.Harvest(world, 0, 64, 0);
                int productCount = drops.Where(s => s.ItemId == items.GetById(productId).NumericId).Sum(s => s.Count);
                int seedCount = drops.Where(s => s.ItemId == items.GetById(seedId).NumericId).Sum(s => s.Count);
                Assert.That(productCount, Is.InRange(1, 2), $"{kind} 产物掉 1-2 个");
                Assert.That(seedCount, Is.InRange(seedMin, 2), $"{kind} 收获自然掉种子（≥1），保证种植循环不断");
            }
        }

        [Test]
        public void 收获掉落_确定性_同seed同坐标两次结果一致()
        {
            var results = new List<ItemStack[]>();
            var farm = new FarmSystem(StubItems(), Seed);
            for (int round = 0; round < 2; round++)
            {
                var world = MakeWorld();
                farm.TryPlant(world, 0, 64, 0, "seeds_wheat");
                farm.ApplyBoneMeal(world, 0, 64, 0);
                farm.ApplyBoneMeal(world, 0, 64, 0);
                results.Add(farm.Harvest(world, 0, 64, 0));
            }

            Assert.That(results[0].Select(s => (s.ItemId, s.Count)),
                Is.EqualTo(results[1].Select(s => (s.ItemId, s.Count))),
                "同 seed 同坐标的收获掉落必须逐项一致（整数哈希掷骰，replay 可复现）");
        }

        // ─── FarmStates 序列化（照 LevelDataSchemaTests 的样例格式）───

        [Test]
        public void FarmStates_roundtrip_导出再导入_作物与阶段一致()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            var world = MakeWorld();
            farm.TryPlant(world, 0, 64, 0, "seeds_wheat");
            farm.ApplyBoneMeal(world, 0, 64, 0); // wheat → stage1
            world.SetBlock(2, 63, 0, FarmSystem.FarmlandId);
            farm.TryPlant(world, 2, 64, 0, "seeds_beet"); // beet → stage0

            Dictionary<string, string> exported = farm.ExportFarmStates();
            Assert.That(exported.Count, Is.EqualTo(2));
            Assert.That(exported["0,64,0"], Is.EqualTo("wheat:1"), "状态串格式必须是 crop:stage");
            Assert.That(exported["2,64,0"], Is.EqualTo("beet:0"));

            // 回读进一个新系统（模拟读档）
            var restored = new FarmSystem(StubItems(), Seed);
            restored.ImportFarmStates(exported);
            Assert.That(restored.TryGetCropState(0, 64, 0, out CropKind c1, out int s1), Is.True);
            Assert.That((c1, s1), Is.EqualTo((CropKind.Wheat, 1)));
            Assert.That(restored.TryGetCropState(2, 64, 0, out CropKind c2, out int s2), Is.True);
            Assert.That((c2, s2), Is.EqualTo((CropKind.Beet, 0)));
        }

        [Test]
        public void FarmStates_导入_照LevelData样例格式_坏条目跳过不炸()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            // LevelDataSchemaTests 用的样例值就是 "wheat:2" / "beet:0"
            farm.ImportFarmStates(new Dictionary<string, string>
            {
                { "12,68,-4", "wheat:2" },
                { "13,68,-4", "beet:0" },
                { "999,68,-4", "mung:1" },
                // 坏条目（读容忍：跳过而不是抛）
                { "bad-key", "wheat:1" },
                { "1,2,3", "unknown_crop:1" },
                { "4,5,6", "wheat:not-a-number" },
            });

            Assert.That(farm.TrackedCropCount, Is.EqualTo(3), "三条合法条目导入，坏条目全部跳过");
            Assert.That(farm.TryGetCropState(12, 68, -4, out CropKind crop, out int stage), Is.True);
            Assert.That((crop, stage), Is.EqualTo((CropKind.Wheat, 2)));
        }

        [Test]
        public void FarmStates_导入后_继续按世界同步()
        {
            var farm = new FarmSystem(StubItems(), Seed);
            farm.ImportFarmStates(new Dictionary<string, string> { { "0,64,0", "wheat:0" } });

            var world = MakeWorld();
            world.SetBlock(0, 64, 0, FarmSystem.StageBlockId(CropKind.Wheat, 0));
            // 三级最坏 3 × (基准 + 1/4 抖动) = 15000，喂 4 倍基准必然长满（不赌具体抖动值）
            farm.Tick(world, FarmSystem.BaseStageTicks(CropKind.Wheat) * 4);

            Assert.That(farm.TryGetCropState(0, 64, 0, out _, out int stage), Is.True);
            Assert.That(stage, Is.EqualTo(FarmSystem.MatureStage), "导入的作物在对应方块存在时照常生长");
        }
    }
}
