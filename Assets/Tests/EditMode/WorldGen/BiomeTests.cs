using NUnit.Framework;
using MyWorld.Core.WorldGen;

namespace MyWorld.Core.Tests.WorldGen
{
    public class BiomeTests
    {
        [Test]
        public void GetBiome_PlainsAtLowTempLowHumidity()
        {
            var biome = BiomeSelector.Select(temperature: 0.3f, humidity: 0.3f);
            Assert.That(biome, Is.EqualTo(Biome.Plains));
        }

        [Test]
        public void GetBiome_DesertAtHighTempLowHumidity()
        {
            var biome = BiomeSelector.Select(temperature: 0.9f, humidity: 0.2f);
            Assert.That(biome, Is.EqualTo(Biome.Desert));
        }

        [Test]
        public void GetBiome_ForestAtLowTempHighHumidity()
        {
            var biome = BiomeSelector.Select(temperature: 0.4f, humidity: 0.8f);
            Assert.That(biome, Is.EqualTo(Biome.Forest));
        }

        [Test]
        public void GetBiome_MountainsAtLowTempVeryLowHumidity()
        {
            var biome = BiomeSelector.Select(temperature: 0.2f, humidity: 0.1f);
            Assert.That(biome, Is.EqualTo(Biome.Mountains));
        }

        [Test]
        public void BiomeConfigLoader_ParsesJson()
        {
            string json = @"[
                { ""id"": 0, ""name"": ""plains"", ""temperature"": 0.5, ""humidity"": 0.5, ""treeDensity"": 8, ""caveMultiplier"": 1.0 },
                { ""id"": 1, ""name"": ""desert"", ""temperature"": 0.9, ""humidity"": 0.2, ""treeDensity"": 0, ""caveMultiplier"": 0.5 }
            ]";
            string path = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllText(path, json);
            var configs = BiomeConfigLoader.Load(path);
            Assert.That(configs.Count, Is.EqualTo(2));
            Assert.That(configs[0].Name, Is.EqualTo("plains"));
        }
    }
}