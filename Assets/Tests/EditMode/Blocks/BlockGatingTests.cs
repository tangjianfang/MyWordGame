using System;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// m10 A3：工具门槛矩阵纯函数（spec §1，孩子的「24 小时」需求修正为 MC 制门槛）。
    /// <para>
    /// <see cref="BlockGating.CanDrop"/> 是门槛判定的唯一入口：镐等级 ≥ 方块 minToolTier
    /// 才允许掉落；不够 = 挖得掉方块但无掉落（对应「木镐挖了不掉」）。
    /// <see cref="BlockGating.BreakSeconds"/> 是 (block, toolTier) → 秒 的查表规则：
    /// 达标 = hardness（spec「对应镐」时间列），不达标 ×4（徒手挖石 4s）。
    /// </para>
    /// <para>
    /// 纯 Core 无 Unity 依赖，dotnet 与 EditMode 双链同跑；真实 JSON 端到端断言
    /// （五镐 toolTier / 四矿矩阵）用仓库里的真实数据文件，与 BlockDropsTests 同模式。
    /// </para>
    /// </summary>
    [TestFixture]
    public class BlockGatingTests
    {
        // ─── CanDrop 门槛矩阵 ────────────────────────────────────────────────

        [TestCase(0, 0, true, "泥土/木头门槛 0：徒手也掉")]
        [TestCase(1, 0, false, "石头要木镐：徒手挖了不掉")]
        [TestCase(1, 1, true, "木镐挖石头：达标")]
        [TestCase(2, 1, false, "铁矿要石镐：木镐挖了不掉")]
        [TestCase(2, 2, true, "石镐挖铁矿：达标")]
        [TestCase(3, 2, false, "金矿要铁镐：石镐挖了不掉")]
        [TestCase(3, 3, true, "铁镐挖金矿：达标")]
        [TestCase(4, 3, false, "机元要钻石镐：铁镐挖了不掉")]
        [TestCase(4, 4, true, "钻石镐挖机元：达标")]
        [TestCase(4, 99, true, "基岩镐(99)是彩蛋终局镐，什么门槛都过")]
        [TestCase(3, 99, true, "更高等级永远兼容更低门槛（只比大小）")]
        public void CanDrop_Matrix(int blockMinTier, int toolTier, bool expected, string reason)
        {
            Assert.That(BlockGating.CanDrop(blockMinTier, toolTier), Is.EqualTo(expected),
                $"minToolTier={blockMinTier} vs toolTier={toolTier}：{reason}");
        }

        // ─── ResolveToolTier：空手 0 / 非镐 0 / 镐取字段 ─────────────────────

        [Test]
        public void ResolveToolTier_Null_ReturnsZero()
        {
            Assert.That(BlockGating.ResolveToolTier(null), Is.EqualTo(0),
                "空手（没有选中物品）的镐等级是 0");
        }

        [Test]
        public void ResolveToolTier_ItemWithoutToolTier_IsZero()
        {
            // 剑/斧/锹有 miningLevel（耐久档位）但没有 toolTier——不算镐，门槛按徒手算
            var sword = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""wooden_sword"", ""numericId"": 1100, ""isTool"": true, ""miningLevel"": 1 }",
            }).GetById("wooden_sword");

            Assert.That(BlockGating.ResolveToolTier(sword), Is.EqualTo(0),
                "木剑不是镐，不能帮玩家过石头的门槛");
        }

        [Test]
        public void ResolveToolTier_Pickaxe_ReadsToolTier()
        {
            var db = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""wooden_pickaxe"", ""numericId"": 1400, ""toolTier"": 1 }",
                @"{ ""id"": ""stone_pickaxe"", ""numericId"": 1401, ""toolTier"": 2 }",
                @"{ ""id"": ""iron_pickaxe"", ""numericId"": 1402, ""toolTier"": 3 }",
                @"{ ""id"": ""diamond_pickaxe"", ""numericId"": 1403, ""toolTier"": 4 }",
            });

            Assert.That(BlockGating.ResolveToolTier(db.GetById("wooden_pickaxe")), Is.EqualTo(1), "木镐 1");
            Assert.That(BlockGating.ResolveToolTier(db.GetById("stone_pickaxe")), Is.EqualTo(2), "石镐 2");
            Assert.That(BlockGating.ResolveToolTier(db.GetById("iron_pickaxe")), Is.EqualTo(3), "铁镐 3");
            Assert.That(BlockGating.ResolveToolTier(db.GetById("diamond_pickaxe")), Is.EqualTo(4), "钻镐 4");
        }

        [Test]
        public void ItemDatabase_NegativeToolTier_Throws()
        {
            // 与 BlockRegistry 对 minToolTier 的态度一致：负数没有意义，写错立刻报而不是静默当 0
            Assert.That(() => ItemDatabase.FromJson(new[]
                {
                    @"{ ""id"": ""bad_pickaxe"", ""numericId"": 1499, ""toolTier"": -1 }",
                }),
                Throws.InstanceOf<InvalidDataException>(),
                "toolTier 为负数应在加载时抛 InvalidDataException");
        }

        [Test]
        public void ItemDatabase_MissingToolTier_DefaultsToZero()
        {
            var def = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""apple"", ""numericId"": 1500 }",
            }).GetById("apple");

            Assert.That(def.ToolTier, Is.EqualTo(0),
                "不写 toolTier 的物品默认 0（视同徒手挖矿），加新物品不写字段不会意外过门槛");
        }

        // ─── BreakSeconds：(block, toolTier) → 秒 查表规则 ───────────────────

        [Test]
        public void BreakSeconds_AtOrAboveTier_ReturnsHardness()
        {
            Assert.That(BlockGating.BreakSeconds(1f, 1, 1), Is.EqualTo(1f), "木镐挖石 1s（hardness=1）");
            Assert.That(BlockGating.BreakSeconds(2f, 2, 2), Is.EqualTo(2f), "石镐挖铁 2s");
            Assert.That(BlockGating.BreakSeconds(6f, 4, 4), Is.EqualTo(6f), "钻镐挖机元 6s");
            Assert.That(BlockGating.BreakSeconds(6f, 4, 99), Is.EqualTo(6f),
                "更高镐不更快也不更慢——spec 只给了「对应镐」一列，超配不奖励");
        }

        [Test]
        public void BreakSeconds_BelowTier_MultipliesByFour()
        {
            Assert.That(BlockGating.BreakSeconds(1f, 1, 0), Is.EqualTo(4f),
                "徒手挖石 4s（spec §1「徒手 4s 不掉落」——×4 倍率的出处）");
            Assert.That(BlockGating.BreakSeconds(2f, 2, 1), Is.EqualTo(8f), "木镐挖铁 8s（且不掉）");
            Assert.That(BlockGating.BreakSeconds(3f, 3, 2), Is.EqualTo(12f), "石镐挖金 12s（且不掉）");
            Assert.That(BlockGating.BreakSeconds(6f, 4, 3), Is.EqualTo(24f), "铁镐挖机元 24s（且不掉）");
        }

        [Test]
        public void BreakSeconds_ZeroTierBlock_AlwaysHardness()
        {
            Assert.That(BlockGating.BreakSeconds(1f, 0, 0), Is.EqualTo(1f), "徒手挖泥 1s");
            Assert.That(BlockGating.BreakSeconds(2f, 0, 0), Is.EqualTo(2f), "徒手砍木头 2s");
            Assert.That(BlockGating.BreakSeconds(1f, 0, 4), Is.EqualTo(1f),
                "门槛 0 的方块拿什么挖都一样快（镐对泥/木没有加成，spec 未定义加速）");
        }

        // ─── 真实数据文件端到端（spec §1 矩阵全量） ─────────────────────────

        /// <summary>
        /// 真实 items/*.json 里六把镐的 toolTier 与 spec §1 门槛矩阵一致。
        /// 注意：本仓库没有金镐（金系装备走剑/斧，spec §3），下界合金镐按 MC 惯例
        /// 与钻石镐同级（挖掘门槛 4，只是耐久/属性更好）。
        /// </summary>
        [Test]
        public void RealItems_PickaxeToolTiers_MatchTheGatingMatrix()
        {
            var items = ItemDatabase.FromJson(
                Directory.GetFiles(LocateItemsDirectory(), "*.json").Select(File.ReadAllText));

            (string id, int expectedTier)[] expected =
            {
                ("wooden_pickaxe", 1),
                ("stone_pickaxe", 2),
                ("iron_pickaxe", 3),
                ("diamond_pickaxe", 4),
                ("netherite_pickaxe", 4),
                ("bedrock_pickaxe", 99),
            };

            foreach ((string id, int expectedTier) in expected)
            {
                Assert.That(items.TryGetById(id, out var def), Is.True, $"{id} 应在真实物品表中");
                Assert.That(def.ToolTier, Is.EqualTo(expectedTier),
                    $"{id} 的 toolTier 应为 {expectedTier}（spec §1 / MC 惯例）");
            }

            // 非镐工具一律 0：剑/斧/锹的 miningLevel 是耐久档位，不能冒充镐门槛
            foreach (string id in new[] { "wooden_sword", "stone_axe", "stone_shovel" })
            {
                Assert.That(items.GetById(id).ToolTier, Is.EqualTo(0),
                    $"{id} 不写 toolTier（非镐类），门槛判定应视同徒手");
            }
        }

        /// <summary>
        /// fix1 I2 守卫：所有 <c>*_pickaxe</c> 类物品必须**显式声明** <c>toolTier</c>（漏写即红）。
        /// toolTier 缺省 0 = 视同徒手——一把「挖不动任何矿」的镐在实机上没有任何线索可查，
        /// 只能让数据契约在加载层就拦住。断言 <c>ToolTier ≥ 1</c>：漏写（缺省 0）和显式写 0
        /// 都是「不如徒手的镐」，一并拦下。将来 C 阶段加金镐时按 MC 惯例写 2（金镐等同石镐），
        /// 合金镐/机元镐按 spec §3 各自定档——不管写几，必须写。
        /// </summary>
        [Test]
        public void RealItems_EveryPickaxe_ExplicitlyDeclaresToolTier()
        {
            var items = ItemDatabase.FromJson(
                Directory.GetFiles(LocateItemsDirectory(), "*.json").Select(File.ReadAllText));

            string[] pickaxeIds = items.ById.Keys
                .Where(id => id.EndsWith("_pickaxe", StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            Assert.That(pickaxeIds.Length, Is.EqualTo(6),
                "当前仓库应有 6 个 *_pickaxe 物品（木/石/铁/钻/下界合金/基岩）。数目变了请同步本断言、" +
                "RealItems_PickaxeToolTiers_MatchTheGatingMatrix 与门槛矩阵——特别地，加新镐必须显式写 toolTier");

            foreach (string id in pickaxeIds)
            {
                Assert.That(items.GetById(id).ToolTier, Is.GreaterThanOrEqualTo(1),
                    $"{id} 是镐类物品，必须显式声明 toolTier 且 ≥ 1（漏写或缺省 0 会让它挖不动任何有门槛的矿，" +
                    "实机上无线索可查）");
            }
        }

        /// <summary>
        /// m10 B1：真实 items/*.json 六把镐的耐久阶梯。木/石/铁照 MC 原值（59/131/250，
        /// 恰好放得进 Metadata 的 8 位上限）；钻 1561 / 下界合金 2031 超 255 放不下，封顶 255
        /// （顶级镐的进阶差异在 toolTier/攻击力，不在耐久）；基岩镐是彩蛋终局镐，同样 255。
        /// 改任何一把的耐久这里立刻红。
        /// </summary>
        [Test]
        public void RealItems_PickaxeMaxDurability_MatchTheLadder()
        {
            var items = ItemDatabase.FromJson(
                Directory.GetFiles(LocateItemsDirectory(), "*.json").Select(File.ReadAllText));

            (string id, int expected)[] expected =
            {
                ("wooden_pickaxe", 59),
                ("stone_pickaxe", 131),
                ("iron_pickaxe", 250),
                ("diamond_pickaxe", 255),
                ("netherite_pickaxe", 255),
                ("bedrock_pickaxe", 255),
            };

            foreach ((string id, int durability) in expected)
            {
                Assert.That(items.TryGetById(id, out var def), Is.True, $"{id} 应在真实物品表中");
                Assert.That(def.MaxDurability, Is.EqualTo(durability),
                    $"{id} 的 maxDurability 应为 {durability}（MC 原值放不进 8 位编码的一律封顶 255）");
            }
        }

        /// <summary>
        /// m10 B1 守卫：所有 <c>*_pickaxe</c> 必须显式声明 <c>maxDurability</c>（漏写即红）。
        /// 缺省 0 = 无耐久概念，挖矿永不磨损——「镐子会碎」直接静默失效，
        /// 实机上同样无线索可查，与上面 toolTier 守卫同款在数据层拦下。
        /// </summary>
        [Test]
        public void RealItems_EveryPickaxe_ExplicitlyDeclaresMaxDurability()
        {
            var items = ItemDatabase.FromJson(
                Directory.GetFiles(LocateItemsDirectory(), "*.json").Select(File.ReadAllText));

            string[] pickaxeIds = items.ById.Keys
                .Where(id => id.EndsWith("_pickaxe", StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            foreach (string id in pickaxeIds)
            {
                Assert.That(items.GetById(id).MaxDurability, Is.InRange(1, 255),
                    $"{id} 是镐类物品，必须显式声明 maxDurability（1..255）——漏写会让它永不磨损");
            }
        }

        /// <summary>
        /// 真实 blocks/*.json 的 hardness + minToolTier 组合出 spec §1 的整张
        /// (block, toolTier) → 秒 矩阵。JSON 数据改坏（比如石头 hardness 偏离 1）
        /// 在这里立刻失败——它是 Unity 侧 BlockInteraction.BreakTime 查表的数据源。
        /// </summary>
        [Test]
        public void RealBlocks_SpecOneMatrix_FromHardnessAndMinToolTier()
        {
            var blocks = BlockRegistry.FromJson(
                Directory.GetFiles(LocateBlocksDirectory(), "*.json").Select(File.ReadAllText));

            float Seconds(ushort blockId, int toolTier)
            {
                BlockDefinition def = blocks.GetByNumericId(blockId);
                return BlockGating.BreakSeconds(def.Hardness, def.MinToolTier, toolTier);
            }

            // spec §1「挖掘时间（对应镐）」列 + 徒手列
            Assert.That(Seconds(BlockIds.Dirt, 0), Is.EqualTo(1f), "泥土：徒手 1s");
            Assert.That(Seconds(BlockIds.Sand, 0), Is.EqualTo(1f), "沙：徒手 1s（群系折扣在 Unity 层另算）");
            Assert.That(Seconds(TreeFeature.LogId, 0), Is.EqualTo(2f), "木头：徒手 2s");
            Assert.That(Seconds(BlockIds.Stone, 0), Is.EqualTo(4f), "石头：徒手 4s 不掉落");
            Assert.That(Seconds(BlockIds.Stone, 1), Is.EqualTo(1f), "石头：木镐 1s");
            Assert.That(Seconds(BlockIds.RawIronOre, 1), Is.EqualTo(8f), "铁矿：木镐 8s 不掉落");
            Assert.That(Seconds(BlockIds.RawIronOre, 2), Is.EqualTo(2f), "铁矿：石镐 2s");
            Assert.That(Seconds(BlockIds.GoldOre, 3), Is.EqualTo(3f), "金矿：铁镐 3s");
            Assert.That(Seconds(BlockIds.SummerAlloyOre, 3), Is.EqualTo(4f), "夏季合金：铁镐 4s");
            Assert.That(Seconds(BlockIds.MachineEssenceOre, 3), Is.EqualTo(24f), "机元：铁镐 24s 不掉落");
            Assert.That(Seconds(BlockIds.MachineEssenceOre, 4), Is.EqualTo(6f), "机元：钻镐 6s");
        }

        // ─── 目录定位（dotnet 从输出目录向上爬；Unity 走 streamingAssetsPath） ──

        private static string LocateItemsDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "items");
#else
            return LocateStreamingAssetsSubdirectory("items");
#endif
        }

        private static string LocateBlocksDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "blocks");
#else
            return LocateStreamingAssetsSubdirectory("blocks");
#endif
        }

        private static string LocateStreamingAssetsSubdirectory(string name)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", name);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                $"未能从测试输出目录向上找到 Assets/StreamingAssets/{name}。");
        }
    }
}
