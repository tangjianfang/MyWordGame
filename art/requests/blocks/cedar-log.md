# 雪松原木（cedar-log）

## 用途

雪松（`cedar`）的树皮侧面贴图，四边无缝。雪松皮是**暖灰棕的纤维状
剥落纹理**——比松皮浅、比白桦皮暗，细碎而不规整。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 裂缝底（最深） | `#463E36` |
| 纤维阴影 | `#585048` |
| 皮面主色 | `#6A6258` |
| 皮面亮部 | `#7C746A` |
| 剥落高光 | `#8E867C` |

暖灰棕五档，全部 B < G（不发紫的暖灰）——灰调若偏冷会在洋红流水线的
去边判据下出问题，也容易与石头混。

## 视觉描述

- 基底是细密的**纵向纤维纹**：1 像素宽的竖细线交错（`#585048` 暗线与
  `#7C746A` 亮线相间），线长短不一
- 散布 **4–6 处剥落小片**：2–4 像素的方形/菱形碎片区，片内 `#8E867C`
  亮色、片缘 1 像素 `#463E36` 深缝
- 剥落片离四边 ≥2 像素
- 无木节、无树脂、无苔藓

## AI 提示词

```
A seamless tileable pixel art texture of cedar tree bark, flat front view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: a base of fine vertical fibrous grain — many 1-pixel vertical lines
of varying length, dark and light alternating — with four to six small peeling
patches scattered over it. Each patch is a 2 to 4 final pixel chip of light
bark with a 1-pixel dark seam around its edge, like thin bark flakes lifting
off. Patches stay at least 2 final pixels away from all four edges and never
line up in a row. No knots, no resin, no moss.

Color palette strictly limited to: #463E36, #585048, #6A6258, #7C746A,
#8E867C. Warm grayish brown, lighter than pine bark, never cold gray, never
purple tint.

CRITICAL: The texture must tile seamlessly on all four edges. Fibers continue
across the top and bottom edges; grain continues across the left and right
edges.

No border, no frame, no outline, no knots, no resin, no moss, no snow, no
leaves, no branches, no text, no watermark, no vignette, no drop shadow, no
gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
knots, resin, sap, moss, snow, leaves, branches, cold gray, purple tint,
stone, cobblestone, text, watermark, blur, 3D render, perspective,
photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 四边偏移半幅自检接缝
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 纤维竖纹 + 4–6 处剥落小片，读作「雪松皮」
- [ ] 暖灰棕不发冷、不与 `stone` 混淆
- [ ] 颜色数 ≤ 5，偏移半幅无接缝
