namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 基于温度+湿度选生物群系。
    /// 简化规则：温度>0.7 干旱=沙漠；湿度>0.7 冷=森林；温度<0.3 干燥=山地；其它=平原。
    /// </summary>
    public static class BiomeSelector
    {
        public static Biome Select(float temperature, float humidity)
        {
            if (temperature > 0.7f && humidity < 0.4f) return Biome.Desert;
            if (humidity > 0.6f && temperature < 0.6f) return Biome.Forest;
            if (temperature < 0.3f && humidity < 0.3f) return Biome.Mountains;
            return Biome.Plains;
        }
    }
}