using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// 校验仓库中真实的方块定义文件。改坏 JSON 会在这里立刻失败，不必等到进游戏才发现。
    /// </summary>
    [TestFixture]
    public class BlockDefinitionFilesTests
    {
        private static readonly string[] ExpectedTextures =
        {
            "stone", "dirt", "grass-top", "grass-side", "sand", "water", "bedrock"
        };

        private BlockRegistry _registry;

        [OneTimeSetUp]
        public void LoadRealDefinitions()
        {
            string directory = LocateBlocksDirectory();
            IEnumerable<string> documents = Directory.GetFiles(directory, "*.json").Select(File.ReadAllText);
            _registry = BlockRegistry.FromJson(documents);
        }

        [Test]
        public void AllDefinitionFiles_ParseAndValidate()
        {
            // 7 个内置方块（air/stone/dirt/grass/sand/water/bedrock）+ 里程碑-3 新增 8 个
            // （planks/log/leaves/sapling/crafting_table/iron_door/lever/redstone_dust）
            // + F2 雪原 snow + m10 四矿石（gold_ore/raw_iron_ore/summer_alloy_ore/machine_essence_ore）
            // + m11 第 1 波并行注册：W1-3 植被 25、W1-4 功能 5（bed/chest/glass/torch/wooden_door）、
            // W1-5 家具 9、W1-6 农业 11——各波共享同一工作区持续落文件。
            // 硬编码总数在并行波次下互相踩（每落一批就得改一次这行），改为「注册表数 = 目录
            // .json 文件数」动态对账：任何文件解析失败 / id 重复 / numericId 冲突都会让
            // OneTimeSetUp 的 FromJson 直接抛异常，守卫力度不减；数量对不上说明有文件漏读。
            string blocksDir = LocateBlocksDirectory();
            int fileCount = Directory.GetFiles(blocksDir, "*.json").Length;
            Assert.That(_registry.Count, Is.EqualTo(fileCount),
                "注册表方块数应等于 blocks 目录的 .json 文件数（一个文件一种方块，一个都不能解析失败）");
            Assert.That(_registry.Count, Is.AtLeast(70), "第 1 波四路全部落地后至少 70 个（20 既有 + 25 植被 + 5 功能 + 9 家具 + 11 农业）");
        }

        [TestCase("air", BlockIds.Air)]
        [TestCase("stone", BlockIds.Stone)]
        [TestCase("dirt", BlockIds.Dirt)]
        [TestCase("grass", BlockIds.Grass)]
        [TestCase("sand", BlockIds.Sand)]
        [TestCase("water", BlockIds.Water)]
        [TestCase("bedrock", BlockIds.Bedrock)]
        [TestCase("snow", BlockIds.Snow)]
        [TestCase("gold_ore", BlockIds.GoldOre)]
        [TestCase("raw_iron_ore", BlockIds.RawIronOre)]
        [TestCase("summer_alloy_ore", BlockIds.SummerAlloyOre)]
        [TestCase("machine_essence_ore", BlockIds.MachineEssenceOre)]
        public void NumericIds_MatchTheConstantsUsedByTheGenerator(string id, ushort expected)
        {
            Assert.That(_registry.GetById(id).NumericId, Is.EqualTo(expected),
                $"{id} 的 numericId 与 BlockIds 常量不一致，世界生成会放错方块");
        }

        [Test]
        public void EveryReferencedTexture_IsOnTheArtRequestList()
        {
            var referenced = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string id in new[] { "stone", "dirt", "grass", "sand", "water", "bedrock" })
            {
                foreach (string texture in _registry.GetById(id).Textures)
                {
                    referenced.Add(texture);
                }
            }

            Assert.That(referenced, Is.EquivalentTo(ExpectedTextures),
                "方块引用的贴图与 art/requests/blocks 下已提需求的贴图不一致");
        }

        [Test]
        public void Water_IsNonSolidTransparentLiquid()
        {
            BlockDefinition water = _registry.GetById("water");

            Assert.That(water.Solid, Is.False, "水不能挡住玩家");
            Assert.That(water.Opaque, Is.False, "水不透明会导致水下全黑且面被错误剔除");
            Assert.That(water.Liquid, Is.True);
        }

        [Test]
        public void BedrockAndWater_AreUnbreakable()
        {
            Assert.That(_registry.GetById("bedrock").IsUnbreakable, Is.True);
            Assert.That(_registry.GetById("water").IsUnbreakable, Is.True);
        }

        [Test]
        public void Grass_UsesDirtOnItsBottomFace()
        {
            BlockDefinition grass = _registry.GetById("grass");

            Assert.That(grass.Textures[(int)BlockFace.Bottom], Is.EqualTo("dirt"),
                "草方块底面应与泥土一致，否则挖开后底面会露馅");
        }

        /// <summary>
        /// m10 A1：工具门槛矩阵（spec §1）落进 blocks/*.json 的 minToolTier 字段。
        /// 0 手 / 1 木镐 / 2 石镐 / 3 铁镐 / 4 钻石镐。
        /// </summary>
        [TestCase("dirt", 0, "泥土徒手可挖")]
        [TestCase("log", 0, "木头徒手可挖")]
        [TestCase("stone", 1, "石头至少要木镐（徒手挖 4s 不掉落是既有规则）")]
        [TestCase("raw_iron_ore", 2, "铁矿需石镐——木镐挖了不掉")]
        [TestCase("gold_ore", 3, "金矿需铁镐")]
        [TestCase("summer_alloy_ore", 3, "夏季合金矿需铁镐")]
        [TestCase("machine_essence_ore", 4, "机元矿需钻石镐")]
        [TestCase("diamond_ore", 3, "钻石矿需铁镐（评审 08 F0——MC 同款门槛）")]
        public void MinToolTier_MatchesTheGatingMatrix(string id, int expected, string reason)
        {
            Assert.That(_registry.GetById(id).MinToolTier, Is.EqualTo(expected),
                $"{id} 的 minToolTier 应为 {expected}：{reason}");
        }

        [Test]
        public void MinToolTier_MissingField_DefaultsToZero()
        {
            // planks 是 m3 的老方块，JSON 里没写 minToolTier——默认 0（徒手），
            // 加新方块不写这个字段不能把老存档世界变得挖不动。
            Assert.That(_registry.GetById("planks").MinToolTier, Is.EqualTo(0),
                "未声明 minToolTier 的方块应默认 0（徒手可挖）");
        }

        /// <summary>m11 W1-3 新增的 25 个植被方块 id 清单（13 树木 + 12 花草）。</summary>
        private static readonly string[] M11VegetationBlockIds =
        {
            // 树木 13：6 新树种各 log+leaves，外加 bush_leaves
            "birch_log", "birch_leaves", "pine_log", "pine_leaves", "cedar_log", "cedar_leaves",
            "jungle_log", "jungle_leaves", "bush_leaves", "sequoia_log", "sequoia_leaves",
            "cherry_log", "cherry_leaves",
            // 花草 12（十字类先例：不挡人不挡光、徒手秒挖）
            "flower_poppy", "flower_dandelion", "flower_orchid", "flower_cornflower",
            "flower_rose", "flower_sunflower", "flower_lilac", "flower_daisy",
            "tall_grass", "fern", "mushroom_red", "mushroom_brown"
        };

        /// <summary>
        /// m11 W1-3：25 个新植被方块的贴图引用必须在 art/requests/blocks 下有同名需求
        /// （文件名 = 贴图名 + .md，块 id 的下划线对应需求的连字符）。png 未入库走品红占位不阻塞，
        /// 但需求文件缺失会让美术链路断档。
        /// </summary>
        [Test]
        public void M11VegetationBlocks_ReferencedTextures_HaveArtRequests()
        {
            string artRequestsDir = LocateArtRequestsDirectory();

            foreach (string blockId in M11VegetationBlockIds)
            {
                BlockDefinition definition = _registry.GetById(blockId);
                Assert.That(definition, Is.Not.Null, $"植被方块 {blockId} 必须已注册");

                foreach (string texture in definition.Textures)
                {
                    Assert.That(texture, Is.EqualTo(blockId.Replace('_', '-')),
                        $"{blockId} 的贴图名应与同名 art 需求一致（下划线转连字符）");
                    Assert.That(File.Exists(Path.Combine(artRequestsDir, texture + ".md")), Is.True,
                        $"{blockId} 引用的贴图 {texture} 在 art/requests/blocks 下没有需求文件");
                }
            }
        }

        /// <summary>花草是十字类装饰：不挡移动（Solid=false）、不挡光（Opaque=false）、徒手秒挖（hardness 0）。</summary>
        [Test]
        public void M11FlowerBlocks_AreNonSolidNonOpaqueAndInstantMineable()
        {
            string[] flowerIds =
            {
                "flower_poppy", "flower_dandelion", "flower_orchid", "flower_cornflower",
                "flower_rose", "flower_sunflower", "flower_lilac", "flower_daisy",
                "tall_grass", "fern", "mushroom_red", "mushroom_brown"
            };

            foreach (string id in flowerIds)
            {
                BlockDefinition flower = _registry.GetById(id);
                Assert.That(flower.Solid, Is.False, $"{id} 不应阻挡移动（Solid=false）");
                Assert.That(flower.Opaque, Is.False, $"{id} 不应挡光（Opaque=false，否则四周面被错误剔除）");
                Assert.That(flower.Hardness, Is.EqualTo(0f), $"{id} 徒手秒挖（hardness=0）");
                Assert.That(flower.MinToolTier, Is.EqualTo(0), $"{id} 不需要工具门槛");
            }
        }

        /// <summary>新树种的 log/leaves 物理属性照既有 log.json / leaves.json：实体、不透明、硬度 2、徒手可挖。</summary>
        [Test]
        public void M11TreeBlocks_MatchLegacyLogAndLeavesShape()
        {
            string[] treeBlockIds =
            {
                "birch_log", "pine_log", "cedar_log", "jungle_log", "sequoia_log", "cherry_log",
                "birch_leaves", "pine_leaves", "cedar_leaves", "jungle_leaves",
                "bush_leaves", "sequoia_leaves", "cherry_leaves"
            };

            foreach (string id in treeBlockIds)
            {
                BlockDefinition block = _registry.GetById(id);
                Assert.That(block.Solid, Is.True, $"{id} 应照 log/leaves 保持实体（Solid=true）");
                Assert.That(block.Opaque, Is.True, $"{id} 应照 log/leaves 保持不透明（Opaque=true）");
                Assert.That(block.Hardness, Is.EqualTo(2.0f), $"{id} 硬度照 log/leaves 为 2.0");
                Assert.That(block.MinToolTier, Is.EqualTo(0), $"{id} 徒手可挖");
            }
        }

        /// <summary>
        /// m11 W1-5 九件家具的注册对照表：方块 id / numericId / 贴图名（= art 需求名）/ 对应物品 id。
        /// numericId 与 blocks/drops/block_drops.json 的 blockNumericId 一一对应（守卫测试钉死两边同步）。
        /// <para>
        /// av W2-11：laptop_block 与 hacker_pc_block 的顶面是屏幕面（"laptop-screen"），
        /// 其余面与原贴图一致——视频背景由 <see cref="MyWorld.Unity.Rendering.VideoScreenSystem"/>
        /// 把贴图槽共享材质的 mainTexture 换成 VideoPlayer 的 RenderTexture。
        /// </para>
        /// </summary>
        private static readonly (string BlockId, ushort NumericId, string Texture, string ItemId)[] M11FurnitureBlocks =
        {
            ("chair_block", 1013, "chair", "chair"),
            ("globe_block", 1014, "globe", "globe"),
            ("hacker_pc_block", 1015, "hacker-pc", "hacker_pc"),
            ("keyboard_block", 1016, "keyboard", "keyboard"),
            ("laptop_block", 1017, "laptop", "laptop"),
            ("mouse_block", 1018, "mouse", "mouse"),
            ("notebook_block", 1019, "notebook", "notebook"),
            ("office_desk_block", 1020, "office-desk", "office_desk"),
            ("table_block", 1021, "table", "table"),
        };

        /// <summary>
        /// m11 W1-5：家具是装饰方块——不挡移动（Solid=false，WorldSolidSource 判它可穿行）、
        /// 不挡视线（Opaque=false，避免把邻居的面错误剔除）、无工具门槛且 hardness=1
        /// （徒手 1s 秒挖；1.0 与 BlockInteraction.BreakTime 的 default 分支一致，
        /// 全量一致性守卫 BlockToolTierTests 不需要进 switch）。
        /// </summary>
        [Test]
        public void M11FurnitureBlocks_AreNonSolidNonOpaqueAndHandMineable()
        {
            foreach ((string blockId, ushort numericId, string _, string _) in M11FurnitureBlocks)
            {
                BlockDefinition furniture = _registry.GetById(blockId);
                Assert.That(furniture.NumericId, Is.EqualTo(numericId),
                    $"{blockId} 的 numericId 应为 {numericId}——block_drops.json 按数字查表，改号必须两边同步");
                Assert.That(furniture.Solid, Is.False,
                    $"{blockId} 是装饰性家具，不能阻挡玩家移动（WorldSolidSource 判 Solid）");
                Assert.That(furniture.Opaque, Is.False,
                    $"{blockId} 不挡视线（ChunkMeshSource 判 Opaque），否则贴墙摆放会把邻居的面错误剔除");
                Assert.That(furniture.MinToolTier, Is.EqualTo(0),
                    $"{blockId} 无工具门槛——家具必须徒手挖得掉且掉得回（门槛会吞掉落）");
                Assert.That(furniture.Hardness, Is.EqualTo(1f),
                    $"{blockId} 徒手 1s 秒挖（hardness=1，与 BreakTime default 分支一致，勿改其它值）");
            }
        }

        /// <summary>
        /// m11 W1-5：家具方块贴图复用同名美术需求（第 0 波 A2 立在 art/requests/items 下），
        /// 贴图名 = 需求文件名（多词连字符：office-desk / hacker-pc）。png 未入库走品红占位不阻塞，
        /// 但需求文件缺失会让美术链路断档，这里照 W1-3 植被同款守卫拦下。
        /// <para>
        /// av W2-11：laptop_block / hacker_pc_block 的顶面（<see cref="BlockFace.Top"/>）必须是
        /// "laptop-screen"——这是 VideoScreenSystem 换 RenderTexture 的目标贴图槽。
        /// 其余五面统一引用原贴图名。
        /// </para>
        /// </summary>
        [Test]
        public void M11FurnitureBlocks_ReferencedTextures_HaveArtRequests()
        {
            // blocks 目录位于 <仓库根>/Assets/StreamingAssets/blocks，向上三级即仓库根；
            // 家具需求在第 0 波落在 art/requests/items（植被在 blocks），全树搜不挑子目录
            string repoRoot = new DirectoryInfo(LocateBlocksDirectory()).Parent.Parent.Parent.FullName;
            string artRequestsRoot = Path.Combine(repoRoot, "art", "requests");
            Assert.That(Directory.Exists(artRequestsRoot), Is.True,
                $"未找到美术需求目录 {artRequestsRoot}");

            foreach ((string blockId, ushort _, string texture, string _) in M11FurnitureBlocks)
            {
                BlockDefinition furniture = _registry.GetById(blockId);
                bool hasScreen = blockId == "laptop_block" || blockId == "hacker_pc_block";
                int screenCount = 0;
                foreach (string face in furniture.Textures)
                {
                    if (hasScreen && face == "laptop-screen") { screenCount++; continue; }
                    Assert.That(face, Is.EqualTo(texture),
                        $"{blockId} 非 screen 面应统一引用 {texture}（装饰方块除顶面屏幕外不分面）");
                }
                Assert.That(screenCount, Is.EqualTo(hasScreen ? 1 : 0),
                    $"{blockId} 顶面应为 laptop-screen（恰好一面，供 VideoScreenSystem 换 RenderTexture）");

                Assert.That(
                    Directory.GetFiles(artRequestsRoot, texture + ".md", SearchOption.AllDirectories).Length,
                    Is.GreaterThan(0),
                    $"{blockId} 引用的贴图 {texture} 在 art/requests 下没有需求文件（家具需求应在 items/{texture}.md）");
            }

            // av W2-11：laptop-screen 自身也应有需求文件
            Assert.That(
                Directory.GetFiles(artRequestsRoot, "laptop-screen.md", SearchOption.AllDirectories).Length,
                Is.GreaterThan(0), "laptop-screen 在 art/requests 下没有需求文件");
        }

        /// <summary>从 StreamingAssets/blocks 向上找仓库根下的 art/requests/blocks（美术需求索引目录）。</summary>
        private static string LocateArtRequestsDirectory()
        {
            // blocks 目录位于 <仓库根>/Assets/StreamingAssets/blocks，向上三级即仓库根
            string blocksDir = LocateBlocksDirectory();
            string repoRoot = new DirectoryInfo(blocksDir).Parent.Parent.Parent.FullName;
            string candidate = Path.Combine(repoRoot, "art", "requests", "blocks");
            Assert.That(Directory.Exists(candidate), Is.True,
                $"未找到美术需求目录 {candidate}——贴图需求索引是注册方块的验收前置");
            return candidate;
        }

        [Test]
        public void OreBlocks_ReferencedTextureFiles_Exist()
        {
            // 四矿石的贴图名约定：金/粗铁复用 B-14 已入库的 gold-ore/iron-ore，
            // 夏季合金/机元是 m10 新程序占位贴图
            (string blockId, string expectedTexture)[] ores =
            {
                ("gold_ore", "gold-ore"),
                ("raw_iron_ore", "iron-ore"),
                ("summer_alloy_ore", "summer-alloy-ore"),
                ("machine_essence_ore", "machine-essence-ore"),
            };

            string texturesDir = Path.Combine(LocateBlocksDirectory(), "textures");

            foreach ((string blockId, string expectedTexture) in ores)
            {
                BlockDefinition ore = _registry.GetById(blockId);
                foreach (string texture in ore.Textures)
                {
                    Assert.That(texture, Is.EqualTo(expectedTexture),
                        $"{blockId} 六面都应引用 {expectedTexture}（矿石不分面）");
                    Assert.That(File.Exists(Path.Combine(texturesDir, texture + ".png")), Is.True,
                        $"{blockId} 引用的贴图 {texture}.png 不存在，实机会显示 missing 棕块");
                }
            }
        }

        private static string LocateBlocksDirectory()
        {
#if UNITY_EDITOR
            // Unity 下 AppContext.BaseDirectory 指向编辑器安装目录，必须走引擎提供的路径
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "blocks");
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "blocks");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("未能从测试输出目录向上找到 Assets/StreamingAssets/blocks。");
#endif
        }
    }
}
