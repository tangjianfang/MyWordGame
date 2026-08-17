using System;
using System.Collections.Generic;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 村庄里的一栋小楼（m11 W3-2）。纯数据：世界坐标 + 楼基高度 + 门朝向旋转，
    /// 由 <see cref="VillageFeature.TryPlanVillage"/> 确定性派生，可跨区块独立盖章。
    /// </summary>
    public sealed class VillageBuilding
    {
        /// <summary>5×5 足印的最小角（x/z 都取小的一侧）世界坐标。</summary>
        public int OriginX;

        public int OriginZ;

        /// <summary>楼基 y：找平后的地板层——BaseY 这层铺 planks 地板，墙体从 BaseY+1 起、共 4 层。</summary>
        public int BaseY;

        /// <summary>门朝向旋转（0=门朝 +Z / 1=门朝 -X / 2=门朝 -Z / 3=门朝 +X），绕 Y 轴 90° 步进。
        /// 由「门面向村中心（水井）」的主轴定向确定性推出，无随机成分。</summary>
        public int Rotation;

        /// <summary>足印中心列（世界坐标）。</summary>
        public int CenterX => OriginX + 2;

        public int CenterZ => OriginZ + 2;

        /// <summary>
        /// 世界坐标反查：落在楼体盒（x/z ∈ 足印，y ∈ [BaseY+1, BaseY+4]）内返回蓝图方块，
        /// 否则返回 null。盖章与「蓝图逐格完整」测试共用这一条路径——生成的每个方块
        /// 都必须与本方法一致，蓝图永远不需要第二份实现。
        /// </summary>
        public ushort? BlockAtWorld(int worldX, int worldY, int worldZ)
        {
            int ox = worldX - OriginX;
            int oz = worldZ - OriginZ;
            int layer = worldY - BaseY - 1;
            if (ox < 0 || ox >= VillageFeature.Footprint
                       || oz < 0 || oz >= VillageFeature.Footprint
                       || layer < 0 || layer >= VillageFeature.StructureHeight)
            {
                return null;
            }

            VillageFeature.UnrotateOffset(Rotation, ox, oz, out int dx, out int dz);
            return VillageFeature.LocalBlock(layer, dx, dz);
        }
    }

    /// <summary>
    /// 一个村庄的完整计划（m11 W3-2）：中心水井 + 3-5 栋小楼。纯数据，可序列化成
    /// 「同 (seed, 村格) 恒定」的形状——跨区块生成时每个区块各自重新派生出同一份计划，
    /// 只把落在本区块内的方块写进自己的 <see cref="ChunkColumn"/>。
    /// </summary>
    public sealed class VillagePlan
    {
        /// <summary>村中心（水井中心）世界坐标。</summary>
        public int CenterX;

        public int CenterZ;

        /// <summary>井口地表 y（整村找平的锚点，井 3×3 守卫通过后即定值）。</summary>
        public int WellY;

        /// <summary>楼群（守卫后 1-5 栋；纯平地形委托下恒为 3-5 栋）。</summary>
        public VillageBuilding[] Buildings;

        /// <summary>结构包围盒（全部楼足印 + 井 3×3），树/花草让位与 Preview 目检用。</summary>
        public int MinX;

        public int MinZ;

        public int MaxX;

        public int MaxZ;

        /// <summary>(worldX, worldZ) 是否落在村庄结构（或其外扩 <paramref name="margin"/> 圈）内。
        /// WorldGenerator 阶段 3 用它让树/花草通道绕开楼址。</summary>
        public bool HasStructureAt(int worldX, int worldZ, int margin)
        {
            return worldX >= MinX - margin && worldX <= MaxX + margin
                && worldZ >= MinZ - margin && worldZ <= MaxZ + margin;
        }

        /// <summary>由井 3×3 与全部楼足印推出包围盒（构造末尾调用一次）。</summary>
        public void ComputeBounds()
        {
            MinX = CenterX - 1;
            MaxX = CenterX + 1;
            MinZ = CenterZ - 1;
            MaxZ = CenterZ + 1;
            foreach (VillageBuilding building in Buildings)
            {
                if (building.OriginX < MinX) MinX = building.OriginX;
                if (building.OriginZ < MinZ) MinZ = building.OriginZ;
                if (building.OriginX + VillageFeature.Footprint - 1 > MaxX)
                {
                    MaxX = building.OriginX + VillageFeature.Footprint - 1;
                }
                if (building.OriginZ + VillageFeature.Footprint - 1 > MaxZ)
                {
                    MaxZ = building.OriginZ + VillageFeature.Footprint - 1;
                }
            }
        }
    }

    /// <summary>
    /// 平原/森林低密度确定性村庄（m11 W3-2，W3-2 任务卡）。
    ///
    /// 与 <see cref="TreeFeature"/> / <see cref="OreFeature"/> 同一模式：纯静态、
    /// 世界坐标整数哈希、不持随机数对象——同 (seed, 村格) 恒得同村，与生成顺序无关，
    /// 可并行调用。跨区块一致性靠「每区块重派生同一份计划、只写自己格内的方块」保证。
    ///
    /// 密度骨架：世界按 <see cref="CellSize"/>（400×400）划分村格；命中的村格
    /// （哈希 PresencePercent 概率）在离格边界 ≥ <see cref="CenterJitterMin"/> 的
    /// 内部抖出中心。中心抖动下限 64 &gt; 村最大半径（楼槽位最远 20+2+足印 5 ≈ 27），
    /// 所以村庄永不越出所属村格——一个区块只需查询覆盖它自己 16×16 方块的 ≤4 个村格。
    ///
    /// 守卫（楼址逐栋判定，不过守卫的楼整栋弃掉，村保留其余楼）：
    ///   · 不穿水：足印 25 列地表全部高出海平面 ≥1；
    ///   · 悬崖：足印内最大高差 ≤ <see cref="MaxFootprintSlope"/>，楼基相对井口落差 ≤ <see cref="MaxVillageDrop"/>；
    ///   · 井 3×3 同样要干地且高差 ≤ <see cref="MaxWellSlope"/>，井守卫不过则整村弃掉。
    /// 群系门槛（平原/森林）依赖气候噪声，留在 <see cref="WorldGenerator"/> 侧判定
    /// （<see cref="WorldGenerator.TryPlanVillageInCell"/>），本类保持无噪声的纯哈希形态。
    ///
    /// 蓝图材料：墙体 planks（1000）、角柱 log（复用 <see cref="TreeFeature.LogId"/>）、
    /// 窗 glass（1024）、门洞 2 格高空气。任务卡原文写的 cobblestone 方块在仓库中
    /// 不存在（blocks/*.json 无此条目，art 需求也无），按「实现优先于文档」以
    /// planks 墙 + stone 井圈替代，勘误随验收汇报回写。
    /// </summary>
    public static class VillageFeature
    {
        /// <summary>村格边长：平均每 400×400 格最多一个村中心（实际密度 = PresencePercent × 群系/守卫通过率）。</summary>
        public const int CellSize = 400;

        /// <summary>村中心离村格边界的最小抖动距离。必须大于村最大半径（守卫测试锁定），
        /// 村庄才不会越出所属村格、区块查询才会完整。</summary>
        public const int CenterJitterMin = 64;

        /// <summary>每个村格实际出村的哈希概率（%）。66% 是「命中概率」，最终密度还要乘
        /// 平原/森林群系占比与水/悬崖守卫通过率。</summary>
        public const int PresencePercent = 66;

        public const int MinBuildings = 3;
        public const int MaxBuildings = 5;

        /// <summary>小楼足印边长（单层 5×4×5 起步的「5」）。</summary>
        public const int Footprint = 5;

        /// <summary>楼体高度（地板之上 4 层：墙脚/窗/顶墙/屋面，「5×4×5」的「4」）。</summary>
        public const int StructureHeight = 4;

        /// <summary>村民 spawn 偏向半径（格）：村内 Villager 权重 ×3 的判定圈，
        /// Core 侧查询入口 <see cref="WorldGenerator.IsInVillageRadius"/>。</summary>
        public const int VillageRadius = 40;

        /// <summary><see cref="VillageRadius"/> 的平方（距离平方比较免开方）。</summary>
        public const int VillageRadiusSq = VillageRadius * VillageRadius;

        /// <summary>楼足印内允许的最大地表高差（找平土方量的上限）。</summary>
        public const int MaxFootprintSlope = 4;

        /// <summary>楼基相对井口允许的最大落差（防楼群挂在断崖两侧）。</summary>
        public const int MaxVillageDrop = 8;

        /// <summary>井 3×3 范围允许的最大地表高差。</summary>
        public const int MaxWellSlope = 2;

        /// <summary>墙体木板。与 <c>blocks/planks.json</c> 的 numericId=1000 手动保持一致。</summary>
        public const ushort PlanksId = 1000;

        /// <summary>窗户玻璃。与 <c>blocks/glass.json</c> 的 numericId=1024 手动保持一致。</summary>
        public const ushort GlassId = 1024;

        // 角柱原木直接复用 TreeFeature.LogId（1001，同源于 blocks/log.json），不另立常量

        // 哈希盐：出村判定 / 中心抖动 x、z / 楼数 / 楼槽位 rank 各占一路，互不干扰
        private const int SaltPresence = 0x5110A;
        private const int SaltJitterX = 0x5110B;
        private const int SaltJitterZ = 0x5110C;
        private const int SaltBuildingCount = 0x5110D;
        private const int SaltSlotRankBase = 0x51200;

        /// <summary>
        /// 小楼蓝图（数组化，rot 0 形态：门朝 +Z 即南侧）。外层 = 层号（0=墙脚层，
        /// 对应 BaseY+1），中层 = 行（z 0..4，0 为北侧），字符串下标 = 列（x 0..4）。
        /// 字符：L=角柱原木 P=墙板 G=玻璃 D=门洞(空气) .=室内(空气)。
        /// 门洞在层 0/1（BaseY+1/+2）连开两格，人形生物可通行；三面墙中点各一扇窗。
        /// </summary>
        private static readonly string[][] BlueprintLayers =
        {
            // 层 0（墙脚层）：南面正中开门
            new[] { "LPPPL", "P...P", "P...P", "P...P", "LPDPL" },
            // 层 1（窗层）：北/西/东墙中点玻璃，门洞上半继续开
            new[] { "LPGPL", "P...P", "G...G", "P...P", "LPDPL" },
            // 层 2（顶墙层）：实墙
            new[] { "LPPPL", "P...P", "P...P", "P...P", "LPPPL" },
            // 层 3（屋面层）：整层铺满 + 四角原木压边
            new[] { "LPPPL", "PPPPP", "PPPPP", "PPPPP", "LPPPL" },
        };

        /// <summary>楼槽位（相对村中心的偏移）：四角 + 四边。最远 20+2 格 + 足印 5 ⇒ 村最大半径 27 &lt; <see cref="CenterJitterMin"/>。</summary>
        private static readonly int[] SlotOffsetsX = { -16, 16, -16, 16, 0, 0, -20, 20 };
        private static readonly int[] SlotOffsetsZ = { -16, -16, 16, 16, -20, 20, 0, 0 };

        /// <summary>村格坐标（地板除，负坐标正确）。区块坐标换算仍走 <see cref="VoxelCoords"/>；
        /// 村格是 400 网格不是区块网格，等价语义在此实现。</summary>
        public static int CellOf(int world)
        {
            return world >= 0
                ? world / CellSize
                : -((CellSize - 1 - world) / CellSize);
        }

        /// <summary>
        /// 便宜的中心查询：只做出村哈希 + 中心抖动（无地表采样）。
        /// <see cref="WorldGenerator.IsInVillageRadius"/> 先用它做半径预筛，
        /// 过筛了再走完整 <see cref="TryPlanVillage"/> 校验守卫。
        /// </summary>
        public static bool TryGetVillageCenter(int cellX, int cellZ, int seed,
            out int centerX, out int centerZ)
        {
            centerX = 0;
            centerZ = 0;
            if (Hash(seed, cellX, cellZ, SaltPresence) % 100 >= PresencePercent) return false;

            int jitterRange = CellSize - 2 * CenterJitterMin;
            centerX = cellX * CellSize + CenterJitterMin
                      + (int)(Hash(seed, cellX, cellZ, SaltJitterX) % (uint)jitterRange);
            centerZ = cellZ * CellSize + CenterJitterMin
                      + (int)(Hash(seed, cellX, cellZ, SaltJitterZ) % (uint)jitterRange);
            return true;
        }

        /// <summary>
        /// 对单个村格派生完整村庄计划。surfaceHeightAt 是地表高度采样（真源
        /// <see cref="WorldGenerator.SurfaceHeightAt"/>，纯函数）；传入恒定高度委托
        /// （如 <c>(x, z) =&gt; 70</c>）即得「全守卫通过」的理想形态，测试用它锁蓝图形状。
        /// 无村（哈希未命中 / 井或全部楼被守卫拒掉）返回 null。群系门槛不在这里——见类注释。
        /// </summary>
        public static VillagePlan TryPlanVillage(int cellX, int cellZ, int seed,
            Func<int, int, int> surfaceHeightAt)
        {
            if (surfaceHeightAt == null) return null;
            if (!TryGetVillageCenter(cellX, cellZ, seed, out int centerX, out int centerZ)) return null;

            // 井守卫：3×3 全干地（高出海平面 ≥1）且高差足够小——井是整村的锚，不过则弃村
            int wellY = surfaceHeightAt(centerX, centerZ);
            int wellMin = wellY;
            int wellMax = wellY;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int h = surfaceHeightAt(centerX + dx, centerZ + dz);
                    if (h <= WorldGenerator.SeaLevel) return null;
                    if (h < wellMin) wellMin = h;
                    if (h > wellMax) wellMax = h;
                }
            }
            if (wellMax - wellMin > MaxWellSlope) return null;

            // 楼数 3-5：哈希定数量；槽位按独立哈希 rank 排序取前 N（rank 并列按下标破平，
            // Array.Sort 不稳定也无妨——结果仍由 (seed, 中心) 唯一决定）
            int buildingCount = MinBuildings
                + (int)(Hash(seed, centerX, centerZ, SaltBuildingCount) % (MaxBuildings - MinBuildings + 1));
            int[] order = new int[SlotOffsetsX.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int cmp = Hash(seed, centerX, centerZ, SaltSlotRankBase + a)
                    .CompareTo(Hash(seed, centerX, centerZ, SaltSlotRankBase + b));
                return cmp != 0 ? cmp : a - b;
            });

            var buildings = new List<VillageBuilding>(MaxBuildings);
            for (int i = 0; i < order.Length && buildings.Count < buildingCount; i++)
            {
                int slot = order[i];
                int originX = centerX + SlotOffsetsX[slot] - 2;
                int originZ = centerZ + SlotOffsetsZ[slot] - 2;
                if (TryBuildBuilding(centerX, centerZ, originX, originZ, surfaceHeightAt, wellY,
                    out VillageBuilding building))
                {
                    buildings.Add(building);
                }
            }
            if (buildings.Count == 0) return null;

            var plan = new VillagePlan
            {
                CenterX = centerX,
                CenterZ = centerZ,
                WellY = wellY,
                Buildings = buildings.ToArray(),
            };
            plan.ComputeBounds();
            return plan;
        }

        /// <summary>
        /// 把一个村庄计划盖章进区块列：只写落在 [chunkOrigin, chunkOrigin+16) 内的方块。
        /// 楼址逐列找平（低填土 / 高削方），随后蓝图 4 层显式写满（含室内与门洞的空气格），
        /// 保证楼内永远干净——阶段 3 的树/花草只被「让位」而不被删，邻近树冠若有叶子
        /// 探进楼体空气格，也会在盖章时被覆盖回空气。井在楼之后盖（井与楼足印不重叠，
        /// 顺序只影响可读性）。
        /// </summary>
        public static void StampIntoChunk(ChunkColumn column, VillagePlan plan,
            int chunkOriginX, int chunkOriginZ)
        {
            if (column == null || plan == null) return;
            for (int i = 0; i < plan.Buildings.Length; i++)
            {
                StampBuilding(column, plan.Buildings[i], chunkOriginX, chunkOriginZ);
            }
            StampWell(column, plan, chunkOriginX, chunkOriginZ);
        }

        /// <summary>蓝图局部坐标 → 方块。layer∈[0,4)（0=BaseY+1 墙脚层），dx/dz∈[0,5)。</summary>
        public static ushort LocalBlock(int layer, int dx, int dz)
        {
            char c = BlueprintLayers[layer][dz][dx];
            switch (c)
            {
                case 'L': return TreeFeature.LogId;
                case 'P': return PlanksId;
                case 'G': return GlassId;
                default: return BlockIds.Air;   // '.' 室内空气 / 'D' 门洞空气
            }
        }

        /// <summary>
        /// 足印内偏移逆旋回蓝图局部偏移（rot1↔rot3 互逆，rot0/rot2 自逆）。
        /// 正向旋转不需要单独的函数——盖章与查询共用 <see cref="VillageBuilding.BlockAtWorld"/>。
        /// </summary>
        internal static void UnrotateOffset(int rotation, int ox, int oz, out int dx, out int dz)
        {
            switch (rotation)
            {
                case 1: dx = oz; dz = 4 - ox; return;
                case 2: dx = 4 - ox; dz = 4 - oz; return;
                case 3: dx = 4 - oz; dz = ox; return;
                default: dx = ox; dz = oz; return;
            }
        }

        /// <summary>单栋楼的守卫 + 派生。守卫不过返回 false（整栋弃掉，村保留其余楼）。</summary>
        private static bool TryBuildBuilding(int wellX, int wellZ, int originX, int originZ,
            Func<int, int, int> surfaceHeightAt, int wellY, out VillageBuilding building)
        {
            building = null;

            // 不穿水 + 悬崖守卫：足印 25 列全部高出海平面 ≥1，且高差在找平能力内
            int minH = int.MaxValue;
            int maxH = int.MinValue;
            for (int dx = 0; dx < Footprint; dx++)
            {
                for (int dz = 0; dz < Footprint; dz++)
                {
                    int h = surfaceHeightAt(originX + dx, originZ + dz);
                    if (h <= WorldGenerator.SeaLevel) return false;
                    if (h < minH) minH = h;
                    if (h > maxH) maxH = h;
                }
            }
            if (maxH - minH > MaxFootprintSlope) return false;

            // 楼基取足印中心列地表；相对井口落差过大（断崖两侧）也弃
            int baseY = surfaceHeightAt(originX + 2, originZ + 2);
            if (baseY > wellY + MaxVillageDrop || baseY < wellY - MaxVillageDrop) return false;

            // 门面向村中心（井）：主轴定向。并列时 X 轴优先——纯位置推导，无随机
            int vx = wellX - (originX + 2);
            int vz = wellZ - (originZ + 2);
            int absVx = vx < 0 ? -vx : vx;
            int absVz = vz < 0 ? -vz : vz;
            int rotation;
            if (absVx >= absVz)
            {
                rotation = vx > 0 ? 3 : 1;
            }
            else
            {
                rotation = vz > 0 ? 0 : 2;
            }

            building = new VillageBuilding
            {
                OriginX = originX,
                OriginZ = originZ,
                BaseY = baseY,
                Rotation = rotation,
            };
            return true;
        }

        /// <summary>楼足印与本区块求交后逐列：找平 → 地板 → 蓝图 4 层。</summary>
        private static void StampBuilding(ChunkColumn column, VillageBuilding building,
            int chunkOriginX, int chunkOriginZ)
        {
            int minX = building.OriginX >= chunkOriginX ? building.OriginX : chunkOriginX;
            int maxX = building.OriginX + Footprint - 1;
            int chunkMaxX = chunkOriginX + VoxelCoords.ChunkSize - 1;
            if (maxX > chunkMaxX) maxX = chunkMaxX;
            if (minX > maxX) return;

            int minZ = building.OriginZ >= chunkOriginZ ? building.OriginZ : chunkOriginZ;
            int maxZ = building.OriginZ + Footprint - 1;
            int chunkMaxZ = chunkOriginZ + VoxelCoords.ChunkSize - 1;
            if (maxZ > chunkMaxZ) maxZ = chunkMaxZ;
            if (minZ > maxZ) return;

            for (int worldX = minX; worldX <= maxX; worldX++)
            {
                for (int worldZ = minZ; worldZ <= maxZ; worldZ++)
                {
                    int localX = VoxelCoords.WorldToLocal(worldX);
                    int localZ = VoxelCoords.WorldToLocal(worldZ);

                    // 找平：地表低于楼基的填土、高于楼基的削方（干地守卫已保证无水）
                    int surface = TopSolidY(column, localX, localZ);
                    for (int y = surface + 1; y < building.BaseY; y++)
                    {
                        column.SetBlock(localX, y, localZ, BlockIds.Dirt);
                    }
                    for (int y = building.BaseY + 1; y <= surface; y++)
                    {
                        column.SetBlock(localX, y, localZ, BlockIds.Air);
                    }

                    // 地板（足印整层 planks）+ 蓝图 4 层显式写满
                    column.SetBlock(localX, building.BaseY, localZ, PlanksId);
                    for (int layer = 0; layer < StructureHeight; layer++)
                    {
                        int y = building.BaseY + 1 + layer;
                        column.SetBlock(localX, y, localZ, building.BlockAtWorld(worldX, y, worldZ).Value);
                    }
                }
            }
        }

        /// <summary>井 3×3：外圈两格高石井壁（WellY/WellY+1），中心下挖两格水 + 井底石。</summary>
        private static void StampWell(ChunkColumn column, VillagePlan plan,
            int chunkOriginX, int chunkOriginZ)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int worldX = plan.CenterX + dx;
                    int worldZ = plan.CenterZ + dz;
                    if (worldX < chunkOriginX || worldX > chunkOriginX + VoxelCoords.ChunkSize - 1
                        || worldZ < chunkOriginZ || worldZ > chunkOriginZ + VoxelCoords.ChunkSize - 1)
                    {
                        continue;
                    }

                    int localX = VoxelCoords.WorldToLocal(worldX);
                    int localZ = VoxelCoords.WorldToLocal(worldZ);
                    int surface = TopSolidY(column, localX, localZ);

                    if (dx == 0 && dz == 0)
                    {
                        // 井心：削高、不足处垫土，再下挖两格水 + 一格井底石
                        for (int y = plan.WellY + 1; y <= surface; y++)
                        {
                            column.SetBlock(localX, y, localZ, BlockIds.Air);
                        }
                        for (int y = surface + 1; y < plan.WellY - 3; y++)
                        {
                            column.SetBlock(localX, y, localZ, BlockIds.Dirt);
                        }
                        column.SetBlock(localX, plan.WellY - 3, localZ, BlockIds.Stone);
                        for (int y = plan.WellY - 2; y <= plan.WellY; y++)
                        {
                            column.SetBlock(localX, y, localZ, BlockIds.Water);
                        }
                    }
                    else
                    {
                        // 井圈：找平到 WellY 后两格高石壁
                        for (int y = surface + 1; y < plan.WellY; y++)
                        {
                            column.SetBlock(localX, y, localZ, BlockIds.Dirt);
                        }
                        for (int y = plan.WellY + 2; y <= surface; y++)
                        {
                            column.SetBlock(localX, y, localZ, BlockIds.Air);
                        }
                        column.SetBlock(localX, plan.WellY, localZ, BlockIds.Stone);
                        column.SetBlock(localX, plan.WellY + 1, localZ, BlockIds.Stone);
                    }
                }
            }
        }

        /// <summary>自世界顶向下找本列最上方的非空气方块（楼址守卫保证干地，这里只会碰到地形）。</summary>
        private static int TopSolidY(ChunkColumn column, int localX, int localZ)
        {
            for (int y = VoxelCoords.MaxY - 1; y > VoxelCoords.MinY; y--)
            {
                if (column.GetBlock(localX, y, localZ) != BlockIds.Air) return y;
            }
            return VoxelCoords.MinY;
        }

        /// <summary>
        /// FNV-1a（32 位）混入 seed、村格坐标与用途盐（逐 32 位字步进），末尾接 murmur3
        /// 终结雪崩——与 <see cref="OreFeature"/> 同款哈希形态，取模低位均匀。
        /// </summary>
        private static uint Hash(int seed, int a, int b, int salt)
        {
            unchecked
            {
                const uint fnvOffsetBasis = 2166136261u;
                const uint fnvPrime = 16777619u;

                uint h = fnvOffsetBasis;
                h = (h ^ (uint)seed) * fnvPrime;
                h = (h ^ (uint)a) * fnvPrime;
                h = (h ^ (uint)b) * fnvPrime;
                h = (h ^ (uint)salt) * fnvPrime;

                h ^= h >> 16;
                h *= 0x45d9f3bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
