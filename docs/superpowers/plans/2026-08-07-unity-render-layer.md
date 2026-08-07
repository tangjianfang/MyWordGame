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
        /// <summary>一个 quad 固定 4 个顶点、6 个索引，SplitByTexture 依赖这个不变量。</summary>
        public const int VerticesPerQuad = 4;

        public const int IndicesPerQuad = 6;

        // 复用的中间缓冲，避免每个区块段都产生一堆临时对象
        private readonly SortedDictionary<int, int> _quadCountsByTexture = new SortedDictionary<int, int>();
        private readonly Dictionary<int, int> _writeCursorByTexture = new Dictionary<int, int>();
        private readonly List<int> _reorderedIndices = new List<int>();

        /// <summary>
        /// 把索引缓冲按贴图重排，使同一张贴图的三角形连续，并输出各段的范围。
        /// <para>
        /// 只动 <see cref="Indices"/>，不动顶点与 <see cref="QuadTextures"/>——
        /// quad 序号是由顶点下标推出来的（<c>索引 / 4</c>），动了顶点侧的任何一个，对应关系就断了。
        /// 重排是稳定的，因此对已排好的缓冲再调一次结果不变。
        /// </para>
        /// </summary>
        public void SplitByTexture(List<Submesh> output)
        {
            output.Clear();
            _quadCountsByTexture.Clear();
            _writeCursorByTexture.Clear();

            if (Indices.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Indices.Count; i += IndicesPerQuad)
            {
                int texture = TextureOfTriangleGroupAt(i);
                _quadCountsByTexture.TryGetValue(texture, out int count);
                _quadCountsByTexture[texture] = count + 1;
            }

            // 前缀和定出每段起点；SortedDictionary 保证段按贴图索引升序，结果可预期
            var start = 0;
            foreach (KeyValuePair<int, int> pair in _quadCountsByTexture)
            {
                int indexCount = pair.Value * IndicesPerQuad;
                output.Add(new Submesh(pair.Key, start, indexCount));
                _writeCursorByTexture[pair.Key] = start;
                start += indexCount;
            }

            while (_reorderedIndices.Count < Indices.Count)
            {
                _reorderedIndices.Add(0);
            }

            for (int i = 0; i < Indices.Count; i += IndicesPerQuad)
            {
                int texture = TextureOfTriangleGroupAt(i);
                int target = _writeCursorByTexture[texture];

                for (var k = 0; k < IndicesPerQuad; k++)
                {
                    _reorderedIndices[target + k] = Indices[i + k];
                }

                _writeCursorByTexture[texture] = target + IndicesPerQuad;
            }

            for (var i = 0; i < Indices.Count; i++)
            {
                Indices[i] = _reorderedIndices[i];
            }
        }

        /// <summary>每组 6 个索引来自同一个 quad，取首个索引反推 quad 序号即可。</summary>
        private int TextureOfTriangleGroupAt(int indexOffset)
            => QuadTextures[Indices[indexOffset] / VerticesPerQuad];
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

## Task 7: Unity 程序集与方块注册表加载

从这里开始进入 Unity 层。这一层允许用 `UnityEngine.*`，与 Core 的
`noEngineReferences: true` 相对。

**Files:**
- Create: `Assets/Scripts/Unity/MyWorld.Unity.asmdef`
- Create: `Assets/Scripts/Unity/Bootstrap/BlockRegistryLoader.cs`

本任务及之后的 Unity 层任务**没有 `dotnet test` 覆盖**——`tools/dotnet/*.csproj` 只链接
`Assets/Scripts/Core` 与 `Assets/Tests/EditMode`，不含 `Assets/Scripts/Unity`。
Unity 层的验证手段是 Task 13 的批处理编译 + 人工验收，这一点在计划里如实标注，
不假装它有自动化覆盖。

- [ ] **Step 1: 建程序集定义**

创建 `Assets/Scripts/Unity/MyWorld.Unity.asmdef`：

```json
{
  "name": "MyWorld.Unity",
  "rootNamespace": "MyWorld.Unity",
  "references": [
    "MyWorld.Core"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: 写方块注册表加载器**

创建 `Assets/Scripts/Unity/Bootstrap/BlockRegistryLoader.cs`：

```csharp
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Blocks;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 从 StreamingAssets 读方块定义。桌面平台上 StreamingAssets 就是普通目录，可以直接文件 IO；
    /// 将来要出 Android 版再换成 UnityWebRequest，不影响调用方。
    /// </summary>
    public static class BlockRegistryLoader
    {
        public static string BlockDirectory => Path.Combine(Application.streamingAssetsPath, "blocks");

        public static string TextureDirectory => Path.Combine(BlockDirectory, "textures");

        public static BlockRegistry Load()
        {
            string directory = BlockDirectory;
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"未找到方块定义目录：{directory}");
            }

            var documents = new List<string>();
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                documents.Add(File.ReadAllText(path));
            }

            if (documents.Count == 0)
            {
                throw new InvalidDataException($"{directory} 下没有任何方块定义文件。");
            }

            return BlockRegistry.FromJson(documents);
        }
    }
}
```

- [ ] **Step 3: 确认 Core 测试没被影响**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：全部通过，总数仍是 186（新增的 Unity 文件不在这个解决方案里）

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/Unity
git commit -m "渲染层: Unity 程序集与方块注册表加载"
```

---

## Task 8: 方块材质库

每张方块贴图一个材质。贴图 PNG 放在 StreamingAssets 下、没有经过 Unity 的资源导入流程，
所以在运行时用 `Texture2D.LoadImage` 从字节流建纹理——好处是**加方块贴图依然不用碰 Unity 编辑器**，
与「方块定义数据驱动」这条既有约定保持一致。

**Files:**
- Create: `Assets/Scripts/Unity/Rendering/BlockMaterialLibrary.cs`

- [ ] **Step 1: 写材质库**

创建 `Assets/Scripts/Unity/Rendering/BlockMaterialLibrary.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Blocks;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 贴图索引 → 材质。贪心网格的 UV 是按格数铺开的（(0,0)..(width,height)），
    /// 所以材质只要开 Repeat 环绕，合并后的大面就能保持单个方块的贴图密度，无需自定义 shader。
    /// </summary>
    public sealed class BlockMaterialLibrary : IDisposable
    {
        private readonly Material[] _materials;
        private readonly Material _missing;
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        private BlockMaterialLibrary(int slotCount, Material missing)
        {
            _materials = new Material[slotCount];
            _missing = missing;
        }

        public int Count => _materials.Length;

        /// <summary>越界或缺贴图时给出醒目的洋红占位材质，让问题在画面上一眼可见而不是静默消失。</summary>
        public Material Get(int textureIndex)
            => textureIndex >= 0 && textureIndex < _materials.Length ? _materials[textureIndex] : _missing;

        public static BlockMaterialLibrary Load(BlockRegistry registry, string textureDirectory)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            Shader shader = FindShader();
            var library = new BlockMaterialLibrary(registry.TextureNames.Count, CreateMissingMaterial(shader));

            for (var slot = 0; slot < registry.TextureNames.Count; slot++)
            {
                string textureName = registry.TextureNames[slot];
                string path = Path.Combine(textureDirectory, textureName + ".png");
                Texture2D texture = library.LoadTexture(path);

                if (texture == null)
                {
                    Debug.LogError($"方块贴图缺失或无法解码：{path}，该贴图改用占位材质。");
                    library._materials[slot] = library._missing;
                    continue;
                }

                library._materials[slot] = new Material(shader)
                {
                    name = textureName,
                    mainTexture = texture
                };
            }

            return library;
        }

        /// <summary>装了 URP 就用 URP 的 Lit，没装则退回内置管线的 Standard，两条路都能跑。</summary>
        private static Shader FindShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit 与内置 Standard 都找不到，无法建立方块材质。");
            }

            return shader;
        }

        private Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                DestroyObject(texture);
                return null;
            }

            // 必须在 LoadImage 之后设置：LoadImage 会按 PNG 重建纹理，之前的采样设置会丢
            // 32×32 像素风要 Point 过滤；关掉 mipmap，否则远处方块会糊成一团
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.anisoLevel = 0;

            _textures.Add(texture);
            return texture;
        }

        private static Material CreateMissingMaterial(Shader shader)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixels(new[] { Color.magenta, Color.black, Color.black, Color.magenta });
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;

            return new Material(shader) { name = "缺失贴图", mainTexture = texture };
        }

        /// <summary>编辑器里 Destroy 要到帧末才生效，非播放态下必须用 DestroyImmediate。</summary>
        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        public void Dispose()
        {
            foreach (Material material in _materials)
            {
                // 缺贴图的槽位共用 _missing，别重复销毁
                if (material != null && material != _missing)
                {
                    DestroyObject(material);
                }
            }

            if (_missing != null)
            {
                DestroyObject(_missing.mainTexture);
                DestroyObject(_missing);
            }

            foreach (Texture2D texture in _textures)
            {
                DestroyObject(texture);
            }

            _textures.Clear();
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add Assets/Scripts/Unity/Rendering/BlockMaterialLibrary.cs
git commit -m "渲染层: 方块材质库"
```

---

## Task 9: MeshBuffer 上传到 Unity Mesh

**Files:**
- Create: `Assets/Scripts/Unity/Rendering/ChunkMeshBuilder.cs`

- [ ] **Step 1: 写上传器**

创建 `Assets/Scripts/Unity/Rendering/ChunkMeshBuilder.cs`：

```csharp
using System.Collections.Generic;
using MyWorld.Core.Math;
using MyWorld.Core.Meshing;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 把 Core 层的 <see cref="MeshBuffer"/> 拷进 Unity 的 Mesh。
    /// 静态缓存只在主线程用——本里程碑的网格生成是同步的，多线程化时这里要一起改。
    /// </summary>
    public static class ChunkMeshBuilder
    {
        private static readonly List<Vector3> ScratchPositions = new List<Vector3>();
        private static readonly List<Vector3> ScratchNormals = new List<Vector3>();
        private static readonly List<Vector2> ScratchUvs = new List<Vector2>();
        private static int[] _scratchIndices = new int[0];

        public static void Apply(MeshBuffer buffer, IReadOnlyList<Submesh> submeshes, Mesh mesh)
        {
            mesh.Clear();

            if (buffer.VertexCount == 0 || submeshes.Count == 0)
            {
                mesh.subMeshCount = 0;
                return;
            }

            CopyPositions(buffer.Positions, ScratchPositions);
            CopyPositions(buffer.Normals, ScratchNormals);
            CopyUvs(buffer.Uvs, ScratchUvs);

            if (_scratchIndices.Length < buffer.IndexCount)
            {
                _scratchIndices = new int[buffer.IndexCount];
            }

            buffer.Indices.CopyTo(_scratchIndices, 0);

            // 一个 16³ 段最坏情况下的顶点数会超过 65535，索引格式统一用 32 位省得判断
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(ScratchPositions);
            mesh.SetNormals(ScratchNormals);
            mesh.SetUVs(0, ScratchUvs);

            mesh.subMeshCount = submeshes.Count;
            for (var i = 0; i < submeshes.Count; i++)
            {
                Submesh submesh = submeshes[i];
                mesh.SetIndices(_scratchIndices, submesh.IndexStart, submesh.IndexCount,
                    MeshTopology.Triangles, i, calculateBounds: false);
            }

            mesh.RecalculateBounds();
        }

        private static void CopyPositions(List<Float3> source, List<Vector3> target)
        {
            target.Clear();
            if (target.Capacity < source.Count)
            {
                target.Capacity = source.Count;
            }

            for (var i = 0; i < source.Count; i++)
            {
                Float3 value = source[i];
                target.Add(new Vector3(value.X, value.Y, value.Z));
            }
        }

        private static void CopyUvs(List<Float2> source, List<Vector2> target)
        {
            target.Clear();
            if (target.Capacity < source.Count)
            {
                target.Capacity = source.Count;
            }

            for (var i = 0; i < source.Count; i++)
            {
                Float2 value = source[i];
                target.Add(new Vector2(value.X, value.Y));
            }
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add Assets/Scripts/Unity/Rendering/ChunkMeshBuilder.cs
git commit -m "渲染层: MeshBuffer 上传到 Unity Mesh"
```

---

## Task 10: 区块段视图

**Files:**
- Create: `Assets/Scripts/Unity/Rendering/ChunkSectionView.cs`

- [ ] **Step 1: 写视图组件**

创建 `Assets/Scripts/Unity/Rendering/ChunkSectionView.cs`：

```csharp
using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 一个 16³ 区块段的渲染体：一个 GameObject + 一份 Mesh + 每张贴图一个材质槽。
    /// 网格顶点用段内局部坐标 [0, 16]，世界位置交给 Transform，这样同一份网格数据与位置解耦。
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class ChunkSectionView : MonoBehaviour
    {
        private static readonly MeshBuffer SharedBuffer = new MeshBuffer();
        private static readonly List<Submesh> SharedSubmeshes = new List<Submesh>();

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private ChunkPos _chunk;
        private int _sectionIndex;

        public static ChunkSectionView Create(Transform parent, ChunkPos chunk, int sectionIndex)
        {
            var gameObject = new GameObject($"区块段 {chunk.X},{chunk.Z} #{sectionIndex}");
            gameObject.transform.SetParent(parent, worldPositionStays: false);
            gameObject.transform.localPosition = new Vector3(
                chunk.X * VoxelCoords.ChunkSize,
                VoxelCoords.MinY + sectionIndex * VoxelCoords.ChunkSize,
                chunk.Z * VoxelCoords.ChunkSize);

            var view = gameObject.AddComponent<ChunkSectionView>();
            view._chunk = chunk;
            view._sectionIndex = sectionIndex;
            view._mesh = new Mesh { name = gameObject.name };
            view.GetComponent<MeshFilter>().sharedMesh = view._mesh;
            view._renderer = view.GetComponent<MeshRenderer>();

            return view;
        }

        /// <summary>
        /// 重建网格。返回是否有可见几何——被完全包裹的段一个面都没有，调用方可以直接把它销毁。
        /// </summary>
        public bool Rebuild(World world, BlockRegistry registry, BlockMaterialLibrary materials)
        {
            int sectionBaseY = VoxelCoords.MinY + _sectionIndex * VoxelCoords.ChunkSize;
            var source = new ChunkMeshSource(world, registry, _chunk, sectionBaseY);

            GreedyMesher.Build(source, SharedBuffer);
            SharedBuffer.SplitByTexture(SharedSubmeshes);
            ChunkMeshBuilder.Apply(SharedBuffer, SharedSubmeshes, _mesh);

            var slots = new Material[SharedSubmeshes.Count];
            for (var i = 0; i < SharedSubmeshes.Count; i++)
            {
                slots[i] = materials.Get(SharedSubmeshes[i].TextureIndex);
            }

            _renderer.sharedMaterials = slots;

            return SharedSubmeshes.Count > 0;
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add Assets/Scripts/Unity/Rendering/ChunkSectionView.cs
git commit -m "渲染层: 区块段视图"
```

---

## Task 11: 自由飞行相机

`ProjectSettings/ProjectSettings.asset` 里 `activeInputHandler: 0`，即旧版 Input Manager，
所以用 `Input.GetKey` / `Input.GetAxis` 是可用的。

**Files:**
- Create: `Assets/Scripts/Unity/Bootstrap/FreeFlyCamera.cs`

- [ ] **Step 1: 写相机组件**

创建 `Assets/Scripts/Unity/Bootstrap/FreeFlyCamera.cs`：

```csharp
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 里程碑 1 的验收工具：WASD 平移、QE 升降、按住右键转视角、Shift 加速。
    /// 玩家控制器是里程碑 2 的内容，这个组件到时候会被替换掉。
    /// </summary>
    public sealed class FreeFlyCamera : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 12f;
        [SerializeField] private float sprintMultiplier = 4f;
        [SerializeField] private float lookSensitivity = 2.5f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            float right = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float up = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            // 升降走世界的上方向而不是相机的上方向，低头飞行时手感才不会打架
            Vector3 direction = transform.right * right + Vector3.up * up + transform.forward * forward;
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

            transform.position += direction * (speed * Time.deltaTime);
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add Assets/Scripts/Unity/Bootstrap/FreeFlyCamera.cs
git commit -m "渲染层: 验收用自由飞行相机"
```

---

## Task 12: 世界启动器

**Files:**
- Create: `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs`

- [ ] **Step 1: 写启动器**

创建 `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs`：

```csharp
using System.Diagnostics;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 里程碑 1 的验收场景：生成固定范围的区块并一次性全部建成网格。
    /// 跟随玩家的动态加载/卸载是后续里程碑的事，这里刻意不做。
    /// </summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        [SerializeField] private int seed = 12345;

        [Tooltip("以原点为中心，向四周各生成多少个区块。3 表示 7×7 共 49 根区块列。")]
        [SerializeField] private int chunkRadius = 3;

        private BlockMaterialLibrary _materials;

        private void Start()
        {
            var stopwatch = Stopwatch.StartNew();

            BlockRegistry registry = BlockRegistryLoader.Load();
            _materials = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);

            var world = new World();
            var generator = new WorldGenerator(seed);

            // 必须先把全部区块灌进 World 再建网格：建网格要采样邻区块才能剔除接缝面，
            // 边生成边建网格会让先建的那几根在接缝处多出一整面
            for (int x = -chunkRadius; x <= chunkRadius; x++)
            {
                for (int z = -chunkRadius; z <= chunkRadius; z++)
                {
                    var pos = new ChunkPos(x, z);
                    world.AddChunk(pos, generator.Generate(pos));
                }
            }

            long generateMs = stopwatch.ElapsedMilliseconds;
            int visible = BuildAllMeshes(world, registry);

            Debug.Log($"世界就绪：{world.LoadedChunkCount} 根区块列（生成 {generateMs} ms），" +
                      $"{visible} 个可见区块段（建网格 {stopwatch.ElapsedMilliseconds - generateMs} ms），" +
                      $"{registry.TextureNames.Count} 种贴图。");
        }

        private int BuildAllMeshes(World world, BlockRegistry registry)
        {
            var visible = 0;

            for (int x = -chunkRadius; x <= chunkRadius; x++)
            {
                for (int z = -chunkRadius; z <= chunkRadius; z++)
                {
                    var pos = new ChunkPos(x, z);
                    if (!world.TryGetChunk(pos, out ChunkColumn column))
                    {
                        continue;
                    }

                    for (var section = 0; section < VoxelCoords.SectionCount; section++)
                    {
                        if (!column.HasSection(section))
                        {
                            continue;
                        }

                        ChunkSectionView view = ChunkSectionView.Create(transform, pos, section);
                        if (view.Rebuild(world, registry, _materials))
                        {
                            visible++;
                        }
                        else
                        {
                            // 完全被包裹的段一个面都没有，留着只是白占一个 GameObject
                            Destroy(view.gameObject);
                        }
                    }
                }
            }

            return visible;
        }

        private void OnDestroy()
        {
            _materials?.Dispose();
            _materials = null;
        }
    }
}
```

- [ ] **Step 2: 提交**

```bash
git add Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs
git commit -m "渲染层: 世界启动器"
```

---

## Task 13: 预览场景与批处理验证

场景文件（`.unity`）是 YAML 序列化的引用网，**手写不现实**，所以用一段编辑器脚本生成它。
这样场景可以随时重建，也能在批处理里跑，不依赖人在编辑器里点鼠标。

**Files:**
- Create: `Assets/Scripts/Unity/Editor/MyWorld.Unity.Editor.asmdef`
- Create: `Assets/Scripts/Unity/Editor/PreviewSceneBuilder.cs`
- Create: `Assets/Scenes/Preview.unity`（由脚本生成，不手写）

- [ ] **Step 1: 建编辑器程序集**

创建 `Assets/Scripts/Unity/Editor/MyWorld.Unity.Editor.asmdef`：

```json
{
  "name": "MyWorld.Unity.Editor",
  "rootNamespace": "MyWorld.Unity.EditorTools",
  "references": [
    "MyWorld.Core",
    "MyWorld.Unity"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: 写场景生成脚本**

创建 `Assets/Scripts/Unity/Editor/PreviewSceneBuilder.cs`：

```csharp
using System.IO;
using MyWorld.Unity.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 生成里程碑 1 的验收场景。场景文件是 YAML 引用网，手写不现实，
    /// 一律由这段脚本重建——既能在编辑器菜单里点，也能被批处理 -executeMethod 调用。
    /// </summary>
    public static class PreviewSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Preview.unity";

        [MenuItem("MyWorld/重建预览场景")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateCamera();

            var world = new GameObject("世界");
            world.AddComponent<WorldBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"预览场景已生成：{ScenePath}");
        }

        private static void CreateLight()
        {
            var gameObject = new GameObject("方向光");
            Light light = gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            // 略微偏斜，让方块的三个可见面亮度分开，轮廓才清楚
            gameObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateCamera()
        {
            var gameObject = new GameObject("相机");
            gameObject.tag = "MainCamera";

            Camera camera = gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.65f, 0.95f);
            // 默认 1000 太远、地形又不高，缩到 500 让深度精度好一些
            camera.farClipPlane = 500f;

            gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<FreeFlyCamera>();

            // 站在地表之上、稍微退开一点，Play 之后立刻能看到地形而不是卡在土里
            gameObject.transform.position = new Vector3(0f, 95f, -40f);
            gameObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
        }
    }
}
```

- [ ] **Step 3: 批处理跑一遍编译 + 生成场景**

先确认没有别的 Unity 实例占着这个项目，否则会报 `another Unity instance is running`。

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . `
  -executeMethod MyWorld.Unity.EditorTools.PreviewSceneBuilder.Build `
  -quit -logFile scene-build.log
```

**退出码不可信——Unity 崩溃时照样返回 0**，必须查日志：

```bash
grep -n "error CS\|Exception\|预览场景已生成" scene-build.log
```

预期：没有 `error CS`、没有 `Exception`，能看到「预览场景已生成：Assets/Scenes/Preview.unity」，
且 `Assets/Scenes/Preview.unity` 确实存在。

有编译错误就回到对应任务修，改完重跑本步。

- [ ] **Step 4: 跑一遍 EditMode 测试，确认新程序集没破坏既有测试**

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode `
  -testResults unity-test-results.xml -logFile unity-tests.log
```

```bash
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' unity-test-results.xml | head -1
```

预期：`total="186" passed="186" failed="0"`

- [ ] **Step 5: 提交**

日志与测试结果是一次性产物，不入版本管理。

```bash
git add Assets/Scripts/Unity/Editor Assets/Scenes
git commit -m "渲染层: 预览场景生成脚本"
```

如果 `git status` 里出现了 `scene-build.log` / `unity-tests.log` / `unity-test-results.xml`，
把它们加进 `.gitignore` 再提交。

- [ ] **Step 6: 人工验收（唯一无法自动化的一步）**

这一步需要人打开编辑器，无法在无人值守时完成。**验收前置条件不满足时，把结论如实
记下来，不要写成"已验证"。**

打开 Unity，加载 `Assets/Scenes/Preview.unity`，按 Play，逐条核对：

| 检查项 | 通过标准 |
| --- | --- |
| 地形出现 | 视野里有连成片的体素地形，不是空场景 |
| 贴图正确 | 草顶是草、草侧是草侧、往下是土和石头，没有整片洋红 |
| 贴图无缝 | 大平面上看不到规则的网格状接缝（贪心合并 + Repeat 环绕的关键验证点） |
| 贴图不糊 | 32×32 像素边缘锐利，不是模糊插值（Point 过滤生效） |
| 区块拼接 | 区块之间没有缝隙，也没有多出来的重叠面（接缝剔除正确） |
| 相机可用 | WASD/QE 能飞，按住右键能转视角 |
| 控制台 | 打印出「世界就绪：49 根区块列 …」，没有报错 |

任何一项不通过，记录现象后回到对应任务排查，不要带着问题往下走。

---

## Task 14: 接入 URP（最后一步，可整体回滚）

前面所有代码在内置渲染管线下已经能跑。这一步是**纯升级**：装 URP 包、建管线资产、
在项目设置里挂上。包解析涉及网络，是整个计划里最容易卡住的一环，所以放最后，
且刻意只占一个提交——**失败就 `git revert` 这一个提交，前面 13 个任务的成果一点不受影响**。

**Files:**
- Modify: `Packages/manifest.json`
- Create: `Assets/Settings/UniversalRenderPipelineAsset.asset`（由编辑器生成）
- Modify: `ProjectSettings/GraphicsSettings.asset`

- [ ] **Step 1: 加包**

在 `Packages/manifest.json` 的 `dependencies` 里加一行（保持字母序）：

```json
"com.unity.render-pipelines.universal": "14.0.11",
```

**版本必须是 14.x**——URP 17.x 是 Unity 6 的，2022.3 上不存在，会把包解析卡死。

- [ ] **Step 2: 让 Unity 解析包**

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . -quit -logFile urp-import.log
```

```bash
grep -n "error\|Failed to resolve\|universal" urp-import.log | head -20
```

预期：能看到 URP 包被解析导入，没有 `Failed to resolve`。

**解析失败的处理方式**：不要在这里反复试。撤掉 `Packages/manifest.json` 的改动，
在计划末尾如实记下「URP 未接入，当前使用内置管线」，然后结束——内置管线下画面照样是对的，
URP 的收益（更好的光照与后处理）属于后续里程碑。

- [ ] **Step 3: 建管线资产并挂上**

在编辑器里：`Assets` 右键 → `Create` → `Rendering` → `URP Asset (with Universal Renderer)`，
存到 `Assets/Settings/UniversalRenderPipelineAsset.asset`；
然后 `Edit` → `Project Settings` → `Graphics` → `Scriptable Render Pipeline Settings` 指到该资产。

`Project Settings` → `Quality` 下每个质量档位的 `Render Pipeline Asset` 也指同一个资产，
否则切换质量档时会掉回内置管线。

- [ ] **Step 4: 确认材质走的是 URP**

`BlockMaterialLibrary.FindShader` 会优先找 `Universal Render Pipeline/Lit`。装了 URP 之后
重新 Play，画面应当和内置管线下基本一致（可能整体亮度略有差异，这是正常的）。

**如果地形变成洋红**，说明材质用的还是内置 Standard shader 而 URP 不认——检查
`Shader.Find("Universal Render Pipeline/Lit")` 是否返回了 null（URP 资产没挂上时会）。

- [ ] **Step 5: 重跑测试与场景生成，确认没被 URP 破坏**

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

预期：186 个全部通过（Core 与渲染管线无关，这步是回归确认）

- [ ] **Step 6: 提交**

```bash
git add Packages/manifest.json Packages/packages-lock.json Assets/Settings ProjectSettings
git commit -m "渲染层: 接入 URP 14.0.11"
```

---

## 完成后的状态

做完这 14 个任务，仓库里应当有：

- Core 层多出「贴图索引」这条贯穿数据流：`BlockRegistry.TextureNames` →
  `IBlockSource.GetTextureIndex` → `MeshBuffer.QuadTextures` → `Submesh[]`，
  全程只有 int，Core 仍然零 Unity 依赖
- Core 测试从 155 涨到 186
- `Assets/Scripts/Unity/` 从空目录变成三个目录（`Bootstrap` / `Rendering` / `Editor`）共 7 个文件
- 一个可重建的预览场景，按 Play 能飞着看地形

**下一个里程碑（玩家层）会用到本里程碑的这些东西**：`ChunkSectionView.Rebuild` 会被
挖掘/放置后的局部重建复用；`WorldBootstrap` 里固定范围的生成循环会被跟随玩家的
动态加载替换；`FreeFlyCamera` 会被真正的玩家控制器替换。

## 已知的技术债（记下来，不在本里程碑处理）

- 网格生成全在主线程同步做，区块一多会卡住一帧。Burst/Job 化留给「打磨」里程碑
- 每个区块段一个 GameObject + 一份 Mesh，没有对象池，动态加载后会频繁 GC
- `ChunkSectionView` 用静态共享 `MeshBuffer`，天然不支持多线程，多线程化时要一起改
- 一个区块段有几种贴图就有几个 drawcall。真成瓶颈了再上 Texture2DArray（设计文档
  「考虑过的方案」里的方案 B）
- 没有接顶点光照/AO，Core 的 `Lighting` 数据目前没被渲染用到
