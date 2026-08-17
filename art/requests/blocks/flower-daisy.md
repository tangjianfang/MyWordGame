# 雏菊（flower-daisy）

## 用途

花草方块（十字面片渲染）。草原最朴素的白花黄心，与各色花混播时的
「留白」。背景洋红键控为透明。

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
| 花瓣白 | `#F2EFE8` |
| 花瓣阴影 | `#DCD8CC` |
| 花心黄 | `#F7DA7A` |
| 茎绿 | `#3F7A2E` |
| 叶绿 | `#52993B` |
| 键控色 | `#FF00FF`（后处理删除） |

花瓣白是暖白（B < G），不与洋红键控冲突；花心黄与蒲公英共用。

## 视觉描述

- 单株：1 像素细茎从底边到约 42% 高度，带 2 片小圆锯齿叶
- 茎顶一朵**雏菊**：10–12 像米花头——一圈 10–12 片白色长圆瓣放射排列
  （每片 1–2 像素宽，瓣根带一点阴影白），中央 3×3 像素黄花心
- 总高约 70%，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single white daisy on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one daisy plant centered. A thin 1-pixel green stem rises from the
bottom edge to about 42 percent height with two small rounded serrated leaves.
On top sits one daisy head 10 to 12 final pixels across: a ring of ten to
twelve long rounded white petals radiating outward, each 1 or 2 pixels wide
with slightly shadowed white at the petal base, around a 3x3 golden yellow
center. Total plant height is about 70 percent of the canvas. Keep at least 2
final pixels of magenta on the left, right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency. The petals are warm white, not pure white, so they never blend
into the magenta.

Palette strictly limited to: #F2EFE8 and #DCD8CC for petals, #F7DA7A for the
center, #3F7A2E and #52993B for stem and leaves.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no lawn, no vase, no
multiple flowers, no text, no watermark, no gradient, no blur, no 3D render, no
anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, lawn, grass, vase, bouquet,
multiple flowers, text, watermark, blur, 3D render, perspective,
anti-aliasing, pure white background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计透明面积 45% – 65%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 70%，白瓣黄心
- [ ] 透明占比 45% – 65%，无残留洋红与紫边
- [ ] 花瓣为暖白两档（不是一片死白）
