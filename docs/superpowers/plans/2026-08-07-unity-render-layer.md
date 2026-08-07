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

## Task 4: 贪心网格记录每个 quad 的贴图

`GreedyMesher` 的 mask 里存的是方块 ID（正数=面朝 +axis，负数=面朝 -axis），
所以**同一个合并后的 quad 内方块 ID 与朝向必然一致**，贴图也就必然一致——
可以安全地一个 quad 记一个贴图索引。

**Files:**
- Modify: `Assets/Scripts/Core/Meshing/MeshBuffer.cs`
- Modify: `Assets/Scripts/Core/Meshing/GreedyMesher.cs`
- Test: `Assets/Tests/EditMode/Meshing/GreedyMesherTextureTests.cs`

- [ ] **Step 1: 写失败的测试**

创建 `Assets/Tests/EditMode/Meshing/GreedyMesherTextureTests.cs`：

```csharp
using System.Linq;
using MyWorld.Core.Meshing;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Meshing
{
    /// <summary>
    /// 复用 GreedyMesherTests 里的 PaddedBlockSource——它把方块 ID 直接当贴图索引返回，
    /// 所以断言里的「贴图索引」就等于放进去的方块 ID。
    /// </summary>
    [TestFixture]
    public class GreedyMesherTextureTests
    {
        private const ushort Stone = 1;
        private const ushort Dirt = 2;

        [Test]
        public void QuadTextures_HasOneEntryPerQuad()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadTextures.Count, Is.EqualTo(mesh.QuadCount));
            Assert.That(mesh.QuadCount, Is.EqualTo(6));
        }

        [Test]
        public void QuadTextures_RecordTheBlockThatOwnsTheFace()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadTextures.Distinct(), Is.EqualTo(new[] { (int)Stone }),
                "只放了一种方块，六个面的贴图索引应当全都指向它");
        }

        [Test]
        public void QuadTextures_DistinguishNeighbouringBlockTypes()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(1, 0, 0, Dirt);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadTextures.Distinct().OrderBy(t => t),
                Is.EqualTo(new[] { (int)Stone, (int)Dirt }));
            Assert.That(mesh.QuadTextures.Count(t => t == Stone), Is.EqualTo(5));
            Assert.That(mesh.QuadTextures.Count(t => t == Dirt), Is.EqualTo(5));
        }

        [Test]
        public void Clear_AlsoResetsQuadTextures()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);
            mesh.Clear();

            Assert.That(mesh.QuadTextures, Is.Empty);
        }

        [Test]
        public void EachQuad_OwnsSixConsecutiveIndices()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(3, 4, 5, Dirt);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            // SplitByTexture 依赖这个不变量：第 i 个 quad 的索引恰好是 [i*6, i*6+6)
            Assert.That(mesh.IndexCount, Is.EqualTo(mesh.QuadCount * 6));
            for (var quad = 0; quad < mesh.QuadCount; quad++)
            {
                for (var k = 0; k < 6; k++)
                {
                    Assert.That(mesh.Indices[quad * 6 + k], Is.InRange(quad * 4, quad * 4 + 3),
                        $"第 {quad} 个 quad 的索引越出了它自己的四个顶点");
                }
            }
        }
    }
}
```

- [ ] **Step 2: 跑测试确认它失败**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~GreedyMesherTextureTests"
```

预期：编译失败，报 `"MeshBuffer"未包含"QuadTextures"的定义`

- [ ] **Step 3: MeshBuffer 增加 QuadTextures**

把 `Assets/Scripts/Core/Meshing/MeshBuffer.cs` 整个替换为：

```csharp
using System.Collections.Generic;
using MyWorld.Core.Math;

namespace MyWorld.Core.Meshing
{
    /// <summary>网格生成的输出缓冲。不含任何 Unity 类型，由适配层负责上传。</summary>
    public sealed class MeshBuffer
    {
        public readonly List<Float3> Positions = new List<Float3>();
        public readonly List<Float3> Normals = new List<Float3>();
        public readonly List<Float2> Uvs = new List<Float2>();
        public readonly List<int> Indices = new List<int>();

        /// <summary>每个 quad 一项，记它用哪张贴图。渲染层据此把网格拆成多个 submesh。</summary>
        public readonly List<int> QuadTextures = new List<int>();

        public int VertexCount => Positions.Count;

        public int IndexCount => Indices.Count;

        public int QuadCount => Positions.Count / 4;

        public void Clear()
        {
            Positions.Clear();
            Normals.Clear();
            Uvs.Clear();
            Indices.Clear();
            QuadTextures.Clear();
        }
    }
}
```

- [ ] **Step 4: GreedyMesher 填入贴图索引**

在 `Assets/Scripts/Core/Meshing/GreedyMesher.cs` 顶部的 using 区补上：

```csharp
using MyWorld.Core.Blocks;
```

把 `Build` 里的这一行：

```csharp
                    EmitQuads(output, mask, cursor, spanU, spanV, axis, u, v);
```

改成：

```csharp
                    EmitQuads(source, output, mask, cursor, spanU, spanV, axis, u, v);
```

把 `EmitQuads` 的签名改成泛型：

```csharp
        private static void EmitQuads<TSource>(TSource source, MeshBuffer output, int[] mask, int[] cursor,
            int[] spanU, int[] spanV, int axis, int u, int v) where TSource : IBlockSource
```

把 `EmitQuads` 里调用 `AddQuad` 的那一行：

```csharp
                    AddQuad(output, cursor, spanU, spanV, axis, face > 0, width, height);
```

替换为：

```csharp
                    // mask 的正负同时编码了「面属于谁」和「朝向」，二者共同决定用哪张贴图
                    bool facingPositive = face > 0;
                    var owner = (ushort)(facingPositive ? face : -face);
                    int textureIndex = source.GetTextureIndex(owner, BlockFaces.FromAxis(axis, facingPositive));

                    AddQuad(output, cursor, spanU, spanV, axis, facingPositive, width, height, textureIndex);
```

把 `AddQuad` 的签名改成：

```csharp
        private static void AddQuad(MeshBuffer output, int[] origin, int[] spanU, int[] spanV, int axis,
            bool facingPositive, int width, int height, int textureIndex)
```

并在 `AddQuad` 内 `int baseVertex = output.Positions.Count;` 的下一行插入：

```csharp
            output.QuadTextures.Add(textureIndex);
```

- [ ] **Step 5: 跑测试确认通过**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：全部通过，总数 177

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Core/Meshing/MeshBuffer.cs Assets/Scripts/Core/Meshing/GreedyMesher.cs Assets/Tests/EditMode/Meshing/GreedyMesherTextureTests.cs
git commit -m "渲染层: 贪心网格记录每个面的贴图索引"
```

---

## Task 5: 按贴图把索引缓冲拆成 submesh

Unity 的一个 submesh 必须对应索引缓冲里**连续的一段**。贪心网格是按轴产出的，
不同贴图的 quad 天然交错，所以要重排一次。顶点缓冲不用动——多个 submesh 共享同一份顶点。

**Files:**
- Create: `Assets/Scripts/Core/Meshing/Submesh.cs`
- Modify: `Assets/Scripts/Core/Meshing/MeshBuffer.cs`
- Test: `Assets/Tests/EditMode/Meshing/MeshBufferSplitTests.cs`

- [ ] **Step 1: 写失败的测试**

创建 `Assets/Tests/EditMode/Meshing/MeshBufferSplitTests.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;
using MyWorld.Core.Math;
using MyWorld.Core.Meshing;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Meshing
{
    [TestFixture]
    public class MeshBufferSplitTests
    {
        /// <summary>手工塞一个 quad：4 个顶点 + 6 个索引 + 1 个贴图索引，与 GreedyMesher 的产出形状一致。</summary>
        private static void AddQuad(MeshBuffer buffer, int textureIndex)
        {
            int baseVertex = buffer.Positions.Count;
            for (var i = 0; i < 4; i++)
            {
                buffer.Positions.Add(new Float3(i, 0f, 0f));
                buffer.Normals.Add(new Float3(0f, 1f, 0f));
                buffer.Uvs.Add(new Float2(0f, 0f));
            }

            buffer.Indices.Add(baseVertex);
            buffer.Indices.Add(baseVertex + 1);
            buffer.Indices.Add(baseVertex + 2);
            buffer.Indices.Add(baseVertex);
            buffer.Indices.Add(baseVertex + 2);
            buffer.Indices.Add(baseVertex + 3);

            buffer.QuadTextures.Add(textureIndex);
        }

        private static List<string> TrianglesOf(MeshBuffer buffer)
        {
            var triangles = new List<string>();
            for (var i = 0; i < buffer.IndexCount; i += 3)
            {
                triangles.Add($"{buffer.Indices[i]},{buffer.Indices[i + 1]},{buffer.Indices[i + 2]}");
            }

            return triangles;
        }

        [Test]
        public void EmptyBuffer_ProducesNoSubmeshes()
        {
            var buffer = new MeshBuffer();
            var submeshes = new List<Submesh>();

            buffer.SplitByTexture(submeshes);

            Assert.That(submeshes, Is.Empty);
        }

        [Test]
        public void SingleTexture_ProducesOneSubmeshCoveringEverything()
        {
            var buffer = new MeshBuffer();
            AddQuad(buffer, 7);
            AddQuad(buffer, 7);
            var submeshes = new List<Submesh>();

            buffer.SplitByTexture(submeshes);

            Assert.That(submeshes.Count, Is.EqualTo(1));
            Assert.That(submeshes[0].TextureIndex, Is.EqualTo(7));
            Assert.That(submeshes[0].IndexStart, Is.EqualTo(0));
            Assert.That(submeshes[0].IndexCount, Is.EqualTo(12));
        }

        [Test]
        public void InterleavedTextures_AreRegroupedIntoContiguousRanges()
        {
            var buffer = new MeshBuffer();
            AddQuad(buffer, 5);
            AddQuad(buffer, 2);
            AddQuad(buffer, 5);
            var submeshes = new List<Submesh>();

            buffer.SplitByTexture(submeshes);

            Assert.That(submeshes.Select(s => s.TextureIndex), Is.EqualTo(new[] { 2, 5 }),
                "段按贴图索引升序排列，与 quad 的产出顺序无关");
            Assert.That(submeshes[0].IndexStart, Is.EqualTo(0));
            Assert.That(submeshes[0].IndexCount, Is.EqualTo(6));
            Assert.That(submeshes[1].IndexStart, Is.EqualTo(6));
            Assert.That(submeshes[1].IndexCount, Is.EqualTo(12));
        }

        [Test]
        public void SubmeshRanges_CoverTheWholeIndexBufferWithoutOverlap()
        {
            var buffer = new MeshBuffer();
            AddQuad(buffer, 3);
            AddQuad(buffer, 1);
            AddQuad(buffer, 3);
            AddQuad(buffer, 9);
            var submeshes = new List<Submesh>();

            buffer.SplitByTexture(submeshes);

            var cursor = 0;
            foreach (Submesh submesh in submeshes)
            {
                Assert.That(submesh.IndexStart, Is.EqualTo(cursor), "各段必须首尾相接");
                cursor += submesh.IndexCount;
            }

            Assert.That(cursor, Is.EqualTo(buffer.IndexCount), "所有段加起来应覆盖整个索引缓冲");
        }

        [Test]
        public void Reordering_PreservesTheSetOfTriangles()
        {
            var buffer = new MeshBuffer();
            AddQuad(buffer, 4);
            AddQuad(buffer, 0);
            AddQuad(buffer, 4);

            List<string> before = TrianglesOf(buffer);

            buffer.SplitByTexture(new List<Submesh>());

            List<string> after = TrianglesOf(buffer);

            Assert.That(after.OrderBy(t => t), Is.EqualTo(before.OrderBy(t => t)),
                "重排只能改变三角形的顺序，不能增删或改写任何一个三角形");
        }

        [Test]
        public void SplitByTexture_ClearsPreviousOutput()
        {
            var buffer = new MeshBuffer();
            AddQuad(buffer, 1);
            var submeshes = new List<Submesh> { new Submesh(99, 0, 0) };

            buffer.SplitByTexture(submeshes);

            Assert.That(submeshes.Count, Is.EqualTo(1));
            Assert.That(submeshes[0].TextureIndex, Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: 跑测试确认它失败**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~MeshBufferSplitTests"
```

预期：编译失败，报 `未能找到类型或命名空间名"Submesh"`

- [ ] **Step 3: 新建 Submesh**

创建 `Assets/Scripts/Core/Meshing/Submesh.cs`：

```csharp
namespace MyWorld.Core.Meshing
{
    /// <summary>索引缓冲中属于同一张贴图的一段，一段对应 Unity 侧的一个 submesh。</summary>
    public readonly struct Submesh
    {
        public readonly int TextureIndex;
        public readonly int IndexStart;
        public readonly int IndexCount;

        public Submesh(int textureIndex, int indexStart, int indexCount)
        {
            TextureIndex = textureIndex;
            IndexStart = indexStart;
            IndexCount = indexCount;
        }

        public override string ToString() => $"贴图 {TextureIndex}：索引 [{IndexStart}, {IndexStart + IndexCount})";
    }
}
```

- [ ] **Step 4: MeshBuffer 增加 SplitByTexture**

在 `Assets/Scripts/Core/Meshing/MeshBuffer.cs` 顶部的 using 区补上：

```csharp
using System;
```

在 `Clear()` 方法之后追加：

```csharp
        /// <summary>每个 quad 固定产出 4 个顶点 6 个索引，重排时据此定位某个 quad 的索引。</summary>
        private const int IndicesPerQuad = 6;

        /// <summary>
        /// 按贴图重排索引缓冲，使同一张贴图的三角形连续，并把每段的范围写进 <paramref name="output"/>。
        /// 顶点缓冲保持不动——多个 submesh 共享同一份顶点，索引可以指向其中任意位置。
        /// 段按贴图索引升序排列，与 quad 的产出顺序无关，便于断言与复现。
        /// </summary>
        public void SplitByTexture(List<Submesh> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Clear();

            if (QuadTextures.Count == 0)
            {
                return;
            }

            var buckets = new SortedDictionary<int, List<int>>();
            for (var quad = 0; quad < QuadTextures.Count; quad++)
            {
                if (!buckets.TryGetValue(QuadTextures[quad], out List<int> quads))
                {
                    quads = new List<int>();
                    buckets[QuadTextures[quad]] = quads;
                }

                quads.Add(quad);
            }

            var reordered = new List<int>(Indices.Count);
            var reorderedTextures = new List<int>(QuadTextures.Count);

            foreach (KeyValuePair<int, List<int>> bucket in buckets)
            {
                int start = reordered.Count;
                foreach (int quad in bucket.Value)
                {
                    int origin = quad * IndicesPerQuad;
                    for (var k = 0; k < IndicesPerQuad; k++)
                    {
                        reordered.Add(Indices[origin + k]);
                    }

                    reorderedTextures.Add(bucket.Key);
                }

                output.Add(new Submesh(bucket.Key, start, reordered.Count - start));
            }

            Indices.Clear();
            Indices.AddRange(reordered);

            // QuadTextures 跟着一起重排，保持「第 i 个 quad 的索引在 [i*6, i*6+6)」这个不变量
            QuadTextures.Clear();
            QuadTextures.AddRange(reorderedTextures);
        }
```

- [ ] **Step 5: 跑测试确认通过**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：全部通过，总数 183

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Core/Meshing/Submesh.cs Assets/Scripts/Core/Meshing/MeshBuffer.cs Assets/Tests/EditMode/Meshing/MeshBufferSplitTests.cs
git commit -m "渲染层: 按贴图把索引缓冲拆成 submesh"
```

---

## Task 6: World 支持整块塞入已生成的区块

`WorldGenerator.Generate` 返回一根完整的 `ChunkColumn`，但 `World` 目前只能靠
`SetBlock` 一格一格创建区块。渲染层需要「生成一根、挂一根」。

**Files:**
- Modify: `Assets/Scripts/Core/Voxel/World.cs`
- Test: `Assets/Tests/EditMode/Voxel/WorldTests.cs`

- [ ] **Step 1: 写失败的测试**

在 `Assets/Tests/EditMode/Voxel/WorldTests.cs` 的最后一个测试方法之后、类的结束大括号之前追加：

```csharp
        [Test]
        public void AddChunk_MakesItsBlocksReadable()
        {
            var world = new World();
            var column = new ChunkColumn();
            column.SetBlock(3, 64, 5, Stone);

            world.AddChunk(new ChunkPos(2, -3), column);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(1));
            Assert.That(world.GetBlock(2 * 16 + 3, 64, -3 * 16 + 5), Is.EqualTo(Stone));
        }

        [Test]
        public void AddChunk_ReplacesAnExistingChunkAtTheSamePosition()
        {
            var world = new World();
            world.SetBlock(0, 64, 0, Stone);

            var replacement = new ChunkColumn();
            replacement.SetBlock(0, 64, 0, Dirt);
            world.AddChunk(new ChunkPos(0, 0), replacement);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(1), "同一位置不应留下两根区块列");
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(Dirt));
        }

        [Test]
        public void AddChunk_WithNullColumn_Throws()
        {
            var world = new World();

            Assert.Throws<System.ArgumentNullException>(() => world.AddChunk(new ChunkPos(0, 0), null));
        }
```

- [ ] **Step 2: 跑测试确认它失败**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~WorldTests"
```

预期：编译失败，报 `"World"未包含"AddChunk"的定义`

- [ ] **Step 3: 写实现**

在 `Assets/Scripts/Core/Voxel/World.cs` 顶部的 using 区补上：

```csharp
using System;
```

在 `public void SetBlock(...)` 之后追加：

```csharp
        /// <summary>挂入一根已经生成好的区块列。同一位置已有区块时整根替换。</summary>
        public void AddChunk(ChunkPos pos, ChunkColumn column)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            _chunks[pos] = column;
        }
```

- [ ] **Step 4: 跑测试确认通过**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：全部通过，总数 186

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Core/Voxel/World.cs Assets/Tests/EditMode/Voxel/WorldTests.cs
git commit -m "渲染层: World 支持整块挂入已生成的区块列"
```

---
