# MyWorld.Unity 渲染层设计（里程碑 1）

## 背景与目标

Core 层（`Assets/Scripts/Core`）已完成：体素坐标、区块存储、贪心网格合并、光照、物理、
存档、方块数据，155 个测试全部通过。`MyWorld.Unity` 层目前完全是空的——没有任何代码
把 `GreedyMesher` 产出的 `MeshBuffer` 变成屏幕上能看见的东西。

这是整个游戏路线图（渲染层 → 玩家层 → 存档接入 → UI → 打磨）的第一个里程碑，也是后面
所有里程碑的前提：没有能跑的场景，玩家控制、交互、UI 都无从谈起。

**验收标准**：在 Unity 编辑器 Play 模式下，用一个自由飞行相机能够漫游一片固定范围的
体素地形，看到正确的方块贴图（无接缝、无拉伸）、正确的区块拼接（无缝隙、无重叠面）。

## 范围（本里程碑只做这些）

- `MyWorld.Unity` asmdef：引用 `MyWorld.Core`，允许 `UnityEngine.*`
- 从 `Assets/StreamingAssets/blocks/textures/*.png` 为每张贴图生成一个 `Material`
  （Repeat 环绕 + Point 过滤，贴合 32×32 像素风格，禁用 mipmap 防止远处模糊融混）
- 扩展 Core 的 `MeshBuffer`：贪心合并输出的每个 quad 额外记录一个"贴图索引"，
  按此把索引缓冲重排成多个 submesh（保持 Core 不引用 Unity 类型——记录的是 int 索引，
  不是 `Material`/`Texture` 对象）
- `ChunkSectionView`：每个非空 `ChunkSection`（16×16×16）对应一个 GameObject，
  `MeshFilter` + `MeshRenderer`，`sharedMaterials` 关联对应贴图的 Material
  （同一份 Material 资源被所有区块复用，以便 SRP Batching）
- URP 14.x 作为**最后一步、可回滚**地接入。渲染代码按"有 URP 用 URP Lit，没有就退回内置
  管线的 Standard"编写，因此包解析若失败，回滚这一个提交即可，前面所有工作照常可用
- `WorldBootstrap`：测试场景，固定生成 N×N 区块列（比如 12×12）+ 自由飞行相机，
  用于肉眼验收

## 范围外（明确不做，留给后续里程碑）

- 动态加载/卸载（跟随玩家位置增删区块）——这是"打磨"里程碑的性能工作，不阻塞"看见世界"
- Burst/Job 多线程网格生成——现在用主线程同步生成，正确性优先
- `Mesh.AllocateWritableMeshData` 零拷贝上传——本里程碑先用 `SetVertices`/`SetIndices`
  逐个拷贝，代码量小得多。等 drawcall 与上传耗时真的成为瓶颈了再换
- 光照渲染（`Lighting` 层数据已存在，但顶点光照/AO 接入放到后面，本里程碑先出无光照或
  URP 默认方向光下的贴图效果）
- 任何玩家交互（挖掘、放置）——里程碑 2 的内容
- Texture2DArray/自定义 shader（见下方"考虑过的方案"）

## 方案取舍：方块贴图怎么接进 Unity

**方案 A（采用）：材质分 submesh。** 每张贴图对应一个 URP 内置 Lit/Simple Lit 材质，
环绕模式设为 Repeat。贪心合并后的 quad 本来就用 `(0,0)..(width,height)` 铺开 UV
（`GreedyMesher.AddQuad`），这正好是为纹理环绕采样设计的——不用任何自定义 shader，
Unity 材质的 Repeat 环绕原生支持。网格按贴图分组导出多个 submesh，一个区块段一个
GameObject，`MeshRenderer.sharedMaterials` 数组对应各 submesh。

代价：一个区块段如果同时包含较多种贴图（比如草方块的顶/侧/底三种 + 石头 + 矿石），
会拆成对应数量的 submesh/drawcall。以现在的方块种类规模（7 个内置 + 个位数新方块）
这个数字很小，可接受。

**方案 B（放弃）：Texture2DArray + 自定义 shader。** 所有贴图打进一张 Texture2DArray，
每个 quad 额外带一个层号，自定义 shader 采样。整个区块段一次 drawcall，性能更好，
但需要写自定义 URP shader（重新实现光照模型或用 Shader Graph 接 Lit 分支）、维护
Texture2DArray 构建流程，复杂度高出一截，且这个项目目前没有 shader 开发经验积累。
这个方案值得在后续"打磨"里程碑、drawcall 真正成为瓶颈时再引入。

## 数据流

```
BlockRegistry (贴图名 → 贴图索引表)
        │
        ▼
ChunkMeshSource (IBlockSource, 已存在)   ← 扩展：GetTextureIndex(blockId, face)
        │
        ▼
GreedyMesher.Build<TSource>              ← 扩展：每个 quad 记一个贴图索引
        │
        ▼
MeshBuffer (Positions/Normals/Uvs/Indices + QuadTextures)
        │
        ▼
MeshBuffer.SplitByTexture → Submesh[]    ← 按贴图重排索引，使同贴图三角形连续
        │
        ▼ (Unity 层，新增)
ChunkMeshBuilder: MeshBuffer + Submesh[] → Unity Mesh (多 submesh)
        │
        ▼
ChunkSectionView: GameObject + MeshFilter + MeshRenderer(sharedMaterials)
        │
        ▼
WorldBootstrap: 固定范围内为每个非空 ChunkSection 生成一个 ChunkSectionView
```

## 测试策略

- Core 层新增的"quad 按贴图分组"逻辑（去重、分组、索引稳定性）用 NUnit 写单元测试，
  和现有 155 个测试一样跑 `dotnet test`
- Unity 侧的材质生成、Mesh 上传、GameObject 生成没有自动化断言的空间——这部分的验收
  是"在编辑器 Play 模式里飞一圈，看贴图和拼接对不对"，计划里会写清楚这是人工验收项，
  不会假装它有测试覆盖
