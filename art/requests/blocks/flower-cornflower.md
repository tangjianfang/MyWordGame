# 矢车菊（flower-cornflower）

## 用途

花草方块（十字面片渲染）。平原/田边的深蓝小花，与蒲公英的亮黄形成
蓝黄对比。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，总高约 70% |
| 透明面积 | **45% – 65%** |
| 颜色数 | 6 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 花瓣深蓝 | `#3A5AA8` |
| 花瓣主蓝 | `#4E72C4` |
| 花瓣亮蓝 | `#6E8ED8` |
| 花心黑 | `#1F1F1F` |
| 茎绿 | `#3F7A2E` |
| 叶绿 | `#52993B` |
| 键控色 | `#FF00FF`（后处理删除） |

比 `flower-orchid` 的蓝更深更饱和，两种蓝花放一起能分清。

## 视觉描述

- 单株：1 像素细茎从底边到约 45% 高度，茎上 3–4 片**线形小叶**交错
- 茎顶一个**矢车菊花头**：9–11 像素的放射多层花头——外圈 8–10 片尖瓣
  呈扇形张开（深蓝打底、主蓝为主、瓣尖亮蓝），中心 3×3 像素黑花心
  被短瓣半掩
- 总高约 70%，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single blue cornflower on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one cornflower plant centered. A thin 1-pixel stem rises from the
bottom edge to about 45 percent height with three or four narrow linear leaves
alternating along it. On top sits one cornflower head 9 to 11 final pixels
across: a radiating flower of eight to ten pointed petals fanned open in three
blue layers (deep blue base, main blue body, bright blue tips), with a 3x3
black center half hidden behind short inner petals. Total plant height is
about 70 percent of the canvas. Keep at least 2 final pixels of magenta on the
left, right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #3A5AA8, #4E72C4, #6E8ED8 for petals, #1F1F1F for
the center, #3F7A2E and #52993B for stem and leaves. Deep true blue, never
purple, never magenta tint.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no wheat field, no
vase, no multiple flowers, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, field, wheat, vase,
bouquet, multiple flowers, purple, violet, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 6 色
3. 统计透明面积 45% – 65%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 70%，放射状深蓝花头黑心
- [ ] 与 `flower-orchid.png` 并排，本种更深更饱和
- [ ] 透明占比 45% – 65%，无残留洋红与紫边
