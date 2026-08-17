# 红蘑菇（mushroom-red）

## 用途

花草方块（十字面片渲染）。森林地表的小型红蘑菇，可采（食用/合成系
第 1 波农业赛道接）。背景洋红键控为透明。

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
| 伞盖暗红 | `#A03028` |
| 伞盖主红 | `#C4362A` |
| 菌褶深红 | `#8E2420` |
| 白斑 | `#E8E4DC` |
| 菌柄米白 | `#DCD8CC` |
| 键控色 | `#FF00FF`（后处理删除） |

红伞白点毒蘑菇的经典读法；红全部为 B < G 暖红。

## 视觉描述

- 单朵：**半球红伞**直径约 10–12 像素，位于画面中部偏下，伞缘微微内卷
- 伞面主红 `#C4362A`，伞顶偏暗 `#A03028`，散布 **3–4 个 1–2 像素白斑**
  （不要排成一条线）
- 伞下 2–3 像素高的菌褶环 `#8E2420`
- 短粗菌柄 3–4 像素高、3 像素宽，米白 `#DCD8CC`
- 整体高约 40%–50%，菌柄触底边，左右各留 ≥3 像素洋红边

## AI 提示词

```
A pixel art sprite of a single red mushroom on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one red toadstool centered, sitting in the lower half of the canvas.
A dome-shaped red cap 10 to 12 final pixels across with a slightly rolled-in
rim, main red #C4362A with a darker #A03028 patch on top, decorated with three
or four scattered white spots of 1 or 2 pixels, never in a straight line. Under
the cap a 2 or 3 pixel ring of deep red gills #8E2420, then a short stubby
cream stalk 3 or 4 pixels tall and 3 pixels wide #DCD8CC. Total height is about
40 to 50 percent of the canvas. The stalk touches the bottom edge; keep at
least 3 final pixels of magenta on the left and right sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the mushroom. No partial
transparency.

Palette strictly limited to: #A03028, #C4362A for the cap, #8E2420 for gills,
#E8E4DC for spots, #DCD8CC for the stalk. Warm red only, never magenta tint.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no grass, no log, no
multiple mushrooms, no fairy ring, no text, no watermark, no gradient, no blur,
no 3D render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, grass, log, stump,
multiple mushrooms, fairy ring, slime, text, watermark, blur, 3D render,
perspective, anti-aliasing, cool pink
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计透明面积 50% – 68%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 红伞白点短柄，一眼读出「红蘑菇」
- [ ] 白斑 3–4 个且不排成直线
- [ ] 透明占比 50% – 68%，无残留洋红与紫边
