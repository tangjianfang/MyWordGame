using System;
using System.Collections.Generic;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    /// <summary>
    /// m11 W3-2：平原/森林低密度确定性村庄。分两层守契约：
    ///
    ///   1. 纯函数层（平地高度委托，全部守卫恒通过）：同 seed 同村、楼数 3-5、
    ///      楼址避井/互不重叠/不越村格边界、蓝图形状（门洞两格高、三扇窗、角柱贯通）；
    ///   2. 真实地形层（默认 seed 的 <see cref="WorldGenerator"/>）：密度区间与最小间距、
    ///      水与悬崖守卫、生成级蓝图逐格完整（含井）、区块级确定性、村民偏向查询半径。
    ///
    /// 断言里写死字面量（400 村格 / 66% 出村率 / 楼基上限等）而不引用实现常量——
    /// 常量改错时测试必须跟着红（与 <see cref="OreFeatureTests"/> 同款纪律）。
    /// </summary>
    [TestFixture]
    public class VillageFeatureTests
    {
        private const int Seed = 20260806;

        /// <summary>平地高度委托：全部干地（70 &gt; 海平面 62）、零坡度——守卫恒通过。</summary>
        private static int FlatSurface(int worldX, int worldZ)
        {
            return 70;
        }

        /// <summary>从 (0,0) 村格起按行扫描，返回第一个出村的 (cellX, cellZ) 与计划（平地委托）。</summary>
        private static VillagePlan FindFlatPlan(out int foundCellX, out int foundCellZ)
        {
            for (int cellZ = 0; cellZ < 40; cellZ++)
            {
                for (int cellX = 0; cellX < 40; cellX++)
                {
                    VillagePlan plan = VillageFeature.TryPlanVillage(cellX, cellZ, Seed, FlatSurface);
                    if (plan != null)
                    {
                        foundCellX = cellX;
                        foundCellZ = cellZ;
                        return plan;
                    }
                }
            }

            foundCellX = -1;
            foundCellZ = -1;
            return null;
        }

        /// <summary>从 (0,0) 村格环形外扩，返回第一个真实村庄（含平原/森林群系门槛）。</summary>
        private static VillagePlan FindRealPlan(WorldGenerator generator, out int foundCellX, out int foundCellZ)
        {
            for (int radius = 0; radius < 12; radius++)
            {
                for (int cellX = -radius; cellX <= radius; cellX++)
                {
                    for (int cellZ = -radius; cellZ <= radius; cellZ++)
                    {
                        // 只扫环带边框，避免重复扫内圈
                        if (radius > 0 && cellX != -radius && cellX != radius
                                       && cellZ != -radius && cellZ != radius)
                        {
                            continue;
                        }
                        VillagePlan plan = generator.TryPlanVillageInCell(cellX, cellZ);
                        if (plan != null)
                        {
                            foundCellX = cellX;
                            foundCellZ = cellZ;
                            return plan;
                        }
                    }
                }
            }

            foundCellX = -1;
            foundCellZ = -1;
            return null;
        }

        [Test]
        public void CellOf_负坐标地板除正确()
        {
            Assert.That(VillageFeature.CellOf(0), Is.EqualTo(0), "0 属于村格 0");
            Assert.That(VillageFeature.CellOf(399), Is.EqualTo(0), "399 是村格 0 的最后一格");
            Assert.That(VillageFeature.CellOf(400), Is.EqualTo(1), "400 是村格 1 的第一格");
            Assert.That(VillageFeature.CellOf(-1), Is.EqualTo(-1), "-1 属于村格 -1（不能向零取整成 0）");
            Assert.That(VillageFeature.CellOf(-400), Is.EqualTo(-1), "-400 是村格 -1 的最后一格");
            Assert.That(VillageFeature.CellOf(-401), Is.EqualTo(-2), "-401 属于村格 -2");
        }

        [Test]
        public void 计划_null高度委托_不派生村庄()
        {
            Assert.That(VillageFeature.TryPlanVillage(0, 0, Seed, null), Is.Null,
                "surfaceHeightAt 为 null 时必须返回 null 而不是抛异常");
        }

        [Test]
        public void 计划_同seed同村格_两次派生完全一致()
        {
            VillagePlan first = FindFlatPlan(out int cellX, out int cellZ);
            Assert.That(first, Is.Not.Null, "40×40 个村格内必然至少命中一个村（出村率 66%）");
            VillagePlan second = VillageFeature.TryPlanVillage(cellX, cellZ, Seed, FlatSurface);

            Assert.That(second, Is.Not.Null, "同 (seed, 村格) 再次派生必须同样出村");
            Assert.That(second.CenterX, Is.EqualTo(first.CenterX), "村中心两次必须一致");
            Assert.That(second.CenterZ, Is.EqualTo(first.CenterZ), "村中心两次必须一致");
            Assert.That(second.WellY, Is.EqualTo(first.WellY), "井口高度两次必须一致");
            Assert.That(second.Buildings.Length, Is.EqualTo(first.Buildings.Length), "楼数两次必须一致");
            Assert.That(second.MinX, Is.EqualTo(first.MinX), "包围盒两次必须一致");
            Assert.That(second.MaxZ, Is.EqualTo(first.MaxZ), "包围盒两次必须一致");
            for (int i = 0; i < first.Buildings.Length; i++)
            {
                Assert.That(second.Buildings[i].OriginX, Is.EqualTo(first.Buildings[i].OriginX), $"第 {i} 楼位置两次必须一致");
                Assert.That(second.Buildings[i].OriginZ, Is.EqualTo(first.Buildings[i].OriginZ), $"第 {i} 楼位置两次必须一致");
                Assert.That(second.Buildings[i].BaseY, Is.EqualTo(first.Buildings[i].BaseY), $"第 {i} 楼楼基两次必须一致");
                Assert.That(second.Buildings[i].Rotation, Is.EqualTo(first.Buildings[i].Rotation), $"第 {i} 楼门朝向两次必须一致");
            }
        }

        [Test]
        public void 计划_换seed_出村判定与位置随之变化()
        {
            int presenceDiffers = 0;
            int centerDiffers = 0;
            for (int cellX = 0; cellX < 12; cellX++)
            {
                for (int cellZ = 0; cellZ < 12; cellZ++)
                {
                    bool hasA = VillageFeature.TryGetVillageCenter(cellX, cellZ, Seed, out int ax, out int az);
                    bool hasB = VillageFeature.TryGetVillageCenter(cellX, cellZ, Seed + 1, out int bx, out int bz);
                    if (hasA != hasB) presenceDiffers++;
                    if (hasA && hasB && (ax != bx || az != bz)) centerDiffers++;
                }
            }

            Assert.That(presenceDiffers, Is.GreaterThan(0), "换 seed 后应有村格出村判定翻转");
            Assert.That(presenceDiffers + centerDiffers, Is.GreaterThan(10),
                "144 个村格上 seed 应实质参与哈希（出村翻转或中心移位显著）");
        }

        [Test]
        public void 计划_平地委托_楼数恒在3到5()
        {
            int villages = 0;
            for (int cellX = 0; cellX < 25; cellX++)
            {
                for (int cellZ = 0; cellZ < 25; cellZ++)
                {
                    VillagePlan plan = VillageFeature.TryPlanVillage(cellX, cellZ, Seed, FlatSurface);
                    if (plan == null) continue;
                    villages++;
                    Assert.That(plan.Buildings.Length, Is.InRange(3, 5),
                        $"村 ({plan.CenterX},{plan.CenterZ}) 楼数 {plan.Buildings.Length} 超出 3-5 区间");
                    Assert.That(plan.WellY, Is.EqualTo(70), "平地委托下井口高度应为 70");
                }
            }
            Assert.That(villages, Is.GreaterThan(0), "625 个村格（66% 出村率）应至少命中一村");
        }

        [Test]
        public void 计划_平地委托_楼址避井互不重叠且不越村格边界()
        {
            for (int cellX = 0; cellX < 25; cellX++)
            {
                for (int cellZ = 0; cellZ < 25; cellZ++)
                {
                    VillagePlan plan = VillageFeature.TryPlanVillage(cellX, cellZ, Seed, FlatSurface);
                    if (plan == null) continue;

                    // 中心必须落在所属村格内（抖动下限 64、上限 336）
                    Assert.That(VillageFeature.CellOf(plan.CenterX), Is.EqualTo(cellX), "村中心必须在所属村格内");
                    Assert.That(VillageFeature.CellOf(plan.CenterZ), Is.EqualTo(cellZ), "村中心必须在所属村格内");

                    // 村最大半径必须小于中心抖动下限——村庄永不越出村格，区块查询才完备
                    Assert.That(plan.MaxX - plan.CenterX, Is.LessThan(64), "村东界离中心必须 < 64（抖动下限）");
                    Assert.That(plan.CenterX - plan.MinX, Is.LessThan(64), "村西界离中心必须 < 64（抖动下限）");
                    Assert.That(plan.MaxZ - plan.CenterZ, Is.LessThan(64), "村北界离中心必须 < 64（抖动下限）");
                    Assert.That(plan.CenterZ - plan.MinZ, Is.LessThan(64), "村南界离中心必须 < 64（抖动下限）");

                    for (int i = 0; i < plan.Buildings.Length; i++)
                    {
                        VillageBuilding a = plan.Buildings[i];
                        // 楼足印避井 3×3
                        Assert.That(a.OriginX > plan.CenterX + 1 || a.OriginX + 4 < plan.CenterX - 1
                                 || a.OriginZ > plan.CenterZ + 1 || a.OriginZ + 4 < plan.CenterZ - 1,
                            Is.True, $"第 {i} 楼足印压到了水井 3×3");

                        // 楼两两足印不重叠
                        for (int j = i + 1; j < plan.Buildings.Length; j++)
                        {
                            VillageBuilding b = plan.Buildings[j];
                            Assert.That(a.OriginX > b.OriginX + 4 || a.OriginX + 4 < b.OriginX
                                     || a.OriginZ > b.OriginZ + 4 || a.OriginZ + 4 < b.OriginZ,
                                Is.True, $"第 {i}/{j} 楼足印重叠");
                        }
                    }
                }
            }
        }

        [Test]
        public void 蓝图_材料只用三种方块_门洞两格高_三扇窗_角柱贯通()
        {
            int glassCount = 0;
            for (int dx = 0; dx < 5; dx++)
            {
                for (int dz = 0; dz < 5; dz++)
                {
                    for (int layer = 0; layer < 4; layer++)
                    {
                        ushort block = VillageFeature.LocalBlock(layer, dx, dz);
                        Assert.That(block == BlockIds.Air || block == VillageFeature.PlanksId
                                 || block == TreeFeature.LogId || block == VillageFeature.GlassId,
                            Is.True, $"蓝图 ({layer},{dx},{dz}) 出现了第四种方块 {block}——只许木板/原木/玻璃/空气");

                        if (block == VillageFeature.GlassId) glassCount++;
                    }
                }
            }
            Assert.That(glassCount, Is.EqualTo(3), "蓝图应有且仅有三扇窗（门那面墙不开窗）");

            // 门洞：层 0/1 在南墙正中 (2,4) 连开两格，且是这两层墙上仅有的空气格
            for (int layer = 0; layer <= 1; layer++)
            {
                int airOnPerimeter = 0;
                for (int dx = 0; dx < 5; dx++)
                {
                    for (int dz = 0; dz < 5; dz++)
                    {
                        bool perimeter = dx == 0 || dx == 4 || dz == 0 || dz == 4;
                        if (!perimeter) continue;
                        if (VillageFeature.LocalBlock(layer, dx, dz) != BlockIds.Air) continue;
                        airOnPerimeter++;
                        Assert.That(dx == 2 && dz == 4, Is.True,
                            $"层 {layer} 的墙上空气格只允许出现在门洞 (2,4)，实测 ({dx},{dz})");
                    }
                }
                Assert.That(airOnPerimeter, Is.EqualTo(1), $"层 {layer} 门洞应恰有一列（当前层墙空气格 {airOnPerimeter}）");
            }

            // 角柱：四角四层全部贯通原木
            foreach ((int dx, int dz) in new[] { (0, 0), (4, 0), (0, 4), (4, 4) })
            {
                for (int layer = 0; layer < 4; layer++)
                {
                    Assert.That(VillageFeature.LocalBlock(layer, dx, dz), Is.EqualTo(TreeFeature.LogId),
                        $"角柱 ({dx},{dz}) 层 {layer} 必须是原木");
                }
            }

            // 屋面：层 3 整层无非空气实心覆盖（室内不漏雨）
            for (int dx = 0; dx < 5; dx++)
            {
                for (int dz = 0; dz < 5; dz++)
                {
                    Assert.That(VillageFeature.LocalBlock(3, dx, dz), Is.Not.EqualTo(BlockIds.Air),
                        $"屋面层 ({dx},{dz}) 不能是空气");
                }
            }
        }

        [Test]
        public void 真实地形_大窗口普查_密度区间与最小间距()
        {
            var generator = new WorldGenerator(Seed);
            var centers = new List<(int x, int z)>();
            const int cellRadius = 15;   // 31×31 村格 = 12400×12400 方块

            for (int cellX = -cellRadius; cellX <= cellRadius; cellX++)
            {
                for (int cellZ = -cellRadius; cellZ <= cellRadius; cellZ++)
                {
                    VillagePlan plan = generator.TryPlanVillageInCell(cellX, cellZ);
                    if (plan == null) continue;
                    centers.Add((plan.CenterX, plan.CenterZ));
                }
            }

            Assert.That(centers.Count, Is.GreaterThanOrEqualTo(5),
                $"31×31 村格（默认 seed）实测 {centers.Count} 村——密度过低，村庄几乎不可达");
            Assert.That(centers.Count, Is.LessThanOrEqualTo(600),
                $"实测 {centers.Count} 村——密度超标（每村格至多一村，且远不到 66% 全出）");

            // 同村格唯一 + 中心抖动下限 64 ⇒ 相邻村格中心最近 128 格
            for (int i = 0; i < centers.Count; i++)
            {
                for (int j = i + 1; j < centers.Count; j++)
                {
                    int dx = centers[i].x - centers[j].x;
                    int dz = centers[i].z - centers[j].z;
                    Assert.That(dx * dx + dz * dz, Is.GreaterThanOrEqualTo(120 * 120),
                        $"村 {centers[i]} 与 {centers[j]} 距离小于 120——中心抖动或村格唯一性被破坏");
                }
            }
        }

        [Test]
        public void 真实地形_楼群与井全部干地_坡度守卫成立()
        {
            var generator = new WorldGenerator(Seed);
            int checkedBuildings = 0;
            int checkedWells = 0;

            for (int cellX = -8; cellX <= 8; cellX++)
            {
                for (int cellZ = -8; cellZ <= 8; cellZ++)
                {
                    VillagePlan plan = generator.TryPlanVillageInCell(cellX, cellZ);
                    if (plan == null) continue;
                    checkedWells++;

                    // 井 3×3 干地 + 高差
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            Assert.That(generator.SurfaceHeightAt(plan.CenterX + dx, plan.CenterZ + dz),
                                Is.GreaterThan(WorldGenerator.SeaLevel),
                                $"井 ({plan.CenterX + dx},{plan.CenterZ + dz}) 落在水域——不穿水守卫失效");
                        }
                    }

                    foreach (VillageBuilding building in plan.Buildings)
                    {
                        checkedBuildings++;
                        int minH = int.MaxValue;
                        int maxH = int.MinValue;
                        for (int dx = 0; dx < 5; dx++)
                        {
                            for (int dz = 0; dz < 5; dz++)
                            {
                                int h = generator.SurfaceHeightAt(building.OriginX + dx, building.OriginZ + dz);
                                Assert.That(h, Is.GreaterThan(WorldGenerator.SeaLevel),
                                    $"楼 ({building.OriginX},{building.OriginZ}) 足印 ({dx},{dz}) 落在水域");
                                if (h < minH) minH = h;
                                if (h > maxH) maxH = h;
                            }
                        }
                        Assert.That(maxH - minH, Is.LessThanOrEqualTo(4),
                            $"楼 ({building.OriginX},{building.OriginZ}) 足印高差 {maxH - minH} 超过找平能力 4");
                        Assert.That(building.BaseY - plan.WellY, Is.InRange(-8, 8),
                            $"楼基 {building.BaseY} 相对井口 {plan.WellY} 落差超过 8——悬崖守卫失效");
                    }
                }
            }

            Assert.That(checkedWells, Is.GreaterThan(0), "17×17 村格内应至少出一村用于守护");
            Assert.That(checkedBuildings, Is.GreaterThan(0), "命中村庄应至少有一栋楼");
        }

        /// <summary>生成覆盖村庄包围盒的全部区块，按 (ChunkPos, ChunkColumn) 建缓存供逐格断言。</summary>
        private static Dictionary<ChunkPos, ChunkColumn> GenerateVillageChunks(WorldGenerator generator, VillagePlan plan)
        {
            var chunks = new Dictionary<ChunkPos, ChunkColumn>();
            for (int chunkX = VoxelCoords.WorldToChunk(plan.MinX); chunkX <= VoxelCoords.WorldToChunk(plan.MaxX); chunkX++)
            {
                for (int chunkZ = VoxelCoords.WorldToChunk(plan.MinZ); chunkZ <= VoxelCoords.WorldToChunk(plan.MaxZ); chunkZ++)
                {
                    var pos = new ChunkPos(chunkX, chunkZ);
                    chunks[pos] = generator.Generate(pos);
                }
            }
            return chunks;
        }

        private static ushort BlockIn(Dictionary<ChunkPos, ChunkColumn> chunks, int worldX, int worldY, int worldZ)
        {
            var pos = new ChunkPos(VoxelCoords.WorldToChunk(worldX), VoxelCoords.WorldToChunk(worldZ));
            return chunks[pos].GetBlock(VoxelCoords.WorldToLocal(worldX), worldY, VoxelCoords.WorldToLocal(worldZ));
        }

        [Test]
        public void 生成_村庄蓝图逐格完整含井()
        {
            var generator = new WorldGenerator(Seed);
            VillagePlan plan = FindRealPlan(generator, out _, out _);
            Assert.That(plan, Is.Not.Null, "环形外扩 12 圈村格内应能找到一个真实村庄");

            var chunks = GenerateVillageChunks(generator, plan);

            foreach (VillageBuilding building in plan.Buildings)
            {
                for (int dx = 0; dx < 5; dx++)
                {
                    for (int dz = 0; dz < 5; dz++)
                    {
                        int wx = building.OriginX + dx;
                        int wz = building.OriginZ + dz;

                        // 地板：楼基整层木板
                        Assert.That(BlockIn(chunks, wx, building.BaseY, wz), Is.EqualTo(VillageFeature.PlanksId),
                            $"楼 ({building.OriginX},{building.OriginZ}) 地板 ({dx},{dz}) 应为木板");

                        // 蓝图 4 层逐格：生成结果与计划反查必须一字不差（含门洞/室内空气）
                        for (int layer = 0; layer < 4; layer++)
                        {
                            int y = building.BaseY + 1 + layer;
                            ushort expected = building.BlockAtWorld(wx, y, wz).Value;
                            ushort actual = BlockIn(chunks, wx, y, wz);
                            Assert.That(actual, Is.EqualTo(expected),
                                $"楼 ({building.OriginX},{building.OriginZ}) ({dx},{layer},{dz}) 生成 {actual} 与蓝图 {expected} 不一致");
                        }
                    }
                }

                // 找平佐证：地板下方紧邻的一格要么是原地表土层、要么是找平回填土——不能是空气（悬空楼）
                for (int dx = 0; dx < 5; dx++)
                {
                    for (int dz = 0; dz < 5; dz++)
                    {
                        Assert.That(BlockIn(chunks, building.OriginX + dx, building.BaseY - 1, building.OriginZ + dz),
                            Is.Not.EqualTo(BlockIds.Air),
                            $"楼 ({building.OriginX},{building.OriginZ}) 地板 ({dx},{dz}) 下方悬空——找平回填缺失");
                    }
                }
            }

            // 井：外圈两格高石壁、井口敞开、中心两格水 + 井底石
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int wx = plan.CenterX + dx;
                    int wz = plan.CenterZ + dz;
                    if (dx == 0 && dz == 0)
                    {
                        Assert.That(BlockIn(chunks, wx, plan.WellY, wz), Is.EqualTo(BlockIds.Water), "井心水面应在井口高度");
                        Assert.That(BlockIn(chunks, wx, plan.WellY - 1, wz), Is.EqualTo(BlockIds.Water), "井心应有下挖水层");
                        Assert.That(BlockIn(chunks, wx, plan.WellY - 2, wz), Is.EqualTo(BlockIds.Water), "井心应有下挖水层");
                        Assert.That(BlockIn(chunks, wx, plan.WellY - 3, wz), Is.EqualTo(BlockIds.Stone), "井底应为石头");
                        Assert.That(BlockIn(chunks, wx, plan.WellY + 1, wz), Is.EqualTo(BlockIds.Air), "井口必须敞开");
                    }
                    else
                    {
                        Assert.That(BlockIn(chunks, wx, plan.WellY, wz), Is.EqualTo(BlockIds.Stone), "井圈底层应为石头");
                        Assert.That(BlockIn(chunks, wx, plan.WellY + 1, wz), Is.EqualTo(BlockIds.Stone), "井圈口沿应为石头");
                    }
                }
            }
        }

        [Test]
        public void 生成_同seed同区块两次生成逐格一致()
        {
            VillagePlan plan = FindRealPlan(new WorldGenerator(Seed), out _, out _);
            Assert.That(plan, Is.Not.Null, "环形外扩 12 圈村格内应能找到一个真实村庄");

            var pos = new ChunkPos(VoxelCoords.WorldToChunk(plan.CenterX), VoxelCoords.WorldToChunk(plan.CenterZ));
            ChunkColumn first = new WorldGenerator(Seed).Generate(pos);
            ChunkColumn second = new WorldGenerator(Seed).Generate(pos);

            for (int y = plan.WellY - 10; y <= plan.WellY + 15; y++)
            {
                for (int lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                {
                    for (int lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                    {
                        Assert.That(second.GetBlock(lx, y, lz), Is.EqualTo(first.GetBlock(lx, y, lz)),
                            $"({pos.X * 16 + lx},{y},{pos.Z * 16 + lz}) 同 seed 两次生成不一致——村庄阶段破坏了确定性");
                    }
                }
            }
        }

        [Test]
        public void 查询_IsInVillageRadius_村内真村外远点假()
        {
            var generator = new WorldGenerator(Seed);
            VillagePlan plan = FindRealPlan(generator, out _, out _);
            Assert.That(plan, Is.Not.Null, "环形外扩 12 圈村格内应能找到一个真实村庄");

            // 井心与半程四点必须在半径内（半径 40）
            Assert.That(generator.IsInVillageRadius(plan.CenterX, plan.CenterZ), Is.True, "村中心必须在村庄半径内");
            Assert.That(generator.IsInVillageRadius(plan.CenterX + 20, plan.CenterZ), Is.True, "村东 20 格应在半径内");
            Assert.That(generator.IsInVillageRadius(plan.CenterX - 20, plan.CenterZ), Is.True, "村西 20 格应在半径内");
            Assert.That(generator.IsInVillageRadius(plan.CenterX, plan.CenterZ + 20), Is.True, "村北 20 格应在半径内");
            Assert.That(generator.IsInVillageRadius(plan.CenterX, plan.CenterZ - 20), Is.True, "村南 20 格应在半径内");

            // 半径外 8 向探测点：村间最小间距 128 > 2×(40+8)，绝大多数方向必须为假。
            // 注意用 System.Math 全名——本命名空间下的 MyWorld.Core.Math 会遮蔽 System.Math
            int farFalse = 0;
            for (int dir = 0; dir < 8; dir++)
            {
                double angle = dir * System.Math.PI / 4;
                int px = plan.CenterX + (int)(48 * System.Math.Cos(angle));
                int pz = plan.CenterZ + (int)(48 * System.Math.Sin(angle));
                if (!generator.IsInVillageRadius(px, pz)) farFalse++;
            }
            Assert.That(farFalse, Is.GreaterThanOrEqualTo(6),
                $"8 个 48 格远探测点中 {farFalse} 个为假——半径判定过宽或有村贴脸（村间最小间距应 ≥ 128）");
        }
    }
}
