# 楼梯/台阶方块对贪心网格的影响评估（m11 W3-5 杂项）

> 任务卡 W3-5 的收尾杂项：楼梯/台阶（非整块形状）需要贪心网格做什么改动、工作量多大、
> 建议做还是不做。**本文只评估不实现**，写给集成点④终审决策用。
> 关联：`docs/superpowers/plans/2026-08-17-milestone-11_part4.md` Task W3-5；
> 楼梯贴图需求已提（`art/requests/blocks/stairs-stone|planks|bricks.md`），方块未注册。

## 结论（先行）

**建议本期不做，m11 收尾不纳入。** 贪心网格、碰撞、存档三层的核心假设同时被非整块形状打破，
改动面跨 `GreedyMesher` / `IBlockSource` / `ISolidBlockSource` / `BlockDefinition` /
`ChunkSection` 存储语义，双链回归面大，与「产品化收尾」的里程碑目标冲突。
若后续（m12+）要做：**先做台阶（半块高、无朝向）验证形状通道，再做楼梯（带朝向）**，
按本文方案 A + 「朝向拆成 4 个方块 id」落地，估 5–8 个工作日（§4）。

## 1. 现状：贪心网格靠四条不变量吃饭

`Assets/Scripts/Core/Meshing/GreedyMesher.cs` 的整套设计建立在四条假设上：

| # | 不变量 | 出处 | 非整块形状如何打破 |
| --- | --- | --- | --- |
| 1 | **面只出现在格界平面上**：每个候选面都在两格之间的整数平面（`cursor[axis]` 从 -1 扫到 Size-1） | `BuildMask` | 台阶顶面在 y+0.5 格内平面；楼梯竖直面有半格偏移（x+0.5/z+0.5）。扫描线根本扫不到这些平面 |
| 2 | **遮挡是二元的**：`IsSolid(near) != IsSolid(far)` 才出面（`ChunkMeshSource.IsSolid` = `Opaque`） | `BuildMask` | 台阶贴着整块，两者都 Opaque → mask=0，台阶的上半竖面被整误剔；反过来台阶上方是空气，顶面又要出。剔除需要「形状对形状」的几何判定 |
| 3 | **同 id 同朝向才能合并**：mask 值 = ±blockId，贪心扩展要求整片同值；UV 按 `(0,0)..(width,height)` 格数铺开 | `EmitQuads` / `AddQuad` | 同一 id 的楼梯有 4 种朝向，mask 只编码 id 不编码朝向，会把朝向不同的面并成一张错面。UV 也不能再按整格铺（半格高的面只该取贴图的一半） |
| 4 | **方块没有状态**：`ChunkSection` 是「局部调色板 + 位打包」只存 block id，无朝向/半高元数据 | `ChunkSection.cs` | 楼梯的朝向、上下倒放**无处存储**。这是数据层根因，比 mesher 本身更麻烦 |

配套系统同样按「一格一布尔」设计：

- **碰撞/移动**：`ISolidBlockSource.IsSolidAt(x,y,z)` 布尔（`WorldSolidSource`），
  `VoxelCollision.Move` 按整格 AABB 推进——楼梯的半格碰撞（走上台阶不用跳）正是它的核心卖点，
  布尔源表达不了。
- **射线拾取**：`VoxelRaycaster.Cast` 同走 `ISolidBlockSource`——半格命中的精度损失（整格命中）可接受，但无法原样复用。
- **光照**：`ChunkLightSystem` 按 Opaque 参与遮挡——楼梯若标 `opaque:true` 会照常挡光且把相邻面的剔除搞乱；参照玻璃先例标 `opaque:false` 最省事（面剔除全部由形状通道自理，见 §3 方案 A）。

## 2. 做楼梯需要动什么（方案对比）

### 方案 A：双通道——整块贪心不动，非整块走「形状网格」通道（若做，推荐）

- `BlockDefinition` 加 `shape` 字段（`full_cube` 默认 / `slab` / `stairs`），`blocks/*.json` 数据驱动。
- 贪心网格**原样保留**，只服务整块（`IsSolid` 判定自动把 shape≠full_cube 当非遮挡处理即可），
  现有全部行为与测试零回归——这是选它的核心理由。
- 新增 `ShapeMesher`（Core）：把楼梯分解成 ≤6 个半格 AABB（底板 16×8×16 + 后半上板 16×8×8，
  台阶=2 个 AABB，楼梯=2 个），逐块出矩形面，邻接剔除规则三级：
  1. 邻格是整块 Opaque → 剔掉被完全覆盖的面；
  2. 邻格同 shape 同朝向贴邻 → 剔共享整面（两台阶叠放合一块整砖）；
  3. 其余一律出面（不同朝向楼梯相接处允许重复面，Minecraft 也这么近似）。
- 顶点直接追加进现有 `MeshBuffer`（`QuadTextures` / 子网格按贴图分桶的管线复用），
  集成点在 `ChunkSectionView`：`GreedyMesher.Build` 之后追加一次 `ShapeMesher.Append`。
- **朝向存储**：不扩 `ChunkSection`——每材质 ×4 朝向各注册一个方块 id
  （`stairs_stone_n/e/s/w` 这类），放置时按玩家朝向选 id（`BlockInteraction` 放置路由已拿得到
  视线方向）。零存储改动，代价是注册表条目 ×4（纯 JSON，量小）。
- UV：形状面按 AABB 实际尺寸取 UV（半格高取 `(0,0)..(w,0.5)` 贴图上半），
  背离「UV 按格数铺开」约定但只影响形状通道，整块通道不受污染。

### 方案 B：2× 细分贪心（把世界当 32³ 半格跑同一算法）——否决

数据体积 ×8（或运行时展开、每 section 展开成本高），贴图密度/`ChunkSerializer` 压缩率全乱，
且楼梯的「后上板」仍是 L 形不是半格盒子，细分到 2× 也拼不出来。否决。

### 方案 C：`ChunkSection` 调色板加 per-block 状态位存朝向——否决

动二进制布局必须 bump `FormatVersion` + 旧档迁移（CLAUDE.md 存档铁律），
位打包/调色板全套测试重验，而「朝向拆 id」能零成本达到同一效果。否决。

## 3. 工作量估计（方案 A 全链）

| 项 | 内容 | 估计 |
| --- | --- | --- |
| 数据层 | `shape` 字段 + 12 个楼梯方块 JSON（3 材质 ×4 朝向）+ `_format.md` + `BlockDefinitionFilesTests` 守卫 | 0.5 天 |
| ShapeMesher | AABB 分解表 + 三级剔除 + UV 半格规则 + `MeshBuffer` 追加（约 300–400 行 Core 代码） | 1.5–2 天 |
| 碰撞 | `ISolidBlockSource` 旁路新增 AABB 查询接口（如 `GetCollisionBoxes`），`VoxelCollision.Move` 改按盒推进，`PlayerMotor` 全链回归 | 1.5–2 天 |
| 放置路由 | `BlockInteraction` 按朝向选 id + 破坏/掉落表条目 | 0.5 天 |
| 视觉/贴图 | `--ui-shot` 不覆盖世界内网格； stairs 贴图入库 + 半格 UV 目检 | 0.5 天 |
| 测试 | 形状分解/剔除/UV/碰撞盒/放置朝向/存档往返，双链同源 | 1–2 天 |
| **合计** | | **5–8 个工作日** |

风险点：碰撞改造是最大不确定项（`VoxelCollision` 是手感基石，逐轴推进算法要从「格布尔」
换成「格内盒列表」，回归面覆盖玩家/掉落物/生物三套移动）；其次是与流式网格（毫秒预算分帧）
的性能叠加——形状通道无贪心合并，最坏一 section 全楼梯 ≈ 4 万三角（实况远低，可先不做合并，
热点出现再加「同形同向整面贪心」）。

## 4. 建议与中间选项

1. **本期不做**（建议）：m11 已进入终审收口，5–8 天 + 大回归面与「产品化」目标冲突；
   楼梯不是孩子验收剧本里的必选项。
2. 若 m12 立项：**先做台阶（slab，无朝向、单 AABB、无旋转）**——它用方案 A 的最小子集
  （形状表 + 形状通道 + 碰撞盒）就能验证三层假设的改造是否站得住，大约是上表的一半工作量；
   站住后再上楼梯（只加朝向拆 id + 双 AABB，mesher 通道已就绪）。
3. 想在 m11 内给孩子一个近似体验的话：跳过楼梯，用「整块 + 现有跳跃（1 格自动上台阶手感
   已由碰撞 Skin/步高近似）」即可，零改动。
