# Unity 渲染层实施计划（里程碑 1）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 Core 层已有的体素世界在 Unity 编辑器里真正渲染出来——按下 Play 能用自由飞行相机漫游一片带正确贴图的地形。

**Architecture:** Core 层给贪心网格的每个 quad 打上"贴图索引"，再把索引缓冲按贴图重排成若干连续段（submesh）；Unity 层为每张方块贴图建一个 Repeat 环绕的材质，把这些段一一对应到 `MeshRenderer.sharedMaterials`。Core 依旧零 Unity 依赖，传递的只是 int 索引。

**Tech Stack:** C# 9 / netstandard2.1（Core）、Unity 2022.3.62f3c1、NUnit、内置渲染管线（URP 作为最后一步可回滚接入）

---

## 设计依据

`docs/superpowers/specs/2026-08-07-unity-render-layer-design.md`

## 文件结构

**Core 层修改**（`Assets/Scripts/Core/`）：

| 文件 | 职责 |
| --- | --- |
| `Blocks/BlockFaces.cs`（新建） | 把贪心网格的「轴 + 正负朝向」翻译成 `BlockFace` |
| `Blocks/BlockRegistry.cs`（修改） | 新增贴图索引表：贴图名去重排序后分配 int 索引，并提供 `(numericId, face) → 索引` 查询 |
| `Meshing/IBlockSource.cs`（修改） | 新增 `GetTextureIndex(ushort, BlockFace)` |
| `Voxel/ChunkMeshSource.cs`（修改） | 实现上述方法，转发给 `BlockRegistry` |
| `Meshing/MeshBuffer.cs`（修改） | 新增 `QuadTextures` 列表与 `SplitByTexture` 重排方法 |
| `Meshing/Submesh.cs`（新建） | 索引缓冲中同一张贴图的一段 `(TextureIndex, IndexStart, IndexCount)` |
| `Meshing/GreedyMesher.cs`（修改） | 每产出一个 quad 就记录它的贴图索引 |
| `Voxel/World.cs`（修改） | 新增 `AddChunk`，让世界生成器的产物能整块塞进来 |

**Unity 层新建**（`Assets/Scripts/Unity/`）：

| 文件 | 职责 |
| --- | --- |
| `MyWorld.Unity.asmdef` | 程序集定义，引用 `MyWorld.Core` |
| `Bootstrap/BlockRegistryLoader.cs` | 从 `StreamingAssets/blocks` 读 JSON 建注册表 |
| `Rendering/BlockMaterialLibrary.cs` | 贴图索引 → `Material`。运行时从 PNG 建 `Texture2D` |
| `Rendering/ChunkMeshBuilder.cs` | `MeshBuffer` + `Submesh[]` → `UnityEngine.Mesh` |
| `Rendering/ChunkSectionView.cs` | 一个区块段的 GameObject + Mesh + 材质槽 |
| `Bootstrap/FreeFlyCamera.cs` | 验收用自由飞行相机 |
| `Bootstrap/WorldBootstrap.cs` | 生成固定范围区块并全部建成网格 |
| `Editor/MyWorld.Unity.Editor.asmdef` | 编辑器程序集定义 |
| `Editor/PreviewSceneBuilder.cs` | 生成验收场景，可从菜单点也可批处理调用 |

## 每个任务开始前

在**仓库根目录**执行，确认起点是干净的：

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：`Passed! - Failed: 0, Passed: 155`（任务推进过程中这个数字会变大）

---

## Task 1: 轴向 → 方块面的换算

`GreedyMesher` 按轴（0=X、1=Y、2=Z）遍历，产出的每个 quad 有一个朝向正负。要查贴图就得先把这两者翻译成 `BlockFace`。

**Files:**
- Create: `Assets/Scripts/Core/Blocks/BlockFaces.cs`
- Test: `Assets/Tests/EditMode/Blocks/BlockFacesTests.cs`

- [ ] **Step 1: 写失败的测试**

创建 `Assets/Tests/EditMode/Blocks/BlockFacesTests.cs`：

```csharp
using System;
using MyWorld.Core.Blocks;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    [TestFixture]
    public class BlockFacesTests
    {
        [TestCase(0, true, BlockFace.East)]
        [TestCase(0, false, BlockFace.West)]
        [TestCase(1, true, BlockFace.Top)]
        [TestCase(1, false, BlockFace.Bottom)]
        [TestCase(2, true, BlockFace.North)]
        [TestCase(2, false, BlockFace.South)]
        public void FromAxis_MapsEachAxisAndSignToItsFace(int axis, bool facingPositive, BlockFace expected)
        {
            Assert.That(BlockFaces.FromAxis(axis, facingPositive), Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void FromAxis_WithInvalidAxis_Throws(int axis)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BlockFaces.FromAxis(axis, true));
        }

        [Test]
        public void FromAxis_CoversAllSixFacesWithoutRepeating()
        {
            var seen = new System.Collections.Generic.HashSet<BlockFace>();
            for (var axis = 0; axis < 3; axis++)
            {
                seen.Add(BlockFaces.FromAxis(axis, true));
                seen.Add(BlockFaces.FromAxis(axis, false));
            }

            Assert.That(seen.Count, Is.EqualTo(6), "六个轴向组合必须一一映射到六个不同的面");
        }
    }
}
```

- [ ] **Step 2: 跑测试确认它失败**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockFacesTests"
```

预期：编译失败，报 `未能找到类型或命名空间名"BlockFaces"`

- [ ] **Step 3: 写实现**

创建 `Assets/Scripts/Core/Blocks/BlockFaces.cs`：

```csharp
using System;

namespace MyWorld.Core.Blocks
{
    /// <summary>网格生成用的轴向与方块面之间的换算。</summary>
    public static class BlockFaces
    {
        /// <summary>
        /// 贪心网格按轴遍历（0=X、1=Y、2=Z），这里把「轴 + 朝向正负」翻译成方块面。
        /// 约定 +Z 为北、-Z 为南；当前 JSON 格式下四个侧面共用同一张贴图，这个约定还观测不到差异。
        /// </summary>
        public static BlockFace FromAxis(int axis, bool facingPositive)
        {
            switch (axis)
            {
                case 0: return facingPositive ? BlockFace.East : BlockFace.West;
                case 1: return facingPositive ? BlockFace.Top : BlockFace.Bottom;
                case 2: return facingPositive ? BlockFace.North : BlockFace.South;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis), axis, "轴只能是 0、1、2。");
            }
        }
    }
}
```

- [ ] **Step 4: 跑测试确认通过**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockFacesTests"
```

预期：`Passed! - Failed: 0, Passed: 9`

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Core/Blocks/BlockFaces.cs Assets/Tests/EditMode/Blocks/BlockFacesTests.cs
git commit -m "渲染层: 轴向到方块面的换算"
```

---

## Task 2: 方块注册表的贴图索引表

渲染层要按贴图分材质，就需要一个稳定的「贴图名 → int 索引」映射。索引按**序数排序**分配，和 `numericId` 的自动分配用同一套确定性约定，保证跨机器一致。

**Files:**
- Modify: `Assets/Scripts/Core/Blocks/BlockRegistry.cs`
- Test: `Assets/Tests/EditMode/Blocks/BlockTextureTableTests.cs`

- [ ] **Step 1: 写失败的测试**

创建 `Assets/Tests/EditMode/Blocks/BlockTextureTableTests.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;
using MyWorld.Core.Blocks;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    [TestFixture]
    public class BlockTextureTableTests
    {
        private const string Air = @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }";

        private const string Stone = @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }";

        private const string Grass = @"{ ""id"": ""grass"", ""numericId"": 3,
            ""textures"": { ""top"": ""grass-top"", ""bottom"": ""dirt"", ""side"": ""grass-side"" } }";

        private const string Dirt = @"{ ""id"": ""dirt"", ""numericId"": 2, ""textures"": { ""all"": ""dirt"" } }";

        private static BlockRegistry BuildRegistry()
            => BlockRegistry.FromJson(new[] { Air, Stone, Grass, Dirt });

        /// <summary>TextureNames 是 IReadOnlyList，没有 IndexOf，这里包一层。</summary>
        private static int SlotOf(BlockRegistry registry, string textureName)
            => registry.TextureNames.ToList().IndexOf(textureName);

        [Test]
        public void TextureNames_AreDeduplicatedAndOrdinallySorted()
        {
            BlockRegistry registry = BuildRegistry();

            // dirt 被 dirt.all 与 grass.bottom 共同引用，只应占一个槽位
            Assert.That(registry.TextureNames,
                Is.EqualTo(new[] { "dirt", "grass-side", "grass-top", "stone" }));
        }

        [Test]
        public void GetTextureIndex_ResolvesEachFaceSeparately()
        {
            BlockRegistry registry = BuildRegistry();
            int grassTop = SlotOf(registry, "grass-top");
            int grassSide = SlotOf(registry, "grass-side");
            int dirt = SlotOf(registry, "dirt");

            Assert.That(registry.GetTextureIndex(3, BlockFace.Top), Is.EqualTo(grassTop));
            Assert.That(registry.GetTextureIndex(3, BlockFace.Bottom), Is.EqualTo(dirt));
            Assert.That(registry.GetTextureIndex(3, BlockFace.East), Is.EqualTo(grassSide));
            Assert.That(registry.GetTextureIndex(3, BlockFace.West), Is.EqualTo(grassSide));
            Assert.That(registry.GetTextureIndex(3, BlockFace.North), Is.EqualTo(grassSide));
            Assert.That(registry.GetTextureIndex(3, BlockFace.South), Is.EqualTo(grassSide));
        }

        [Test]
        public void GetTextureIndex_ForBlockWithAllTextures_IsSameOnEveryFace()
        {
            BlockRegistry registry = BuildRegistry();
            int stone = SlotOf(registry, "stone");

            for (var face = 0; face < 6; face++)
            {
                Assert.That(registry.GetTextureIndex(1, (BlockFace)face), Is.EqualTo(stone));
            }
        }

        [Test]
        public void GetTextureIndex_ForAir_ReturnsNoTexture()
        {
            BlockRegistry registry = BuildRegistry();

            Assert.That(registry.GetTextureIndex(0, BlockFace.Top), Is.EqualTo(BlockRegistry.NoTextureIndex),
                "空气没有可见面，不应占用贴图槽位");
        }

        [Test]
        public void GetTextureIndex_ForUnregisteredId_ReturnsNoTexture()
        {
            BlockRegistry registry = BuildRegistry();

            Assert.That(registry.GetTextureIndex(60000, BlockFace.Top), Is.EqualTo(BlockRegistry.NoTextureIndex),
                "热路径上遇到未注册 ID 不能抛异常");
        }

        [Test]
        public void TextureIndices_AreIndependentOfDocumentOrder()
        {
            BlockRegistry forward = BlockRegistry.FromJson(new[] { Air, Stone, Grass, Dirt });
            BlockRegistry reversed = BlockRegistry.FromJson(new[] { Dirt, Grass, Stone, Air });

            Assert.That(reversed.TextureNames, Is.EqualTo(forward.TextureNames),
                "贴图索引必须与文件枚举顺序无关，否则不同机器上的存档/渲染会对不上");
        }
    }
}
```

注意：`Assert.That(registry.TextureNames, Is.EqualTo(new[] {...}))` 里 `TextureNames` 是
`IReadOnlyList<string>`，NUnit 的集合比较能直接吃下它，无需转换。

- [ ] **Step 2: 跑测试确认它失败**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockTextureTableTests"
```

预期：编译失败，报 `"BlockRegistry"未包含"TextureNames"的定义`

- [ ] **Step 3: 写实现**

在 `Assets/Scripts/Core/Blocks/BlockRegistry.cs` 中，把这两个字段声明加到已有的 `_byId` / `_byNumericId` 之后：

```csharp
        private readonly Dictionary<ushort, int[]> _faceTextures = new Dictionary<ushort, int[]>();
        private string[] _textureNames = Array.Empty<string>();
```

在 `public int Count => _byId.Count;` 之后加入公开成员：

```csharp
        /// <summary>贴图表中表示「此面无贴图」的索引：空气与未注册方块都是它。</summary>
        public const int NoTextureIndex = -1;

        /// <summary>所有方块引用到的贴图名，去重后按序数排序。下标即渲染层的材质槽编号。</summary>
        public IReadOnlyList<string> TextureNames => _textureNames;

        /// <summary>热路径查询：取某方块某个面的贴图索引，未注册时返回 <see cref="NoTextureIndex"/>。</summary>
        public int GetTextureIndex(ushort numericId, BlockFace face)
            => _faceTextures.TryGetValue(numericId, out int[] faces) ? faces[(int)face] : NoTextureIndex;
```

在 `FromJson` 末尾的 `return registry;` 之前插入一行：

```csharp
            registry.BuildTextureTable();

            return registry;
```

在 `private void Add(BlockDefinition definition)` 之前插入方法：

```csharp
        /// <summary>
        /// 汇总所有方块引用到的贴图并分配索引。按序数排序而非首次出现顺序，
        /// 使索引与文件枚举顺序无关，与 numericId 的自动分配保持同一套确定性约定。
        /// </summary>
        private void BuildTextureTable()
        {
            _textureNames = _byId.Values
                .Where(definition => definition.Textures != null)
                .SelectMany(definition => definition.Textures)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _textureNames.Length; i++)
            {
                lookup[_textureNames[i]] = i;
            }

            _faceTextures.Clear();
            foreach (BlockDefinition definition in _byId.Values)
            {
                var faces = new int[6];
                for (var face = 0; face < faces.Length; face++)
                {
                    faces[face] = definition.Textures == null
                        ? NoTextureIndex
                        : lookup[definition.Textures[face]];
                }

                _faceTextures[definition.NumericId] = faces;
            }
        }
```

文件顶部已有 `using System;`、`using System.Collections.Generic;`、`using System.Linq;`，无需新增。

- [ ] **Step 4: 跑测试确认通过**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockTextureTableTests"
```

预期：`Passed! - Failed: 0, Passed: 6`

- [ ] **Step 5: 跑全量测试，确认没碰坏既有行为**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：全部通过，总数为 155 + 9 + 6 = 170

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Core/Blocks/BlockRegistry.cs Assets/Tests/EditMode/Blocks/BlockTextureTableTests.cs
git commit -m "渲染层: 方块注册表的贴图索引表"
```

---

## Task 3: 网格数据源提供贴图索引

`GreedyMesher` 只认识 `IBlockSource`。要让它知道每个面用哪张贴图，就得从这个接口拿。

**Files:**
- Modify: `Assets/Scripts/Core/Meshing/IBlockSource.cs`
- Modify: `Assets/Scripts/Core/Voxel/ChunkMeshSource.cs`
- Modify: `Assets/Tests/EditMode/Meshing/GreedyMesherTests.cs:141-176`（测试替身补上新方法）
- Test: `Assets/Tests/EditMode/Voxel/ChunkMeshSourceTextureTests.cs`

- [ ] **Step 1: 写失败的测试**

创建 `Assets/Tests/EditMode/Voxel/ChunkMeshSourceTextureTests.cs`：

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    [TestFixture]
    public class ChunkMeshSourceTextureTests
    {
        private const string Air = @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }";

        private const string Stone = @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }";

        private const string Grass = @"{ ""id"": ""grass"", ""numericId"": 3,
            ""textures"": { ""top"": ""grass-top"", ""bottom"": ""dirt"", ""side"": ""grass-side"" } }";

        [Test]
        public void GetTextureIndex_DelegatesToRegistry()
        {
            BlockRegistry registry = BlockRegistry.FromJson(new[] { Air, Stone, Grass });
            var source = new ChunkMeshSource(new World(), registry, new ChunkPos(0, 0), 0);

            Assert.That(source.GetTextureIndex(3, BlockFace.Top),
                Is.EqualTo(registry.GetTextureIndex(3, BlockFace.Top)));
            Assert.That(source.GetTextureIndex(3, BlockFace.Bottom),
                Is.Not.EqualTo(source.GetTextureIndex(3, BlockFace.Top)),
                "草方块的顶面与底面用的是不同贴图");
        }

        [Test]
        public void GetTextureIndex_ForAir_ReturnsNoTexture()
        {
            BlockRegistry registry = BlockRegistry.FromJson(new[] { Air, Stone, Grass });
            var source = new ChunkMeshSource(new World(), registry, new ChunkPos(0, 0), 0);

            Assert.That(source.GetTextureIndex(0, BlockFace.Top), Is.EqualTo(BlockRegistry.NoTextureIndex));
        }
    }
}
```

- [ ] **Step 2: 跑测试确认它失败**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~ChunkMeshSourceTextureTests"
```

预期：编译失败，报 `"ChunkMeshSource"未包含"GetTextureIndex"的定义`

- [ ] **Step 3: 扩展接口**

把 `Assets/Scripts/Core/Meshing/IBlockSource.cs` 整个替换为：

```csharp
using MyWorld.Core.Blocks;

namespace MyWorld.Core.Meshing
{
    /// <summary>
    /// 网格生成的方块来源。坐标可越出 [0, 16) 到 -1 与 16，用于采样相邻区块以正确剔除接缝面。
    /// 设计为接口 + 泛型约束，使实现可以是 struct，避免热循环中的虚调用。
    /// </summary>
    public interface IBlockSource
    {
        ushort GetBlock(int x, int y, int z);

        bool IsSolid(ushort blockId);

        /// <summary>该方块某个面用哪张贴图。返回值用于把网格拆成按贴图分组的 submesh。</summary>
        int GetTextureIndex(ushort blockId, BlockFace face);
    }
}
```

- [ ] **Step 4: 实现到 ChunkMeshSource**

在 `Assets/Scripts/Core/Voxel/ChunkMeshSource.cs` 的 `IsSolid` 方法之后追加：

```csharp
        public int GetTextureIndex(ushort blockId, BlockFace face) => _registry.GetTextureIndex(blockId, face);
```

- [ ] **Step 5: 补上测试替身的新方法**

在 `Assets/Tests/EditMode/Meshing/GreedyMesherTests.cs` 中，`PaddedBlockSource` 的 `IsSolid` 之后追加：

```csharp
        /// <summary>测试替身直接把方块 ID 当贴图索引用，方便断言 quad 归到了哪一组。</summary>
        public int GetTextureIndex(ushort blockId, BlockFace face) => blockId;
```

并在该文件顶部的 using 区补上：

```csharp
using MyWorld.Core.Blocks;
```

- [ ] **Step 6: 跑测试确认通过**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：全部通过，总数 172

- [ ] **Step 7: 提交**

```bash
git add Assets/Scripts/Core/Meshing/IBlockSource.cs Assets/Scripts/Core/Voxel/ChunkMeshSource.cs Assets/Tests/EditMode/Meshing/GreedyMesherTests.cs Assets/Tests/EditMode/Voxel/ChunkMeshSourceTextureTests.cs
git commit -m "渲染层: 网格数据源提供每面的贴图索引"
```

---
