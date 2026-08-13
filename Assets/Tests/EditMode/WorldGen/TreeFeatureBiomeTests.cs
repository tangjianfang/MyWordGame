using NUnit.Framework;
using MyWorld.Core.WorldGen;

namespace MyWorld.Core.Tests.WorldGen
{
    /// <summary>
    /// Task C4：TreeFeature 按 BiomeConfig.TreeDensity 决定密度。
    /// 验证 ShouldPlaceTree 对不同群系配置给出预期的放置概率。
    /// </summary>
    [TestFixture]
    public class TreeFeatureBiomeTests
    {
        [Test]
        public void Desert_PlacesNoTrees()
        {
            var config = new BiomeConfig { Id = (int)Biome.Desert, TreeDensity = 0 };
            int trees = 0;
            for (int x = 0; x < 100; x++)
            for (int z = 0; z < 100; z++)
                if (TreeFeature.ShouldPlaceTree(x, z, config, seed: 42)) trees++;
            Assert.That(trees, Is.EqualTo(0), "沙漠 TreeDensity=0 不应生成树");
        }

        [Test]
        public void Forest_PlacesManyTrees()
        {
            var config = new BiomeConfig { Id = (int)Biome.Forest, TreeDensity = 30 };
            int trees = 0;
            for (int x = 0; x < 100; x++)
            for (int z = 0; z < 100; z++)
                if (TreeFeature.ShouldPlaceTree(x, z, config, seed: 42)) trees++;
            Assert.That(trees, Is.GreaterThan(50), "森林 TreeDensity=30 应高密度");
        }

        [Test]
        public void Plains_PlacesFewTrees()
        {
            var config = new BiomeConfig { Id = (int)Biome.Plains, TreeDensity = 8 };
            int trees = 0;
            for (int x = 0; x < 100; x++)
            for (int z = 0; z < 100; z++)
                if (TreeFeature.ShouldPlaceTree(x, z, config, seed: 42)) trees++;
            Assert.That(trees, Is.InRange(5, 30), "平原中等密度");
        }
    }
}
