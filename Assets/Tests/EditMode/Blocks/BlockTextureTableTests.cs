using System.Collections.Generic;
using System.Linq;
using MyWorld.Core.Blocks;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    [TestFixture]
    public class BlockTextureTableTests
    {
        private const string AirJson = @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }";

        private const string StoneJson =
            @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }";

        private const string GrassJson =
            @"{ ""id"": ""grass"", ""numericId"": 2,
                ""textures"": { ""top"": ""grass-top"", ""bottom"": ""dirt"", ""side"": ""grass-side"" } }";

        /// <summary>TextureNames 是 IReadOnlyList，没有 IndexOf，这里包一层。</summary>
        private static int SlotOf(BlockRegistry registry, string textureName)
            => registry.TextureNames.ToList().IndexOf(textureName);

        private static BlockRegistry Build(params string[] documents)
            => BlockRegistry.FromJson(new List<string>(documents));

        [Test]
        public void TextureNames_ContainsEveryReferencedTextureExactlyOnce()
        {
            BlockRegistry registry = Build(AirJson, StoneJson, GrassJson);

            Assert.That(registry.TextureNames,
                Is.EquivalentTo(new[] { "dirt", "grass-side", "grass-top", "stone" }),
                "被引用的贴图应当去重后全部登记");
        }

        [Test]
        public void TextureNames_AreSortedOrdinally_SoIndicesAreStableAcrossMachines()
        {
            BlockRegistry registry = Build(AirJson, GrassJson, StoneJson);
            BlockRegistry reversed = Build(StoneJson, GrassJson, AirJson);

            // StringComparer 同时实现了 IComparer 和 IComparer<string>，不指明泛型参数会二义
            Assert.That(registry.TextureNames, Is.Ordered.Using<string>(System.StringComparer.Ordinal));
            Assert.That(reversed.TextureNames, Is.EqualTo(registry.TextureNames),
                "贴图索引不能依赖方块定义的枚举顺序");
        }

        [Test]
        public void GetTextureIndex_ReturnsSlotOfThatFacesTexture()
        {
            BlockRegistry registry = Build(AirJson, StoneJson, GrassJson);

            Assert.That(registry.GetTextureIndex(2, BlockFace.Top), Is.EqualTo(SlotOf(registry, "grass-top")));
            Assert.That(registry.GetTextureIndex(2, BlockFace.Bottom), Is.EqualTo(SlotOf(registry, "dirt")));
            Assert.That(registry.GetTextureIndex(2, BlockFace.North), Is.EqualTo(SlotOf(registry, "grass-side")));
        }

        [Test]
        public void GetTextureIndex_ForUniformBlock_IsSameOnEveryFace()
        {
            BlockRegistry registry = Build(AirJson, StoneJson, GrassJson);
            int expected = SlotOf(registry, "stone");

            for (var face = 0; face < 6; face++)
            {
                Assert.That(registry.GetTextureIndex(1, (BlockFace)face), Is.EqualTo(expected));
            }
        }

        [Test]
        public void GetTextureIndex_ForAir_IsNoTextureIndex()
        {
            BlockRegistry registry = Build(AirJson, StoneJson);

            Assert.That(registry.GetTextureIndex(0, BlockFace.Top), Is.EqualTo(BlockRegistry.NoTextureIndex),
                "空气没有贴图");
        }

        [Test]
        public void GetTextureIndex_ForUnregisteredBlock_IsNoTextureIndex()
        {
            BlockRegistry registry = Build(AirJson, StoneJson);

            Assert.That(registry.GetTextureIndex(9999, BlockFace.Top), Is.EqualTo(BlockRegistry.NoTextureIndex),
                "热路径查询未注册 ID 不应抛异常");
        }
    }
}
