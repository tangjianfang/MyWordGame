# 虞美人（flower-poppy）

## 用途

花草方块（十字面片渲染，与 `sapling` 同管线）。草原/森林地表装饰，
第 1 波植被赛道按 `flowers.json` 投放。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，总高约 70% |
| 透明面积 | **45% – 65%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 花瓣暗红 | `#A03028` |
| 花瓣主红 | `#C4362A` |
| 花瓣亮红 | `#E04A32` |
| 花心黑 | `#1F1F1F` |
| 茎叶绿 | `#3F7A2E` / `#52993B` |
| 键控色 | `#FF00FF`（后处理删除） |

红系全部 B < G（偏暖），避免被「去洋红边」误删；花心黑用煤黑系。

## 视觉描述

- 单株：一根 1 像素绿茎从底边向上到约 40% 高度，带 2 片 2–3 像素的小尖叶
- 茎顶一朵**虞美人**：花头 10–12 像素宽的碗形四瓣花，瓣缘起伏
- 瓣面三层红（暗底/主红/亮红）做体积感，中央 2×2 像素黑花心
- 总高约 70%（22–23 像素），左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single red poppy flower on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one poppy plant centered. A thin 1-pixel green stem rises from the
bottom edge to about 40 percent height with two small pointed leaves, then one
cup-shaped flower head 10 to 12 final pixels wide of four rounded petals with
slightly ruffled edges. Petal shading uses three red layers (dark base, main
red, bright red) and a 2x2 black flower center. Total plant height is about 70
percent of the canvas. Keep at least 2 final pixels of magenta on the left,
right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #A03028, #C4362A, #E04A32 for petals, #1F1F1F for
the flower center, #3F7A2E and #52993B for stem and leaves. Warm reds only.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no grass, no vase, no
multiple flowers, no buds, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, grass, vase, bouquet,
multiple flowers, buds, field of poppies, text, watermark, blur, 3D render,
perspective, anti-aliasing, cool pink, magenta petals
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表色值
3. 统计透明面积 45% – 65%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 70%，红花黑心
- [ ] 透明占比 45% – 65%
- [ ] 红全部为暖红（无 B > G 像素），无残留洋红与紫边
- [ ] 远看是一朵红 花、近看有花瓣层次
