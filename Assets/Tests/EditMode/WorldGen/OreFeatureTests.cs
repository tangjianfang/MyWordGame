using System.Collections.Generic;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    /// <summary>
    /// m10 A2：石层嵌矿。<see cref="OreFeature.OreAt"/> 是纯函数（seed + 世界坐标 → 矿石或 Stone），
    /// 测试守住四条契约：确定性、四矿可达、地层上限、稀有度排序；最后两个是生成级集成测试，
    /// 验证 <c>WorldGenerator.FillColumn</c> 的石头分支确实走了嵌矿。
    ///
    /// 地层与稀有度约定：粗铁 y&lt;48 每 1/60、金 y&lt;32 每 1/90、合金 y&lt;24 每 1/120、机元 y&lt;16 每 1/400。
    /// 断言里写死字面量而不引用实现常量——常量改错时测试必须跟着红。
    /// </summary>
    [TestFixture]
    public class OreFeatureTests
    {
        private const int Seed = 20260806;

        /// <summary>扫描盒边长（x/z 方向）。128² × 32 层 ≈ 52 万格，1/400 的机元期望 1300+ 个，足够稳定。</summary>
        private const int ScanSize = 128;

        [Test]
        public void OreAt_SameSeedAndPosition_AlwaysReturnsSameBlock()
        {
            for (var x = -20; x <= 20; x += 7)
            for (var y = -60; y <= 60; y += 11)
            for (var z = -20; z <= 20; z += 7)
            {
                ushort first = OreFeature.OreAt(Seed, x, y, z);
                ushort second = OreFeature.OreAt(Seed, x, y, z);
                Assert.That(second, Is.EqualTo(first),
                    $"({x},{y},{z}) 同 seed 同位置两次调用结果必须一致（不持随机对象）");
            }
        }

        [Test]
        public void OreAt_DifferentSeed_ProducesDifferentResultSomewhere()
        {
            int diffs = 0;
            for (var x = 0; x < 64; x++)
            for (var y = -64; y < 16; y++)
            for (var z = 0; z < 64; z++)
            {
                if (OreFeature.OreAt(Seed, x, y, z) != OreFeature.OreAt(Seed + 1, x, y, z))
                {
                    diffs++;
                }
            }

            Assert.That(diffs, Is.GreaterThan(0), "换 seed 后嵌矿结果应当不同（seed 参与哈希）");
        }

        [Test]
        public void OreAt_LargeScan_AllFourOresAppear()
        {
            var seen = new HashSet<ushort>();
            // y ∈ [-16, 16) 覆盖全部四层地层，四种矿石都有机会出现
            for (var y = -16; y < 16; y++)
            for (var x = 0; x < ScanSize; x++)
            for (var z = 0; z < ScanSize; z++)
            {
                seen.Add(OreFeature.OreAt(Seed, x, y, z));
            }

            Assert.That(seen, Does.Contain(BlockIds.RawIronOre), "大范围扫描必须出现粗铁矿");
            Assert.That(seen, Does.Contain(BlockIds.GoldOre), "大范围扫描必须出现金矿");
            Assert.That(seen, Does.Contain(BlockIds.SummerAlloyOre), "大范围扫描必须出现合金矿");
            Assert.That(seen, Does.Contain(BlockIds.MachineEssenceOre), "大范围扫描必须出现机元矿");
        }

        [Test]
        public void OreAt_AboveStratumCeiling_OreNeverAppears()
        {
            // y ≥ 48 在全部矿层之上：无论 hash 如何都只能返回 Stone（粗铁的地层上限由此覆盖）
            for (var y = 48; y < 80; y++)
            for (var x = 0; x < ScanSize; x++)
            for (var z = 0; z < ScanSize; z++)
            {
                Assert.That(OreFeature.OreAt(Seed, x, y, z), Is.EqualTo(BlockIds.Stone),
                    $"y={y} 已在全部矿层之上，只能返回石头");
            }

            AssertOreNeverAppearsAbove(BlockIds.GoldOre, ceilingY: 32);
            AssertOreNeverAppearsAbove(BlockIds.SummerAlloyOre, ceilingY: 24);
            AssertOreNeverAppearsAbove(BlockIds.MachineEssenceOre, ceilingY: 16);
        }

        [Test]
        public void OreAt_RarityOrder_IronMostCommonEssenceRarest()
        {
            // 全地层栈计数（y ∈ [-64, 48) 一趟扫完）：稀有度序在世界尺度上成立——
            // 粗铁(1/60 × 111 层) > 金(1/90 × 95 层) ≈ 合金(1/120 × 88 层) > 机元(1/400 × 80 层)。
            // 故意不按矿种分各自地层计数：若四种矿共用同一余数类（如都用 h%N==0），
            // 倍数嵌套会让深层矿「偷走」粗铁的命中（60 的倍数一半也是 120 的倍数），
            // 实测粗铁会退化到与金一样多——这条测试就是防那种写法的。
            long iron = 0, gold = 0, alloy = 0, essence = 0;
            for (var y = VoxelCoords.MinY; y < 48; y++)
            for (var x = 0; x < ScanSize; x++)
            for (var z = 0; z < ScanSize; z++)
            {
                switch (OreFeature.OreAt(Seed, x, y, z))
                {
                    case BlockIds.RawIronOre: iron++; break;
                    case BlockIds.GoldOre: gold++; break;
                    case BlockIds.SummerAlloyOre: alloy++; break;
                    case BlockIds.MachineEssenceOre: essence++; break;
                }
            }

            Assert.That(iron, Is.GreaterThan(gold), $"全地层计数粗铁({iron})应比金({gold})多");
            Assert.That(iron, Is.GreaterThan(alloy), $"全地层计数粗铁({iron})应比合金({alloy})多");
            Assert.That(gold, Is.GreaterThan(essence), $"金({gold})应比机元({essence})常见");
            Assert.That(alloy, Is.GreaterThan(essence), $"合金({alloy})应比机元({essence})常见");

            // 粗铁应明显比金常见（密度 1/60 vs 1/90、层厚 111 vs 95 → 期望比 ≈ 1.7），
            // 下限 1.25 排除「余数嵌套偷命中」退化出的铁≈金（实测只能到 1.05）
            double ironRatio = iron / (double)gold;
            Assert.That(ironRatio, Is.InRange(1.25, 3.0),
                $"粗铁({iron})对金({gold})应有明显的稀有度差，实际比值 {ironRatio:F2}");

            // 金 ≈ 合金：期望比 ≈ (95/90)/(88/120) ≈ 1.44，容差放宽到 [0.5, 2]
            double ratio = gold / (double)alloy;
            Assert.That(ratio, Is.InRange(0.5, 2.0),
                $"金({gold})与合金({alloy})稀有度应当接近，实际比值 {ratio:F2}");
        }

        [Test]
        public void Generate_StoneLayer_ContainsOreBlocks()
        {
            // 生成级集成：区块石层里确实能扫到矿石 ID（FillColumn 接线生效）
            var generator = new WorldGenerator(Seed);
            var oreIds = new HashSet<ushort>
            {
                BlockIds.GoldOre, BlockIds.RawIronOre, BlockIds.SummerAlloyOre, BlockIds.MachineEssenceOre
            };

            var found = new HashSet<ushort>();
            ChunkColumn column = generator.Generate(new ChunkPos(0, 0));
            for (var y = VoxelCoords.MinY + 1; y < 48; y++)
            for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
            for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
            {
                if (oreIds.Contains(column.GetBlock(lx, y, lz)))
                {
                    found.Add(column.GetBlock(lx, y, lz));
                }
            }

            Assert.That(found, Is.Not.Empty, "生成区块的地下石层应当至少嵌有一种矿石");
        }

        [Test]
        public void Generate_OresRespectTheirStrata()
        {
            // 生成级集成：整根区块扫下来，每种矿石都不越过自己的地层上限
            var generator = new WorldGenerator(Seed);
            ChunkColumn column = generator.Generate(new ChunkPos(0, 0));

            for (var y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
            for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
            for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
            {
                switch (column.GetBlock(lx, y, lz))
                {
                    case BlockIds.GoldOre:
                        Assert.That(y, Is.LessThan(32), $"金矿出现在 y={y}，越过了地层上限 32");
                        break;
                    case BlockIds.RawIronOre:
                        Assert.That(y, Is.LessThan(48), $"粗铁矿出现在 y={y}，越过了地层上限 48");
                        break;
                    case BlockIds.SummerAlloyOre:
                        Assert.That(y, Is.LessThan(24), $"合金矿出现在 y={y}，越过了地层上限 24");
                        break;
                    case BlockIds.MachineEssenceOre:
                        Assert.That(y, Is.LessThan(16), $"机元矿出现在 y={y}，越过了地层上限 16");
                        break;
                }
            }
        }

        /// <summary>从 ceilingY 起再扫 16 层，断言该矿石从未越过地层上限。</summary>
        private static void AssertOreNeverAppearsAbove(ushort oreId, int ceilingY)
        {
            for (var y = ceilingY; y < ceilingY + 16; y++)
            for (var x = 0; x < ScanSize; x++)
            for (var z = 0; z < ScanSize; z++)
            {
                Assert.That(OreFeature.OreAt(Seed, x, y, z), Is.Not.EqualTo(oreId),
                    $"{oreId} 不应出现在 y={y}（地层上限 {ceilingY}）");
            }
        }
    }
}
