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
            Assert.That(_registry.Count, Is.EqualTo(20), "当前应有 7 个内置方块 + 8 个里程碑-3 新方块 + 1 个雪方块 + 4 个矿石 = 20 个");
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
