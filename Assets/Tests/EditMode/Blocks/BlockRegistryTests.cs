using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Blocks;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    [TestFixture]
    public class BlockRegistryTests
    {
        private const string StoneJson = @"{
            ""id"": ""stone"",
            ""displayName"": ""石头"",
            ""numericId"": 1,
            ""textures"": { ""all"": ""stone"" },
            ""solid"": true,
            ""opaque"": true,
            ""lightEmission"": 0,
            ""hardness"": 1.5
        }";

        private const string GrassJson = @"{
            ""id"": ""grass"",
            ""displayName"": ""草方块"",
            ""numericId"": 3,
            ""textures"": { ""top"": ""grass-top"", ""bottom"": ""dirt"", ""side"": ""grass-side"" },
            ""solid"": true,
            ""opaque"": true,
            ""lightEmission"": 0,
            ""hardness"": 0.6
        }";

        [Test]
        public void Registry_LooksUpDefinitionByStringId()
        {
            BlockRegistry registry = BlockRegistry.FromJson(new[] { StoneJson });

            BlockDefinition stone = registry.GetById("stone");

            Assert.That(stone.DisplayName, Is.EqualTo("石头"));
            Assert.That(stone.Hardness, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void Registry_LooksUpDefinitionByNumericId()
        {
            BlockRegistry registry = BlockRegistry.FromJson(new[] { StoneJson });

            Assert.That(registry.GetByNumericId(1).Id, Is.EqualTo("stone"));
        }

        [Test]
        public void Textures_ShorthandAll_FillsEverySixFaces()
        {
            BlockRegistry registry = BlockRegistry.FromJson(new[] { StoneJson });

            BlockDefinition stone = registry.GetById("stone");

            for (var face = 0; face < 6; face++)
            {
                Assert.That(stone.Textures[face], Is.EqualTo("stone"), $"第 {face} 面贴图未填充");
            }
        }

        [Test]
        public void Textures_ShorthandTopBottomSide_MapsToCorrectFaces()
        {
            BlockRegistry registry = BlockRegistry.FromJson(new[] { GrassJson });

            BlockDefinition grass = registry.GetById("grass");

            Assert.That(grass.Textures[(int)BlockFace.Top], Is.EqualTo("grass-top"));
            Assert.That(grass.Textures[(int)BlockFace.Bottom], Is.EqualTo("dirt"));
            Assert.That(grass.Textures[(int)BlockFace.East], Is.EqualTo("grass-side"));
            Assert.That(grass.Textures[(int)BlockFace.West], Is.EqualTo("grass-side"));
            Assert.That(grass.Textures[(int)BlockFace.North], Is.EqualTo("grass-side"));
            Assert.That(grass.Textures[(int)BlockFace.South], Is.EqualTo("grass-side"));
        }

        [Test]
        public void NumericId_WhenOmitted_IsAutoAssignedFromOneThousand()
        {
            const string custom = @"{ ""id"": ""zebra"", ""textures"": { ""all"": ""t"" } }";
            const string other = @"{ ""id"": ""apple"", ""textures"": { ""all"": ""t"" } }";

            BlockRegistry registry = BlockRegistry.FromJson(new[] { custom, other });

            // 按字符串 id 排序后分配，保证与文件枚举顺序无关
            Assert.That(registry.GetById("apple").NumericId, Is.EqualTo(1000));
            Assert.That(registry.GetById("zebra").NumericId, Is.EqualTo(1001));
        }

        [Test]
        public void NumericId_AutoAssignment_IsIndependentOfInputOrder()
        {
            const string a = @"{ ""id"": ""apple"", ""textures"": { ""all"": ""t"" } }";
            const string z = @"{ ""id"": ""zebra"", ""textures"": { ""all"": ""t"" } }";

            BlockRegistry forward = BlockRegistry.FromJson(new[] { a, z });
            BlockRegistry reversed = BlockRegistry.FromJson(new[] { z, a });

            Assert.That(forward.GetById("apple").NumericId, Is.EqualTo(reversed.GetById("apple").NumericId));
            Assert.That(forward.GetById("zebra").NumericId, Is.EqualTo(reversed.GetById("zebra").NumericId));
        }

        [Test]
        public void DuplicateStringId_IsRejected()
        {
            Assert.Throws<InvalidDataException>(
                () => BlockRegistry.FromJson(new[] { StoneJson, StoneJson }));
        }

        [Test]
        public void DuplicateNumericId_IsRejected()
        {
            const string clash = @"{ ""id"": ""other"", ""numericId"": 1, ""textures"": { ""all"": ""t"" } }";

            Assert.Throws<InvalidDataException>(
                () => BlockRegistry.FromJson(new[] { StoneJson, clash }));
        }

        [Test]
        public void MissingId_IsRejected()
        {
            const string noId = @"{ ""textures"": { ""all"": ""t"" } }";

            Assert.Throws<InvalidDataException>(() => BlockRegistry.FromJson(new[] { noId }));
        }

        [Test]
        public void MissingTextures_IsRejectedForNonAirBlocks()
        {
            const string noTextures = @"{ ""id"": ""ghost"" }";

            Assert.Throws<InvalidDataException>(() => BlockRegistry.FromJson(new[] { noTextures }));
        }

        [Test]
        public void PartialTextureShorthand_IsRejected()
        {
            const string partial = @"{ ""id"": ""half"", ""textures"": { ""top"": ""t"" } }";

            Assert.Throws<InvalidDataException>(() => BlockRegistry.FromJson(new[] { partial }),
                "只给 top 却没给 bottom 和 side，会留下未填充的面");
        }

        [TestCase(-1)]
        [TestCase(16)]
        public void LightEmissionOutsideZeroToFifteen_IsRejected(int emission)
        {
            string json = $@"{{ ""id"": ""lamp"", ""textures"": {{ ""all"": ""t"" }}, ""lightEmission"": {emission} }}";

            Assert.Throws<InvalidDataException>(() => BlockRegistry.FromJson(new[] { json }));
        }

        [Test]
        public void MalformedJson_IsRejectedWithInvalidData()
        {
            Assert.Throws<InvalidDataException>(() => BlockRegistry.FromJson(new[] { "{ not json" }));
        }

        [Test]
        public void AirBlock_NeedsNoTexturesAndIsNotSolid()
        {
            const string air = @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }";

            BlockRegistry registry = BlockRegistry.FromJson(new[] { air });

            BlockDefinition definition = registry.GetById("air");
            Assert.That(definition.NumericId, Is.EqualTo(0));
            Assert.That(definition.Solid, Is.False);
            Assert.That(definition.Opaque, Is.False);
        }

        [Test]
        public void NegativeHardness_MarksBlockUnbreakable()
        {
            const string bedrock = @"{ ""id"": ""bedrock"", ""textures"": { ""all"": ""bedrock"" }, ""hardness"": -1 }";

            BlockRegistry registry = BlockRegistry.FromJson(new[] { bedrock });

            Assert.That(registry.GetById("bedrock").IsUnbreakable, Is.True);
        }

        [Test]
        public void UnknownStringId_ThrowsWithHelpfulMessage()
        {
            BlockRegistry registry = BlockRegistry.FromJson(new[] { StoneJson });

            var exception = Assert.Throws<KeyNotFoundException>(() => registry.GetById("nope"));
            Assert.That(exception.Message, Does.Contain("nope"));
        }
    }
}
