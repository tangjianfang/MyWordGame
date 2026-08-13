using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyWorld.Core.WorldGen
{
    public class BiomeConfig
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public float Temperature { get; set; }
        public float Humidity { get; set; }
        public int TreeDensity { get; set; }
        public float CaveMultiplier { get; set; } = 1.0f;
    }

    public static class BiomeConfigLoader
    {
        public static List<BiomeConfig> Load(string jsonPath)
        {
            string json = System.IO.File.ReadAllText(jsonPath);
            return JsonConvert.DeserializeObject<List<BiomeConfig>>(json);
        }
    }
}