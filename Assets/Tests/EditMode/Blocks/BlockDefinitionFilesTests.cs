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
            Assert.That(_registry.Count, Is.EqualTo(7), "当前应有 7 个内置方块定义");
        }

        [TestCase("air", BlockIds.Air)]
        [TestCase("stone", BlockIds.Stone)]
        [TestCase("dirt", BlockIds.Dirt)]
        [TestCase("grass", BlockIds.Grass)]
        [TestCase("sand", BlockIds.Sand)]
        [TestCase("water", BlockIds.Water)]
        [TestCase("bedrock", BlockIds.Bedrock)]
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

        private static string LocateBlocksDirectory()
        {
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
        }
    }
}
