# 棕蘑菇（mushroom-brown）

## 用途

花草方块（十字面片渲染）。森林地表的可食用棕蘑菇，与红蘑菇同版式
不同色。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中偏下，株高约 40% – 50% |
| 透明面积 | **50% – 68%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 伞盖深棕 | `#6B4E2E` |
| 伞盖主棕 | `#8A6741` |
| 伞缘亮棕 | `#9C7549` |
| 菌褶深木色 | `#4E3826` |
| 菌柄米白 | `#DCD8CC` |
| 键控色 | `#FF00FF`（后处理删除） |

伞盖三色即 `planks` 木板系——棕蘑菇长在木头上，色系同源。

## 视觉描述

- 单朵：**半球棕伞**直径约 10–12 像素，位于画面中部偏下，与
  `mushroom-red` 同版式（大小/位置一致，只换配色与去白斑）
- 伞面主棕 `#8A6741`，伞顶偏深 `#6B4E2E`，伞缘一圈 1 像素亮棕 `#9C7549`
- 伞下 2–3 像素菌褶环 `#4E3826`
- 短粗菌柄 3–4 像素高、3 像素宽，米白 `#DCD8CC`
- 整体高约 40%–50%，菌柄触底边，左右各留 ≥3 像素洋红边

## AI 提示词

```
A pixel art sprite of a single brown mushroom on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one brown mushroom centered, sitting in the lower half of the canvas.
A dome-shaped brown cap 10 to 12 final pixels across, main brown #8A6741 with
a darker #6B4E2E top and a 1-pixel lighter rim #9C7549 around the edge. Under
the cap a 2 or 3 pixel ring of dark brown gills #4E3826, then a short stubby
cream stalk 3 or 4 pixels tall and 3 pixels wide #DCD8CC. No spots on the cap.
Total height is about 40 to 50 percent of the canvas. The stalk touches the
bottom edge; keep at least 3 final pixels of magenta on the left and right
sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the mushroom. No partial
transparency.

Palette strictly limited to: #6B4E2E, #8A6741, #9C7549 for the cap, #4E3826
for gills, #DCD8CC for the stalk.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no grass, no log, no
multiple mushrooms, no spots, no red, no text, no watermark, no gradient, no
blur, no 3D render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, grass, log, stump,
multiple mushrooms, spots, red cap, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计透明面积 50% – 68%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 棕伞无斑短柄，与 `mushroom-red.png` 并排版式（大小/位置）一致
- [ ] 透明占比 50% – 68%，无残留洋红与紫边
