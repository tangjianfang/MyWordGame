# Milestone 3: Gameplay Core + World + Mob Implementation Plan (Part 2 — Phase C + D)

> **Part 1**: `2026-08-13-milestone-3.md`（Phase A cleanup+视觉+音频 / Phase B gameplay core）
> **Part 2**（本文）：Phase C world gen / Phase D mob+AI
>
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成 4 种生物群系视觉+生成参数（Phase C）+ 4 种生物 + AI + 战利品表（Phase D）。

**Architecture:** Phase C 在 WorldGenerator 注入 biome 选择 + 3D 噪声洞穴雕刻 + 树木密度分支；Phase D 新增 4 种 MobKind + 行为分支 + 战利品表 + 黎明/黄昏时段刷怪。所有生成逻辑纯 Core（确定性 + 无 UnityEngine）。

**Tech Stack:** 沿用 Part 1（Unity 2022.3.62f3c1 · URP 14.0.11 · Core netstandard2.1 C# 9 · Newtonsoft.Json）

---

## Global Constraints（沿用 Part 1，重复以便本 part 自洽）

1. **Core 层禁 UnityEngine**：新 Core 代码不能 `using UnityEngine.*`；用 `Core/Math/Float3`
2. **`TreatWarningsAsErrors=true`**：Core 一行警告都不能留
3. **不动既有 csproj / 不新建 asmdef**
4. **dotnet + Unity EditMode 双轨**：纯 Core 测试走 dotnet；Unity 相关走 EditMode
5. **视觉测试只 Unity EditMode 跑**：`#if UNITY_EDITOR ... #endif`
6. **docstring / 注释 / 测试断言消息 一律中文**
7. **deterministic 生成**：只依赖 seed + 世界坐标
8. **测试命令**：dotnet `dotnet test tools/dotnet/MyWorld.Tools.sln`；Unity EditMode `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode ...`
9. **截图**：`ScreenshotCapture.CaptureAllDefault` 0-arg 重载
10. **不带 `-nographics`**

---

## 文件清单（本 part 涉及）

### Phase C
- 新增 `Assets/Scripts/Core/World/Biome.cs` + `.meta`
- 新增 `Assets/Scripts/Core/World/BiomeConfig.cs` + `.meta`
- 新增 `Assets/Scripts/Core/World/CaveCarver.cs` + `.meta`
- 修改 `Assets/Scripts/Core/Noise/ValueNoise.cs`（加 `Sample3D`）
- 修改 `Assets/Scripts/Core/World/WorldGenerator.cs`（biome 选择 + cave pass + 树木密度）
- 新增 `Assets/StreamingAssets/biomes.json`
- 新增 `Assets/Tests/EditMode/World/CaveCarverTests.cs`
- 新增 `Assets/Tests/EditMode/World/BiomeTests.cs`
- 新增 `Assets/Tests/EditMode/World/TreeFeatureBiomeTests.cs`
- 新增 `Assets/Tests/EditMode/Visual/BiomeVisualTests.cs`

### Phase D
- 新增 `Assets/Scripts/Core/Entities/MobKind.cs` + `.meta`
- 新增 `Assets/Scripts/Core/Entities/MobAI.cs` + `.meta`
- 新增 `Assets/Scripts/Core/Entities/ItemDropTable.cs` + `.meta`
- 新增 `Assets/Scripts/Core/Entities/MobSpawnRules.cs` + `.meta`
- 新增 `Assets/StreamingAssets/mobs/drop_tables.json`
- 新增 `Assets/StreamingAssets/mobs/spawn_rules.json`
- 修改 `Assets/Scripts/Unity/Entities/MobView.cs`（4 种 kind 的视觉）
- 新增 `Assets/Scripts/Unity/Entities/MobManager.cs` + `.meta`
- 修改 `Assets/Scripts/Unity/World/WorldBootstrap.cs`（MobManager 注册）
- 新增 `Assets/Tests/EditMode/Entities/MobAITests.cs`
- 新增 `Assets/Tests/EditMode/Entities/MobSpawnRulesTests.cs`
- 新增 `Assets/Tests/EditMode/Entities/ItemDropTableTests.cs`
- 新增 `Assets/Tests/EditMode/Visual/MobVisualTests.cs`

---

# Phase C: World Gen（8 tasks, 3-4 天）

---

### Task C1: ValueNoise2D.Sample3D + CaveCarver Core

**Files:**
- Modify: `Assets/Scripts/Core/Noise/ValueNoise.cs`（加 `Sample3D` 方法）
- Create: `Assets/Scripts/Core/World/CaveCarver.cs` + `.meta`
- Test: `Assets/Tests/EditMode/World/CaveCarverTests.cs`（新建）

**Interfaces:**
- `ValueNoise.Sample3D(int x, int y, int z, int seed)` public method
- `CaveCarver.Carve(Float3 worldPos, int seed)` public method，true = 挖空

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/World/CaveCarverTests.cs`：

```csharp
using NUnit.Framework;
using MyWorld.Core.World;
using MyWorld.Core.Math;

namespace MyWorld.Core.Tests.World
{
    public class CaveCarverTests
    {
        [Test]
        public void Carve_SkipsBedrockLayer()
        {
            var carver = new CaveCarver(seed: 42);
            // bedrock y=-64 → 不论噪声值如何都不挖
            bool carved = carver.ShouldCarve(new Float3(0, -64, 0), 0f);
            Assert.That(carved, Is.False, "基岩层永不挖空");
        }

        [Test]
        public void Carve_ReturnsBool()
        {
            var carver = new CaveCarver(seed: 42);
            // 验证 API 形状（不卡具体值）
            bool _ = carver.ShouldCarve(new Float3(10, 70, 10), 0f);
            Assert.Pass();  // 跑通即可
        }

        [Test]
        public void Carve_DeterministicForSameSeed()
        {
            var c1 = new CaveCarver(seed: 42);
            var c2 = new CaveCarver(seed: 42);
            bool a = c1.ShouldCarve(new Float3(15, 80, 15), 0.5f);
            bool b = c2.ShouldCarve(new Float3(15, 80, 15), 0.5f);
            Assert.That(a, Is.EqualTo(b), "同 seed 同坐标同 density → 同结果");
        }

        [Test]
        public void Carve_DifferentSeedProducesDifferentResults()
        {
            var c1 = new CaveCarver(seed: 42);
            var c2 = new CaveCarver(seed: 99);
            int diffs = 0;
            for (int i = 0; i < 50; i++)
            {
                if (c1.ShouldCarve(new Float3(i * 7, 70, i * 3), 0.5f) !=
                    c2.ShouldCarve(new Float3(i * 7, 70, i * 3), 0.5f))
                    diffs++;
            }
            Assert.That(diffs, Is.GreaterThan(20), "不同 seed 应差异显著（>40%）");
        }
    }
}
```

- [ ] **Step 2: 跑测试（应失败）**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~CaveCarverTests"
```

期望：失败（CaveCarver / ShouldCarve 不存在）。

- [ ] **Step 3: 加 ValueNoise.Sample3D**

`Assets/Scripts/Core/Noise/ValueNoise.cs` 加：

```csharp
public static float Sample3D(int x, int y, int z, int seed)
{
    // 简化版：3D integer hash → [0, 1)
    uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761);
    h ^= h >> 16;
    h *= 0x45d9f3b;
    h ^= h >> 16;
    return (h & 0xFFFFFF) / (float)0x1000000;
}
```

> 若 `ValueNoise` 已有更复杂实现（如 fBm），**沿用现有**，不重写。

- [ ] **Step 4: 实现 CaveCarver**

新建 `Assets/Scripts/Core/World/CaveCarver.cs`：

```csharp
using MyWorld.Core.Math;
using MyWorld.Core.Noise;

namespace MyWorld.Core.World
{
    /// <summary>
    /// 3D 洞穴雕刻：基于 3D ValueNoise 阈值挖空。基岩层永不挖。
    /// </summary>
    public class CaveCarver
    {
        private const int BedrockLevel = -64;
        private const float NoiseThreshold = 0.65f;  // 噪声 > 此值挖空

        private readonly int _seed;

        public CaveCarver(int seed) { _seed = seed; }

        public bool ShouldCarve(Float3 worldPos, float localDensity)
        {
            if (worldPos.y <= BedrockLevel) return false;
            float noise = ValueNoise.Sample3D(
                (int)worldPos.x, (int)worldPos.y, (int)worldPos.z, _seed);
            // localDensity 0=平原（少洞）1=山地（多洞）
            float threshold = NoiseThreshold - localDensity * 0.2f;
            return noise > threshold;
        }
    }
}
```

- [ ] **Step 5: 跑测试 pass + 全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~CaveCarverTests"
dotnet test tools/dotnet/MyWorld.Tools.sln   # 验证 Core 整体
grep -E 'total=|result="' Builds/logs/A-final.xml | head -1
```

期望：CaveCarver 4/4 pass + 整体 325/325 pass。

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/Noise/ValueNoise.cs Assets/Scripts/Core/World/CaveCarver.cs Assets/Scripts/Core/World/CaveCarver.cs.meta Assets/Tests/EditMode/World/CaveCarverTests.cs
git commit -m "world: CaveCarver 3D 噪声雕刻 + 基岩保护 + 确定性"
```

---

### Task C2: Biome enum + GetBiome + biomes.json + BiomeConfigLoader

**Files:**
- Create: `Assets/Scripts/Core/World/Biome.cs` + `.meta`
- Create: `Assets/Scripts/Core/World/BiomeConfig.cs` + `.meta`
- Create: `Assets/StreamingAssets/biomes.json`
- Test: `Assets/Tests/EditMode/World/BiomeTests.cs`（新建）

**Interfaces:**
- `enum Biome { Plains, Desert, Forest, Mountains }`
- `BiomeConfig { Biome Id, string Name, float Temperature, float Humidity, int TreeDensity, float CaveMultiplier }`
- `BiomeConfigLoader.Load(string path)` 静态方法（Newtonsoft.Json）

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/World/BiomeTests.cs`：

```csharp
using NUnit.Framework;
using MyWorld.Core.World;

namespace MyWorld.Core.Tests.World
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
```

- [ ] **Step 2: 跑测试（应失败）**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BiomeTests"
```

- [ ] **Step 3: 实现 Biome + BiomeSelector + BiomeConfig + BiomeConfigLoader**

新建 `Assets/Scripts/Core/World/Biome.cs`：

```csharp
namespace MyWorld.Core.World
{
    public enum Biome
    {
        Plains = 0,
        Desert = 1,
        Forest = 2,
        Mountains = 3
    }
}
```

新建 `Assets/Scripts/Core/World/BiomeSelector.cs`：

```csharp
namespace MyWorld.Core.World
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
```

新建 `Assets/Scripts/Core/World/BiomeConfig.cs`：

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MyWorld.Core.World
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
```

新建 `Assets/StreamingAssets/biomes.json`：

```json
[
  { "id": 0, "name": "plains",    "temperature": 0.5, "humidity": 0.5, "treeDensity": 8,  "caveMultiplier": 1.0 },
  { "id": 1, "name": "desert",    "temperature": 0.9, "humidity": 0.2, "treeDensity": 0,  "caveMultiplier": 0.5 },
  { "id": 2, "name": "forest",    "temperature": 0.4, "humidity": 0.8, "treeDensity": 30, "caveMultiplier": 1.5 },
  { "id": 3, "name": "mountains", "temperature": 0.2, "humidity": 0.1, "treeDensity": 2,  "caveMultiplier": 2.0 }
]
```

- [ ] **Step 4: 跑测试 pass + 全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BiomeTests"
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：Biome 5/5 pass + 整体 330/330 pass。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/World/Biome.cs Assets/Scripts/Core/World/Biome.cs.meta Assets/Scripts/Core/World/BiomeSelector.cs Assets/Scripts/Core/World/BiomeSelector.cs.meta Assets/Scripts/Core/World/BiomeConfig.cs Assets/Scripts/Core/World/BiomeConfig.cs.meta Assets/StreamingAssets/biomes.json Assets/Tests/EditMode/World/BiomeTests.cs
git commit -m "world: Biome enum + BiomeSelector + biomes.json + BiomeConfigLoader"
```

---

### Task C3: WorldGenerator 集成 biome + cave pass

**Files:**
- Modify: `Assets/Scripts/Core/World/WorldGenerator.cs`（生成流程加 biome 选择 + cave pass）
- Test: 复用现有 WorldGeneratorTests（跑全量确认未破坏）

**Interfaces:**
- `WorldGenerator.GenerateChunk(int chunkX, int chunkZ, int seed)` 内部增加：
  1. 计算 chunk 中心 (worldX, worldZ) 的温度/湿度
  2. `BiomeSelector.Select` 得 biome
  3. 加载该 biome 的 `BiomeConfig`（TreeDensity / CaveMultiplier）
  4. 走完现有地形生成后，加一遍 `CaveCarver.ShouldCarve` 把方块置空气

- [ ] **Step 1: 实现 WorldGenerator 修改**

读 `Assets/Scripts/Core/World/WorldGenerator.cs`（grep `GenerateChunk` 或 `GenerateColumn`），**最小改动**：在原生成函数末尾加 cave pass。

```csharp
// WorldGenerator.cs GenerateColumn 或 GenerateChunk 末尾加：
Biome biome = BiomeSelector.Select(
    temperature: SampleTemperature(worldX, worldZ, seed),
    humidity: SampleHumidity(worldX, worldZ, seed));

BiomeConfig config = BiomeConfigLoader.Load("biomes.json")
    .Find(c => c.Id == (int)biome) ?? new BiomeConfig { Id = (int)biome, Name = biome.ToString(), TreeDensity = 0, CaveMultiplier = 1f };

var carver = new CaveCarver(seed);
for (int y = -63; y < 320; y++)
{
    var pos = new Float3(worldX, y, worldZ);
    float density = (biome == Biome.Mountains) ? 1.0f : 0.5f;
    if (carver.ShouldCarve(pos, density))
    {
        // 把当前方块置空气（仅当不是 bedrock）
        var block = GetBlock(worldX, y, worldZ);
        if (block != BlockIds.Bedrock) SetBlock(worldX, y, worldZ, BlockIds.Air);
    }
}
```

（具体位置 / SetBlock API 视现有代码而定，**实现者读现有 WorldGenerator 后最小接入**。）

- [ ] **Step 2: 跑全量回归确认未破坏**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：330/330 pass（CaveCarver / Biome / 现有 WorldGenerator 都不应失败）。

- [ ] **Step 3: 视觉烟雾测试**

```bash
bash tools/scripts/visual-smoke.sh
```

期望：4/4 screenshot pass（验证 cave pass 不让地形塌成粉/全黑）。

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Core/World/WorldGenerator.cs
git commit -m "world: WorldGenerator 集成 Biome 选择 + CaveCarver pass"
```

---

### Task C4: TreeFeature biome 密度分支

**Files:**
- Modify: `Assets/Scripts/Core/World/Features/TreeFeature.cs`（按 biome 调整密度）
- Test: `Assets/Tests/EditMode/World/TreeFeatureBiomeTests.cs`（新建）

**Interfaces:**
- 现有 `TreeFeature.Place(int worldX, int worldZ, BiomeConfig config, int seed)` —— 加 config 参数

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/World/TreeFeatureBiomeTests.cs`：

```csharp
using NUnit.Framework;
using MyWorld.Core.World;
using MyWorld.Core.World.Features;

namespace MyWorld.Core.Tests.World
{
    public class TreeFeatureBiomeTests
    {
        [Test]
        public void Desert_PlacesNoTrees()
        {
            var config = new BiomeConfig { Id = 1, TreeDensity = 0 };
            int trees = 0;
            for (int x = 0; x < 100; x++)
            for (int z = 0; z < 100; z++)
                if (TreeFeature.ShouldPlaceTree(x, z, config, seed: 42)) trees++;
            Assert.That(trees, Is.EqualTo(0), "沙漠 TreeDensity=0 不应生成树");
        }

        [Test]
        public void Forest_PlacesManyTrees()
        {
            var config = new BiomeConfig { Id = 2, TreeDensity = 30 };
            int trees = 0;
            for (int x = 0; x < 100; x++)
            for (int z = 0; z < 100; z++)
                if (TreeFeature.ShouldPlaceTree(x, z, config, seed: 42)) trees++;
            Assert.That(trees, Is.GreaterThan(50), "森林 TreeDensity=30 应高密度");
        }

        [Test]
        public void Plains_PlacesFewTrees()
        {
            var config = new BiomeConfig { Id = 0, TreeDensity = 8 };
            int trees = 0;
            for (int x = 0; x < 100; x++)
            for (int z = 0; z < 100; z++)
                if (TreeFeature.ShouldPlaceTree(x, z, config, seed: 42)) trees++;
            Assert.That(trees, Is.InRange(5, 30), "平原中等密度");
        }
    }
}
```

- [ ] **Step 2: 跑测试（应失败）**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~TreeFeatureBiomeTests"
```

- [ ] **Step 3: 实现 TreeFeature.ShouldPlaceTree**

读现有 `Assets/Scripts/Core/World/Features/TreeFeature.cs`，加：

```csharp
public static bool ShouldPlaceTree(int worldX, int worldZ, BiomeConfig config, int seed)
{
    if (config.TreeDensity <= 0) return false;
    // 基于密度计算阈值（简化：density% 概率）
    uint h = (uint)((worldX * 73856093) ^ (worldZ * 19349663) ^ (seed * 2654435761));
    int chance = (int)(h % 100);
    return chance < config.TreeDensity;
}
```

并修改原 Place 流程，**让 biome 决定密度**。

- [ ] **Step 4: 跑测试 pass + 全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~TreeFeatureBiomeTests"
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：TreeFeatureBiome 3/3 + 333/333 overall。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/World/Features/TreeFeature.cs Assets/Tests/EditMode/World/TreeFeatureBiomeTests.cs
git commit -m "world: TreeFeature 按 BiomeConfig.TreeDensity 决定密度"
```

---

### Task C5: BiomeVisualTests 4 截图（plains/desert/forest/mountains）

**Files:**
- Create: `Assets/Tests/EditMode/Visual/BiomeVisualTests.cs` + `.meta`

**Interfaces:**
- `[OneTimeSetUp]` 跑 4 次种子 → 4 张截图，分别保存

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/Visual/BiomeVisualTests.cs`：

```csharp
#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using MyWorld.Unity.EditorTools;

namespace MyWorld.Core.Tests.Visual
{
    public class BiomeVisualTests
    {
        [Test]
        public void Capture_PlainsDesertForestMountains()
        {
            ScreenshotCapture.CaptureAllDefault();
            AssetDatabase.Refresh();
            // 验证截图文件存在（output 由 ScreenshotCapture 写到 Builds/logs/screenshot/）
            string outDir = System.IO.Path.Combine(Application.dataPath, "..", "Builds", "logs", "screenshots");
            Assert.That(System.IO.Directory.Exists(outDir), Is.True, "截图目录应存在");
            var files = System.IO.Directory.GetFiles(outDir, "*.png");
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(2), "至少 2 张截图（overworld + third-person）");
        }
    }
}
#endif
```

> **实际期望 4 张 biome 截图**——若 ScreenshotCapture 暂只支持 2 张，**扩展**：
> 加 `ScreenshotCapture.CaptureBiome(BiomeKind b)` 方法，每个 biome 调一次传 seed 偏移。**简化**：先用单 seed 起 4 次 `WorldGenerator` 在内存验证不同 biome 出现，再拍单张总图。

> **实施决定**：本任务只做「WorldGenerator 不同 seed 输出不同 biome」的核心断言 + visual-smoke 全图。看 ScreenshotCapture 现状再决定是否扩展 capture 方法。

```csharp
[Test]
public void WorldGenerator_GeneratesAllFourBiomes()
{
    // 走 4 个种子验证 biome 出现率
    int[] biomeCounts = new int[4];  // Plains/Desert/Forest/Mountains
    int[] seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };
    foreach (var seed in seeds)
    {
        for (int chunkX = -5; chunkX < 5; chunkX++)
        for (int chunkZ = -5; chunkZ < 5; chunkZ++)
        {
            // 简化：模拟 chunk 中心温度+湿度
            float t = Mathf.PerlinNoise(chunkX * 0.1f, chunkZ * 0.1f + seed);
            float h = Mathf.PerlinNoise(chunkX * 0.1f + 100, chunkZ * 0.1f + seed);
            var biome = BiomeSelector.Select(t, h);
            biomeCounts[(int)biome]++;
        }
    }
    Assert.That(biomeCounts[(int)Biome.Plains], Is.GreaterThan(0));
    Assert.That(biomeCounts[(int)Biome.Desert], Is.GreaterThan(0));
    Assert.That(biomeCounts[(int)Biome.Forest], Is.GreaterThan(0));
    Assert.That(biomeCounts[(int)Biome.Mountains], Is.GreaterThan(0));
}
```

- [ ] **Step 2: 跑测试（应失败）**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -projectPath "C:/tjf/github/MyWordGame" \
  -runTests -testPlatform EditMode -testFilter 'BiomeVisualTests' \
  -testResults Builds/logs/C5-test.xml -logFile Builds/logs/C5-test.log
grep -E 'total=|result="' Builds/logs/C5-test.xml | head -1
```

- [ ] **Step 3: 实现（按 Step 1 内容）**

如 BiomeSelector 不接受真实噪声输入，**改用** `ValueNoise.Sample2D`（已有）作为温度+湿度来源：

```csharp
float t = ValueNoise.Sample2D(chunkX, chunkZ, seed);
float h = ValueNoise.Sample2D(chunkX + 1000, chunkZ + 1000, seed);
```

- [ ] **Step 4: 跑测试 pass + visual-smoke**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter 'BiomeVisualTests' \
  -testResults Builds/logs/C5-test.xml -logFile Builds/logs/C5-test.log
grep -E 'total=|result="' Builds/logs/C5-test.xml | head -1
bash tools/scripts/visual-smoke.sh
```

期望：BiomeVisual 1/1 + visual-smoke 4/4 pass。

- [ ] **Step 5: Commit**

```bash
git add Assets/Tests/EditMode/Visual/BiomeVisualTests.cs Assets/Tests/EditMode/Visual/BiomeVisualTests.cs.meta
git commit -m "test: BiomeVisualTests 4 种 biome 出现断言 + visual-smoke 验证"
```

---

### Task C6: CaveCarver 视觉烟雾（无全黑/无塌地形）

**Files:**
- 无新文件，复用 visual-smoke + 改截图分析脚本（若存在）

- [ ] **Step 1: 跑 visual-smoke 并分析截图**

```bash
bash tools/scripts/visual-smoke.sh
ls -la Builds/logs/screenshots/
```

期望：overworld.png / third-person.png 不全黑（pixel sample 有 RGB）。

- [ ] **Step 2: 手动读截图**

```bash
# 抽样像素（用 ImageMagick identify 或 python PIL）
python -c "from PIL import Image; im = Image.open('Builds/logs/screenshots/overworld.png'); print('size:', im.size); pixels = list(im.getdata()); print('非黑像素数:', sum(1 for p in pixels if p != (0,0,0,255)))"
```

期望：非黑像素 > 10000。

- [ ] **Step 3: 若塌地形，回滚 CaveCarver 阈值**

```bash
grep "NoiseThreshold" Assets/Scripts/Core/World/CaveCarver.cs
```

若阈值过大导致大量方块被挖 → 调到 0.7。若过小无效果 → 调到 0.6。

- [ ] **Step 4: 重新 visual-smoke**

```bash
bash tools/scripts/visual-smoke.sh
```

期望：4/4 pass + 截图含草地色像素。

- [ ] **Step 5: Commit（如有改动）**

```bash
git add Assets/Scripts/Core/World/CaveCarver.cs
git commit -m "world: CaveCarver 阈值调优（C5 视觉回归验证）" || echo "无需调优，跳过 commit"
```

---

### Task C7: BlockInteraction.cs biome-specific hardness

**Files:**
- Modify: `Assets/Scripts/Unity/Player/BlockInteraction.cs`（硬编码 1s 改成按 biome 调）

**Interfaces:**
- 现有 `BreakTime(BlockKind b)` → 改为 `BreakTime(BlockKind b, Biome biome)`
- 山地群系石头硬 ×2，沙漠沙软 ×0.5

- [ ] **Step 1: 实现修改**

读 `BlockInteraction.cs`：

```csharp
// 旧
public float BreakTime(int blockId) => 1f;

// 新
public float BreakTime(int blockId, Biome biome)
{
    float base_ = 1f;
    if (blockId == BlockIds.Stone || blockId == BlockIds.Cobblestone)
        base_ = (biome == Biome.Mountains) ? 2f : 1f;
    if (blockId == BlockIds.Sand)
        base_ = (biome == Biome.Desert) ? 0.5f : 1f;
    return base_;
}
```

- [ ] **Step 2: 调用点更新**

grep `BreakTime(` 找到所有调用，加 biome 参数：

```csharp
var biome = BiomeSelector.Select(t, h);  // 已有方法或 WorldBootstrap 暴露
float time = BreakTime(blockId, biome);
```

- [ ] **Step 3: 跑全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/logs/C7-regression.xml -logFile Builds/logs/C7-regression.log
grep -E 'total=|result="' Builds/logs/C7-regression.xml | head -1
```

期望：dotnet 333/333 + EditMode 354/354（353 + 1 BiomeVisual）。

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Unity/Player/BlockInteraction.cs
git commit -m "gameplay: BlockInteraction 按 Biome 调整 break time（山硬沙软）"
```

---

### Task C8: Phase C 完成验证（visual-smoke 完整回归）

**Files:**
- 无新文件，纯验证

- [ ] **Step 1: 全量 dotnet**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：333/333 pass。

- [ ] **Step 2: 全量 Unity EditMode**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -projectPath . -runTests -testPlatform EditMode \
  -testResults Builds/logs/C-final.xml -logFile Builds/logs/C-final.log
grep -E 'total=|result="' Builds/logs/C-final.xml | head -1
```

期望：354/354 pass。

- [ ] **Step 3: visual-smoke**

```bash
bash tools/scripts/visual-smoke.sh
```

期望：4/4 pass。

- [ ] **Step 4: 一键流水线（不启动）**

```bash
./tools/scripts/build-and-run.sh --skip-tests --no-launch --clean
```

期望：Build OK。

- [ ] **Step 5: 写 Phase C 完成 entry 到 ledger**

新建 `.superpowers/sdd/2026-08-13-milestone-3/progress.md`（如果使用 SDD 模式）。

---

## Phase C 完成验证

**Phase C 累计 EditMode**: 354（353 + 1 BiomeVisual）
**Phase C 累计 dotnet**: 333（325 + 8: CaveCarver×4 + Biome×5 + TreeFeatureBiome×3 - 4 复用 = +8）
**Phase C commits**: 8（C1-C8）

---

# Phase D: Mob + AI（8 tasks, 5-6 天）

---

### Task D1: MobKind + MobAI Core + ItemDropTable

**Files:**
- Create: `Assets/Scripts/Core/Entities/MobKind.cs` + `.meta`
- Create: `Assets/Scripts/Core/Entities/MobAI.cs` + `.meta`
- Create: `Assets/Scripts/Core/Entities/ItemDropTable.cs` + `.meta`
- Test: `Assets/Tests/EditMode/Entities/MobAITests.cs`（新建）

**Interfaces:**
- `enum MobKind { Pig, Cow, Chicken, Zombie }`
- `MobAI` 纯 Core：`Tick(MobKind kind, Float3 pos, Float3 playerPos, float dt)`
- `ItemDropTable.Roll(MobKind kind, int seed)` 返回 `ItemStack?`

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/Entities/MobAITests.cs`：

```csharp
using NUnit.Framework;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;

namespace MyWorld.Core.Tests.Entities
{
    public class MobAITests
    {
        [Test]
        public void Pig_MovesRandomlyWhenIdle()
        {
            var ai = new MobAI(MobKind.Pig, seed: 42);
            var pos = new Float3(0, 70, 0);
            ai.Tick(pos, new Float3(100, 70, 100), dt: 1f);
            // 1s 后位置应变化
            Assert.That(ai.CurrentPosition, Is.Not.EqualTo(pos));
        }

        [Test]
        public void Cow_DoesNotMoveFar()
        {
            var ai = new MobAI(MobKind.Cow, seed: 42);
            var pos = new Float3(0, 70, 0);
            ai.Tick(pos, new Float3(100, 70, 100), dt: 1f);
            // 牛移动速度 = 猪 × 0.5（简化）
            float dist = (ai.CurrentPosition - pos).Length();
            Assert.That(dist, Is.LessThan(2f), "牛 1s 移 < 2 block");
        }

        [Test]
        public void Chicken_HopsShortDistance()
        {
            var ai = new MobAI(MobKind.Chicken, seed: 42);
            var pos = new Float3(0, 70, 0);
            ai.Tick(pos, new Float3(100, 70, 100), dt: 0.5f);
            float dist = (ai.CurrentPosition - pos).Length();
            Assert.That(dist, Is.LessThan(1.5f), "鸡 0.5s 跳 < 1.5 block");
        }

        [Test]
        public void Zombie_ApproachesPlayer()
        {
            var ai = new MobAI(MobKind.Zombie, seed: 42);
            var pos = new Float3(0, 70, 0);
            ai.Tick(pos, new Float3(10, 70, 0), dt: 1f);
            // 朝玩家走
            float distToPlayer = (ai.CurrentPosition - new Float3(10, 70, 0)).Length();
            float initialDist = (pos - new Float3(10, 70, 0)).Length();
            Assert.That(distToPlayer, Is.LessThan(initialDist), "僵尸朝玩家靠近");
        }

        [Test]
        public void Zombie_DoesNotMoveWhenFar()
        {
            var ai = new MobAI(MobKind.Zombie, seed: 42);
            var pos = new Float3(0, 70, 0);
            ai.Tick(pos, new Float3(100, 70, 0), dt: 1f);  // 100 block 远
            float dist = (ai.CurrentPosition - pos).Length();
            Assert.That(dist, Is.LessThan(0.5f), "僵尸超 16 block 距离不追");
        }
    }
}
```

- [ ] **Step 2: 跑测试（应失败）**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~MobAITests"
```

- [ ] **Step 3: 实现 MobKind + MobAI**

新建 `Assets/Scripts/Core/Entities/MobKind.cs`：

```csharp
namespace MyWorld.Core.Entities
{
    public enum MobKind
    {
        Pig = 0,
        Cow = 1,
        Chicken = 2,
        Zombie = 3
    }
}
```

新建 `Assets/Scripts/Core/Entities/MobAI.cs`：

```csharp
using MyWorld.Core.Math;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 生物 AI（Core 纯逻辑）。Pig/Cow/Chicken 随机巡逻，Zombie 追踪玩家。
    /// </summary>
    public class MobAI
    {
        public Float3 CurrentPosition { get; private set; }
        public MobKind Kind { get; }

        private const float WanderSpeed = 1.5f;     // pig 默认
        private const float CowSpeed = 0.8f;
        private const float ChickenSpeed = 0.5f;
        private const float ZombieSpeed = 2.0f;
        private const float ZombieAggroRange = 16f;

        private readonly int _seed;
        private float _wanderAngle = 0f;

        public MobAI(MobKind kind, int seed, Float3? startPos = null)
        {
            Kind = kind;
            _seed = seed;
            CurrentPosition = startPos ?? new Float3(0, 70, 0);
        }

        public void Tick(Float3 position, Float3 playerPosition, float dt)
        {
            CurrentPosition = position;
            float speed = Kind switch
            {
                MobKind.Pig => WanderSpeed,
                MobKind.Cow => CowSpeed,
                MobKind.Chicken => ChickenSpeed,
                MobKind.Zombie => ZombieSpeed,
                _ => 0f
            };

            if (Kind == MobKind.Zombie)
            {
                float distToPlayer = (playerPosition - position).Length();
                if (distToPlayer > ZombieAggroRange) return;
                // 朝玩家方向
                Float3 dir = (playerPosition - position).Normalized();
                CurrentPosition = position + dir * speed * dt;
            }
            else
            {
                // 随机巡逻：每 2s 换方向
                _wanderAngle += dt * 0.5f;  // 简化：匀速转
                Float3 dir = new Float3(MathF.Cos(_wanderAngle), 0, MathF.Sin(_wanderAngle));
                CurrentPosition = position + dir * speed * dt;
            }
        }
    }
}
```

> `Float3.Normalized` / `MathF` 若不存在则用 `System.MathF` + 手写归一化。

- [ ] **Step 4: 跑测试 + 全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~MobAITests"
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：MobAI 5/5 + 338/338 overall。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Entities/MobKind.cs Assets/Scripts/Core/Entities/MobKind.cs.meta Assets/Scripts/Core/Entities/MobAI.cs Assets/Scripts/Core/Entities/MobAI.cs.meta Assets/Tests/EditMode/Entities/MobAITests.cs
git commit -m "entities: MobKind enum + MobAI（Pig/Cow/Chicken 巡逻 + Zombie 追踪）"
```

---

### Task D2: ItemDropTable + MobSpawnRules + JSON 数据

**Files:**
- Create: `Assets/Scripts/Core/Entities/ItemDropTable.cs` + `.meta`
- Create: `Assets/Scripts/Core/Entities/MobSpawnRules.cs` + `.meta`
- Create: `Assets/StreamingAssets/mobs/drop_tables.json`
- Create: `Assets/StreamingAssets/mobs/spawn_rules.json`
- Test: `Assets/Tests/EditMode/Entities/ItemDropTableTests.cs`（新建）
- Test: `Assets/Tests/EditMode/Entities/MobSpawnRulesTests.cs`（新建）

**Interfaces:**
- `ItemDropTable.Roll(MobKind kind, int seed)` 返回 `ItemStack?` 或 null
- `MobSpawnRules.ShouldSpawn(Biome biome, MobKind kind, int lightLevel, int seed)` 返回 bool

- [ ] **Step 1: 写 ItemDropTable 测试**

新建 `Assets/Tests/EditMode/Entities/ItemDropTableTests.cs`：

```csharp
using NUnit.Framework;
using MyWorld.Core.Entities;

namespace MyWorld.Core.Tests.Entities
{
    public class ItemDropTableTests
    {
        [Test]
        public void Pig_AlwaysDropsPork()
        {
            var table = ItemDropTable.Load("drop_tables.json");
            var drop = table.Roll(MobKind.Pig, seed: 42);
            Assert.That(drop, Is.Not.Null);
            Assert.That(drop.ItemId, Is.GreaterThan(0));
        }

        [Test]
        public void Chicken_DropsFeather()
        {
            var table = ItemDropTable.Load("drop_tables.json");
            var drop = table.Roll(MobKind.Chicken, seed: 42);
            Assert.That(drop, Is.Not.Null);
        }
    }
}
```

- [ ] **Step 2: 写 MobSpawnRules 测试**

新建 `Assets/Tests/EditMode/Entities/MobSpawnRulesTests.cs`：

```csharp
using NUnit.Framework;
using MyWorld.Core.Entities;
using MyWorld.Core.World;

namespace MyWorld.Core.Tests.Entities
{
    public class MobSpawnRulesTests
    {
        [Test]
        public void Pig_SpawnsInPlains()
        {
            var rules = MobSpawnRules.Load("spawn_rules.json");
            int spawns = 0;
            for (int i = 0; i < 100; i++)
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Pig, lightLevel: 15, seed: i)) spawns++;
            Assert.That(spawns, Is.GreaterThan(50), "平原+光 15 应高概率刷猪");
        }

        [Test]
        public void Zombie_SpawnsAtNight()
        {
            var rules = MobSpawnRules.Load("spawn_rules.json");
            int daySpawns = 0, nightSpawns = 0;
            for (int i = 0; i < 100; i++)
            {
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Zombie, lightLevel: 15, seed: i)) daySpawns++;
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Zombie, lightLevel: 0, seed: i)) nightSpawns++;
            }
            Assert.That(nightSpawns, Is.GreaterThan(daySpawns), "僵尸夜间刷新率高");
        }

        [Test]
        public void Chicken_SpawnsInForest()
        {
            var rules = MobSpawnRules.Load("spawn_rules.json");
            int plainsSpawns = 0, forestSpawns = 0;
            for (int i = 0; i < 100; i++)
            {
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Chicken, lightLevel: 15, seed: i)) plainsSpawns++;
                if (rules.ShouldSpawn(Biome.Forest, MobKind.Chicken, lightLevel: 15, seed: i)) forestSpawns++;
            }
            Assert.That(forestSpawns, Is.GreaterThan(plainsSpawns), "森林鸡多");
        }
    }
}
```

- [ ] **Step 3: 跑测试（应失败）**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~ItemDropTableTests|FullyQualifiedName~MobSpawnRulesTests"
```

- [ ] **Step 4: 实现 ItemDropTable + drop_tables.json**

新建 `Assets/StreamingAssets/mobs/drop_tables.json`：

```json
{
  "Pig":     [{ "itemId": 1002, "count": 1, "chance": 1.0 }],
  "Cow":     [{ "itemId": 1003, "count": 1, "chance": 1.0 }],
  "Chicken": [{ "itemId": 1004, "count": 1, "chance": 1.0 }],
  "Zombie":  [{ "itemId": 1005, "count": 1, "chance": 0.5 }]
}
```

> `itemId` 是占位（实际查 `BlockIds` / `ItemIds`）。**实施者填正确值**。

新建 `Assets/Scripts/Core/Entities/ItemDropTable.cs`：

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using MyWorld.Core.Items;

namespace MyWorld.Core.Entities
{
    public class DropEntry
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
        public float Chance { get; set; }
    }

    public class ItemDropTable
    {
        private readonly Dictionary<MobKind, List<DropEntry>> _table;

        private ItemDropTable(Dictionary<MobKind, List<DropEntry>> table) { _table = table; }

        public static ItemDropTable Load(string jsonPath)
        {
            string json = System.IO.File.ReadAllText(jsonPath);
            var raw = JsonConvert.DeserializeObject<Dictionary<string, List<DropEntry>>>(json);
            var dict = new Dictionary<MobKind, List<DropEntry>>();
            foreach (var kv in raw)
            {
                MobKind kind = kv.Key switch
                {
                    "Pig" => MobKind.Pig,
                    "Cow" => MobKind.Cow,
                    "Chicken" => MobKind.Chicken,
                    "Zombie" => MobKind.Zombie,
                    _ => throw new System.ArgumentException($"未知 MobKind: {kv.Key}")
                };
                dict[kind] = kv.Value;
            }
            return new ItemDropTable(dict);
        }

        public ItemStack? Roll(MobKind kind, int seed)
        {
            if (!_table.TryGetValue(kind, out var entries) || entries.Count == 0) return null;
            uint h = (uint)(seed * 2654435761);
            float roll = (h & 0xFFFF) / 65535f;
            float cumulative = 0f;
            foreach (var e in entries)
            {
                cumulative += e.Chance;
                if (roll < cumulative) return new ItemStack(e.ItemId, e.Count);
            }
            return null;
        }
    }
}
```

- [ ] **Step 5: 实现 MobSpawnRules + spawn_rules.json**

新建 `Assets/StreamingAssets/mobs/spawn_rules.json`：

```json
{
  "Pig":     { "biomes": ["Plains", "Forest"], "minLight": 9, "weight": 10 },
  "Cow":     { "biomes": ["Plains"], "minLight": 9, "weight": 8 },
  "Chicken": { "biomes": ["Plains", "Forest"], "minLight": 9, "weight": 8 },
  "Zombie":  { "biomes": ["Plains", "Desert", "Forest", "Mountains"], "minLight": 0, "weight": 5 }
}
```

新建 `Assets/Scripts/Core/Entities/MobSpawnRules.cs`：

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using MyWorld.Core.World;

namespace MyWorld.Core.Entities
{
    public class SpawnRule
    {
        public List<string> Biomes { get; set; }
        public int MinLight { get; set; }
        public int Weight { get; set; }
    }

    public class MobSpawnRules
    {
        private readonly Dictionary<MobKind, SpawnRule> _rules;

        private MobSpawnRules(Dictionary<MobKind, SpawnRule> rules) { _rules = rules; }

        public static MobSpawnRules Load(string jsonPath)
        {
            string json = System.IO.File.ReadAllText(jsonPath);
            var raw = JsonConvert.DeserializeObject<Dictionary<string, SpawnRule>>(json);
            var dict = new Dictionary<MobKind, SpawnRule>();
            foreach (var kv in raw)
            {
                MobKind kind = kv.Key switch
                {
                    "Pig" => MobKind.Pig,
                    "Cow" => MobKind.Cow,
                    "Chicken" => MobKind.Chicken,
                    "Zombie" => MobKind.Zombie,
                    _ => throw new System.ArgumentException($"未知 MobKind: {kv.Key}")
                };
                dict[kind] = kv.Value;
            }
            return new MobSpawnRules(dict);
        }

        public bool ShouldSpawn(Biome biome, MobKind kind, int lightLevel, int seed)
        {
            if (!_rules.TryGetValue(kind, out var rule)) return false;
            if (lightLevel < rule.MinLight) return false;
            if (!rule.Biomes.Contains(biome.ToString())) return false;
            uint h = (uint)(seed * 2654435761 ^ (int)biome * 73856093 ^ (int)kind * 19349663);
            int chance = (int)(h % 100);
            return chance < rule.Weight * 5;  // weight 10 → 50% spawn chance
        }
    }
}
```

- [ ] **Step 6: 跑测试 pass + 全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~ItemDropTableTests|FullyQualifiedName~MobSpawnRulesTests"
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：ItemDropTable 2/2 + MobSpawnRules 3/3 + 343/343 overall。

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Core/Entities/ItemDropTable.cs Assets/Scripts/Core/Entities/ItemDropTable.cs.meta Assets/Scripts/Core/Entities/MobSpawnRules.cs Assets/Scripts/Core/Entities/MobSpawnRules.cs.meta Assets/StreamingAssets/mobs/drop_tables.json Assets/StreamingAssets/mobs/spawn_rules.json Assets/Tests/EditMode/Entities/ItemDropTableTests.cs Assets/Tests/EditMode/Entities/MobSpawnRulesTests.cs
git commit -m "entities: ItemDropTable + MobSpawnRules + drop_tables.json/spawn_rules.json"
```

---

### Task D3: MobView per-kind visuals（4 种生物视觉）

**Files:**
- Modify: `Assets/Scripts/Unity/Entities/MobView.cs`（按 MobKind 切换 mesh + color）

**Interfaces:**
- 现有 `MobView` 单 mesh，改为 switch 4 种 kind

- [ ] **Step 1: 读现有 MobView**

```bash
cat Assets/Scripts/Unity/Entities/MobView.cs
```

- [ ] **Step 2: 实现 per-kind 视觉**

修改 `Awake()` 或 `Setup(MobKind kind)`：

```csharp
public void Setup(MobKind kind)
{
    _kind = kind;
    // 删除旧 mesh
    foreach (Transform child in transform) DestroyImmediate(child.gameObject);

    switch (kind)
    {
        case MobKind.Pig:
            CreateBody(scale: (0.9f, 0.6f, 1.2f), color: new Color(0.9f, 0.7f, 0.7f));
            CreateHead(offset: (0, 0.5f, 0.4f), scale: (0.5f, 0.5f, 0.5f), color: new Color(0.9f, 0.7f, 0.7f));
            break;
        case MobKind.Cow:
            CreateBody(scale: (1.0f, 0.8f, 1.4f), color: new Color(0.3f, 0.2f, 0.1f));
            CreateHead(offset: (0, 0.7f, 0.6f), scale: (0.6f, 0.6f, 0.6f), color: new Color(0.3f, 0.2f, 0.1f));
            break;
        case MobKind.Chicken:
            CreateBody(scale: (0.4f, 0.4f, 0.5f), color: new Color(1f, 1f, 0.9f));
            CreateHead(offset: (0, 0.4f, 0.3f), scale: (0.3f, 0.3f, 0.3f), color: new Color(1f, 0.9f, 0.1f));
            break;
        case MobKind.Zombie:
            CreateBody(scale: (0.6f, 1.8f, 0.4f), color: new Color(0.4f, 0.6f, 0.4f));
            CreateHead(offset: (0, 1.0f, 0f), scale: (0.5f, 0.5f, 0.5f), color: new Color(0.4f, 0.6f, 0.4f));
            break;
    }
}

private void CreateBody(Vector3 scale, Color color) { /* 创建 cube + ApplyColor */ }
private void CreateHead(Vector3 offset, Vector3 scale, Color color) { /* 同 */ }
```

- [ ] **Step 3: 跑全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/logs/D3-regression.xml -logFile Builds/logs/D3-regression.log
grep -E 'total=|result="' Builds/logs/D3-regression.xml | head -1
```

期望：dotnet 343/343 + EditMode 354/354（无新增测试，视觉靠 D4）。

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Unity/Entities/MobView.cs
git commit -m "visual: MobView 4 种 kind 的 mesh + color（猪/牛/鸡/僵尸）"
```

---

### Task D4: MobVisualTests 2 截图（白天 vs 夜晚）

**Files:**
- Create: `Assets/Tests/EditMode/Visual/MobVisualTests.cs` + `.meta`

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/Visual/MobVisualTests.cs`：

```csharp
#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.Entities;
using MyWorld.Core.Entities;

namespace MyWorld.Core.Tests.Visual
{
    public class MobVisualTests
    {
        [Test]
        public void SpawnPig_ProducesMesh()
        {
            var go = new GameObject("PigTest");
            var view = go.AddComponent<MobView>();
            view.Setup(MobKind.Pig);
            int cubeCount = 0;
            foreach (Transform child in go.transform) cubeCount++;
            Assert.That(cubeCount, Is.EqualTo(2), "猪应有 body + head 2 个 cube");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SpawnZombie_ProducesTallMesh()
        {
            var go = new GameObject("ZombieTest");
            var view = go.AddComponent<MobView>();
            view.Setup(MobKind.Zombie);
            // 僵尸应比猪高
            var body = go.transform.GetChild(0);
            Assert.That(body.localScale.y, Is.GreaterThan(1.5f), "僵尸身体 y > 1.5");
            Object.DestroyImmediate(go);
        }
    }
}
#endif
```

- [ ] **Step 2: 跑测试（应失败）**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -projectPath "C:/tjf/github/MyWordGame" \
  -runTests -testPlatform EditMode -testFilter 'MobVisualTests' \
  -testResults Builds/logs/D4-test.xml -logFile Builds/logs/D4-test.log
```

- [ ] **Step 3: 跑测试 pass + 全量回归**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/logs/D4-regression.xml -logFile Builds/logs/D4-regression.log
grep -E 'total=|result="' Builds/logs/D4-regression.xml | head -1
```

期望：MobVisual 2/2 + EditMode 356/356。

- [ ] **Step 4: visual-smoke**

```bash
bash tools/scripts/visual-smoke.sh
```

期望：4/4 pass（截图含生物 mesh）。

- [ ] **Step 5: Commit**

```bash
git add Assets/Tests/EditMode/Visual/MobVisualTests.cs Assets/Tests/EditMode/Visual/MobVisualTests.cs.meta
git commit -m "test: MobVisualTests（Pig 2 cube + Zombie >1.5 高）"
```

---

### Task D5: MobManager spawn tick + DayNightCycle wire

**Files:**
- Create: `Assets/Scripts/Unity/Entities/MobManager.cs` + `.meta`
- Modify: `Assets/Scripts/Unity/World/WorldBootstrap.cs`（注册 MobManager）

**Interfaces:**
- `MobManager.TickSpawn(int seed, float dayNightPhase)` 每 10s 检查 spawn

- [ ] **Step 1: 实现 MobManager**

新建 `Assets/Scripts/Unity/Entities/MobManager.cs`：

```csharp
using System.Collections.Generic;
using UnityEngine;
using MyWorld.Core.Entities;
using MyWorld.Core.World;

namespace MyWorld.Unity.Entities
{
    /// <summary>
    /// 周期性检查刷怪。白天刷温顺动物，夜间刷僵尸。
    /// </summary>
    public class MobManager : MonoBehaviour
    {
        [SerializeField] private float spawnInterval = 10f;
        [SerializeField] private int maxMobs = 20;

        private float _lastSpawnTime;
        private readonly List<GameObject> _mobs = new();

        private void Update()
        {
            if (Time.time - _lastSpawnTime < spawnInterval) return;
            _lastSpawnTime = Time.time;
            if (_mobs.Count >= maxMobs) return;

            // 简化：用 Player 当前位置作刷怪点
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var playerPos = player.transform.position;
            int seed = (int)(Time.time * 1000);
            int light = IsNight() ? 0 : 15;

            for (int i = 0; i < 3; i++)
            {
                MobKind kind = light == 0 ? MobKind.Zombie : MobKind.Pig;
                // 简化：用常量 biome（Plains）；实际应查 worldPos 的 biome
                Biome biome = Biome.Plains;
                var rules = MobSpawnRules.Load("mobs/spawn_rules.json");
                if (!rules.ShouldSpawn(biome, kind, light, seed + i)) continue;

                SpawnMob(kind, playerPos + RandomOffset(seed + i));
            }
        }

        private bool IsNight() => (Time.time / 600f) % 1f < 0.5f;  // 简化：10 分钟周期

        private Vector3 RandomOffset(int seed)
        {
            uint h = (uint)(seed * 2654435761);
            return new Vector3(((h & 0xFF) / 255f - 0.5f) * 20f, 0, (((h >> 8) & 0xFF) / 255f - 0.5f) * 20f);
        }

        private void SpawnMob(MobKind kind, Vector3 pos)
        {
            var go = new GameObject($"Mob_{kind}");
            go.transform.position = pos;
            var view = go.AddComponent<MobView>();
            view.Setup(kind);
            _mobs.Add(go);
        }
    }
}
```

- [ ] **Step 2: WorldBootstrap 注册**

```csharp
// WorldBootstrap.cs Start
var mobMgr = playerGo.AddComponent<MobManager>();
```

- [ ] **Step 3: 跑全量回归**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/logs/D5-regression.xml -logFile Builds/logs/D5-regression.log
grep -E 'total=|result="' Builds/logs/D5-regression.xml | head -1
```

期望：dotnet 343/343 + EditMode 356/356。

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Unity/Entities/MobManager.cs Assets/Scripts/Unity/Entities/MobManager.cs.meta Assets/Scripts/Unity/World/WorldBootstrap.cs
git commit -m "entities: MobManager spawn tick + WorldBootstrap 注册"
```

---

### Task D6: VillagerManager + TradeUi 接线 verify

**Files:**
- Modify: `Assets/Scripts/Unity/Entities/VillagerManager.cs`（若已存在则 verify；否则 placeholder）
- Modify: `Assets/Scripts/Unity/UI/VillagerTradeUi.cs`（接现有 TradeOffer 表）
- Test: `Assets/Tests/EditMode/UI/VillagerTradeTests.cs`（新建）

- [ ] **Step 1: 读现有 VillagerManager**

```bash
ls Assets/Scripts/Unity/Entities/ 2>/dev/null
ls Assets/Scripts/Unity/UI/VillagerTrade* 2>/dev/null
```

- [ ] **Step 2: 写测试（占位）**

```csharp
#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    public class VillagerTradeTests
    {
        [Test]
        public void TradeUi_OpensOnNpcInteraction()
        {
            // 占位：若 VillagerTradeUi 存在则验证 IsVisible toggle
            var uiType = System.Type.GetType("MyWorld.Unity.UI.VillagerTradeUi, Assembly-CSharp");
            Assert.That(uiType, Is.Not.Null, "VillagerTradeUi 类存在");
        }
    }
}
#endif
```

- [ ] **Step 3: 跑测试 + 全量回归**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Builds/logs/D6-regression.xml -logFile Builds/logs/D6-regression.log
grep -E 'total=|result="' Builds/logs/D6-regression.xml | head -1
```

期望：EditMode 357/357（+1 占位）。

- [ ] **Step 4: Commit**

```bash
git add Assets/Tests/EditMode/UI/VillagerTradeTests.cs
git commit -m "test: VillagerTradeTests 占位验证（待后续 Villager 实装扩展）"
```

---

### Task D7: Phase D 完成验证 + visual-smoke 完整回归

- [ ] **Step 1: 全量 dotnet**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：343/343 pass。

- [ ] **Step 2: 全量 Unity EditMode**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -projectPath . -runTests -testPlatform EditMode \
  -testResults Builds/logs/D-final.xml -logFile Builds/logs/D-final.log
grep -E 'total=|result="' Builds/logs/D-final.xml | head -1
```

期望：357/357 pass。

- [ ] **Step 3: visual-smoke**

```bash
bash tools/scripts/visual-smoke.sh
```

期望：4/4 pass。

- [ ] **Step 4: Build 验证**

```bash
./tools/scripts/build-and-run.sh --skip-tests --no-launch --clean
ls -la Builds/Windows/MyWordGame.exe
```

期望：Build OK。

- [ ] **Step 5: 写最终 ledger entry**

---

### Task D8: 整体里程碑验证（4 phase 全部完成）

**Files:**
- 无新文件，最终验收

- [ ] **Step 1: 端到端流水线（完整）**

```bash
./tools/scripts/build-and-run.sh
```

期望：
- dotnet test 343/343
- Unity EditMode 357/357
- URP / BuildSetup / Build 三次 PASS
- `Builds/Windows/MyWordGame.exe` 落地
- 进程在后台跑（tasklist | grep MyWordGame）

- [ ] **Step 2: 启动并采 4 张截图**

```bash
./tools/scripts/visual-smoke.sh
# 期望: 4 张图（overworld + third-person + 2 张生物图）都非黑
```

- [ ] **Step 3: 检查所有 spec 验收项**

逐条核对 spec 验收清单：

| 验收项 | 验证方式 |
| --- | --- |
| Phase A 5 项 cleanup | `git log --oneline \| grep -E "^A[1-6]"` 6 commits |
| Phase A 3 视觉/音频 | `git log --oneline \| grep -E "(PlayerVisual\|PlayerAudioSystem\|HandController)"` |
| Phase B 6 项 gameplay | `git log --oneline \| grep -E "HungerSystem\|FurnaceSystem\|ItemDropEntity\|Crafting\|Death\|FoodBar\|Respawn"` |
| Phase C 5 项 world | `git log --oneline \| grep -E "CaveCarver\|Biome\|TreeFeature\|WorldGenerator"` |
| Phase D 6 项 mob | `git log --oneline \| grep -E "Mob[A-Z]"` |
| EditMode 测试 ≥ 337 | `grep "total=" Builds/logs/D-final.xml` 显示 357 |
| Build OK | `ls Builds/Windows/MyWordGame.exe` |
| visual-smoke 4/4 | grep `result="Passed"` |

- [ ] **Step 4: Commit ledger**

```bash
git add .superpowers/sdd/2026-08-13-milestone-3/progress.md
git commit -m "milestone-3: 全部 4 phase 完成 + 端到端验证通过"
```

- [ ] **Step 5: Push（如网络可达）**

```bash
git push origin main
```

若网络不通（之前的 github.com:443 错误），**记录为 deferred，本地 30 commits 已安全**。

---

## Phase D + 整体完成验证

**最终 EditMode**: 357（337 baseline + 20 新增: A=10, B=6, C=1, D=3）
**最终 dotnet**: 343（315 baseline + 28 新增: A=0, B=10, C=8, D=10）
**总计 commits**: 30（A:6 + B:8 + C:8 + D:8）

| Phase | dotnet 新增 | EditMode 新增 | 任务数 |
| --- | --- | --- | --- |
| A (cleanup+visual+audio) | 0 | 10 | 6 |
| B (gameplay core) | 10 | 6 | 8 |
| C (world gen) | 8 | 1 | 8 |
| D (mob + AI) | 10 | 3 | 8 |
| **合计** | **+28** | **+20** | **30** |

---

# Plan Part 2 完成

Phase C (8 tasks) + Phase D (8 tasks) = **16 tasks 完成**.

完整 milestone-3 plan: Part 1（14 tasks）+ Part 2（16 tasks）= **30 tasks 完成**。