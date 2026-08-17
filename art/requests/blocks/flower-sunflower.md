# 向日葵（flower-sunflower）

## 用途

花草方块（十字面片渲染）。高大黄花，收集卡「植物 · 向日葵」同源。
背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，总高约 80%（花草里最高的一种） |
| 透明面积 | **45% – 60%** |
| 颜色数 | 6 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 花瓣暗金 | `#DCAE3A` |
| 花瓣亮金 | `#F7DA7A` |
| 花盘深棕 | `#4E3826` |
| 花盘棕 | `#6B4E2E` |
| 茎绿 | `#3F7A2E` |
| 叶绿 | `#52993B` |
| 键控色 | `#FF00FF`（后处理删除） |

金黄与 `flower-dandelion` 共用两色，花盘棕用泥土系——不引入新色。

## 视觉描述

- 单株：2 像素粗茎从底边到约 55% 高度，茎上 2 片**大心形叶**
  （4–5 像素宽）左右张开
- 茎顶一个大花盘：14–16 像素直径——外圈 12–16 片长尖金瓣放射排列
  （暗金打底/亮金受光两层），中央 7–8 像素的**棕色花盘**
  （两档棕 + 点状籽纹）
- 总高约 80%，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single sunflower on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one sunflower plant centered. A thick 2-pixel green stem rises from
the bottom edge to about 55 percent height, carrying two large heart-shaped
leaves 4 or 5 final pixels wide fanning left and right. On top sits one big
flower head 14 to 16 final pixels across: a ring of twelve to sixteen long
pointed golden petals radiating outward (dark gold base, bright gold lit
side), around a central brown disc of 7 or 8 pixels with a dotted seed
pattern. Total plant height is about 80 percent of the canvas. Keep at least 2
final pixels of magenta on the left and right sides; petals never touch the
top edge.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #DCAE3A and #F7DA7A for petals, #4E3826 and
#6B4E2E for the disc, #3F7A2E and #52993B for stem and leaves.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no field, no vase, no
multiple sunflowers, no sky, no sun, no clouds, no text, no watermark, no
gradient, no blur, no 3D render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, field, sun, sky, clouds,
vase, multiple sunflowers, text, watermark, blur, 3D render, perspective,
anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 6 色
3. 统计透明面积 45% – 60%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 80%，金瓣棕盘大花头
- [ ] 透明占比 45% – 60%，无残留洋红与紫边
- [ ] 与 `flower-dandelion.png` 并排，金色一致、体型明显更大
