# B-18 工作台（顶面 + 侧面）

一次需求包含**两张贴图**：`crafting_table-top`（俯视工作台顶面，能看到工具格）与
`crafting_table-side`（侧面，看到木板侧面 + 锯齿斜线）。玩家在工作台前合成时，
顶面才会朝向玩家（其它视角全部是侧面）。

## 平铺要求

| 贴图 | 平铺要求 | 原因 |
| --- | --- | --- |
| `crafting_table-top` | **四边无缝** | 工具格背景的格线必须对齐 |
| `crafting_table-side` | **四边无缝** | 房屋墙面会大面积使用 |

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 各 32 × 32 像素 |
| 生成尺寸 | 各 1024 × 1024（再降采样） |
| Alpha | 无，完全不透明 |
| 颜色数 | 各 5 – 8 色 |

## 调色板（严格使用）

两张共用基础木板色，加一个工具格 / 锯齿的深色：

| 用途 | HEX |
| --- | --- |
| 板缝 | `#6B4E2E` |
| 主色 | `#8A6741` |
| 亮部 | `#9C7549` |
| 工具格 / 锯齿线 | `#1A1A1A` |

侧面额外需要 `#B98D57`（最亮的高光，让锯齿边有立体感）。

## 视觉描述

### `crafting_table-top` 顶面

俯视看到的工作台顶面——**3×3 工具格的栅格**。

- 中央一个 **3×3 的格子阵列**，每个格约 8×8 像素（占整张图大部分）
- 工具格的边框是 1 像素宽的 `#1A1A1A` 深色线，**水平线与竖直线必须严格对齐到 32×32
  的整数分界**（第 0/8/16/24 行 + 列），否则 UV 平铺后格线会错位
- 格内是纯木板黄 `#9C7549`，不要在格内画具体工具图标（图标由 UI 层另外提供）
- 3×3 阵列的**最外圈四周**留 4 像素宽的木板边框 `#8A6741`，作为工作台的"台面边缘"
- 板内可以有几道木纹短横线（1 像素），但**不能穿越工具格的边界线**

### `crafting_table-side` 侧面

正前方看到的工作台侧面。

- **上半**（约第 0–8 行）：从顶面侧切下来的木板条（与 `planks` 同色），水平方向有 1 像素
  的板缝 `#6B4E2E`
- **下半**（约第 9–31 行）：工作台的木质柜身，木板色打底，**斜向锯齿线**贯穿中央
  - 锯齿由几条 `#1A1A1A` 斜线段组成（每段长约 4–6 像素，斜率约 1:1 或 2:1），
    形成锯条形状
  - 锯齿只画中央一条（高约 6–8 像素），不要满图都画
  - 锯齿两侧用 `#1A1A1A` 描一个细长的把手轮廓（也可省略）
- 左右边缘必须严格无缝

## AI 提示词

### `crafting_table-top`

```
A seamless tileable pixel art texture of a crafting table top viewed from directly
above, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination across the entire image.

Content: a wooden tabletop with a 3x3 grid of dark outlined square slots in the
center. The grid lines are exactly 1 pixel wide and must align to the 8-pixel
boundaries of the image (rows/columns 0, 8, 16, 24, 32) so that when tiled on
cube faces the grid stays square. Each slot interior is filled with a single
medium wood tone. The outer 4 pixels of the image on all four sides are a darker
wood border representing the tabletop edge.

Color palette strictly limited to: wood border #6B4E2E and #8A6741, slot fill
#9C7549, grid line and outline #1A1A1A.

CRITICAL: The grid lines must lie EXACTLY on the 8-pixel boundaries so that
stacked and adjacent placements keep the grid square. No skew, no rotation,
no perspective.

No tool icons inside the slots, no hammer, no saw, no anvil, no text, no watermark,
no vignette, no drop shadow, no gradient, no 3D render, no perspective.
```

### `crafting_table-side`

```
A seamless tileable pixel art texture of a crafting table viewed from the front,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: the upper third (about rows 0-8) shows the side edge of the tabletop
with horizontal plank seams aligned to the image boundaries. The lower two thirds
(rows 9-31) show the front of a wooden cabinet with a single stylized saw blade
silhouette drawn in the center, about 6-8 pixels tall, made of short diagonal
line segments forming a zigzag sawtooth pattern, outlined in #1A1A1A. The saw is
a single motif, not repeated across the whole image.

Color palette strictly limited to: #6B4E2E, #8A6741, #9C7549, #B98D57 for the wood
body, and #1A1A1A exclusively for the saw outline and plank seams.

CRITICAL: tiles seamlessly on all four edges. Horizontal plank seams in the upper
third must continue across the left and right edges.

No tools, no hammer, no nails, no screws, no bolts, no real saw, no reflective
metal, no text, no watermark, no vignette, no gradient, no 3D render,
no perspective, no glow.
```

## 负面提示词（两张通用）

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline
around whole image, hammer, wrench, screw, nail, bolt, real tools, metal, reflective
metal, shiny, glass, perspective, 3D render, photorealistic, text, watermark,
signature, blur, checkerboard, transparent background
```

## 后处理

1. 两张分别最近邻降采样到 32 × 32
2. 各自量化到对应调色板
3. **顶面强制检查**：第 0/8/16/24 行 + 列必须全部是 `#1A1A1A`（格线），
   不对齐则手动补线
4. 侧面必须确保第 0 行与第 31 行板缝位置对齐（`log-side` 同款检查）
5. 偏移半幅自检接缝
6. Alpha 全部置为 255

## 跨资源一致性

- 顶面 / 侧面的木板色必须与 `planks` 同色系，使工作台拆开看是木制品
- 侧面锯齿的 `#1A1A1A` 描线粗细与 `log-side` 沟槽主色一致

## 验收标准

- [ ] 两张尺寸均恰为 32 × 32
- [ ] 顶面 3×3 格的格线必须落在第 0/8/16/24 行 + 列上
- [ ] 顶面外圈 4 像素是 `#8A6741` 木板边框
- [ ] 侧面锯齿是单一图形，不是满图重复
- [ ] 侧面上下偏移半幅后板缝对齐
- [ ] 两张并排看起来是同一个工作台
- [ ] 颜色数 ≤ 8
- [ ] 无任何像素 Alpha < 255
