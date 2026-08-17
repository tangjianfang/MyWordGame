# 樱花原木（cherry-log）

## 用途

樱花树（`cherry`，平原粉叶树）的树皮侧面贴图，四边无缝。樱皮是
**暖深灰 + 横向皮孔**——与白桦同属「横皮孔」识别型，但底色是深灰棕，
配粉白樱叶对比强烈。

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
| 皮孔深色（最深） | `#2E2824` |
| 皮面阴影 | `#4A423C` |
| 皮面主色 | `#5A5048` |
| 皮面亮部 | `#6A5E54` |
| 皮面高光 | `#7A6E62` |

暖深灰五档——刻意做成 B < G 的**暖灰**（不发紫的烟灰），冷紫灰会
在去洋红边流水线上被误伤，也会与石头混。

## 视觉描述

- 基底深灰棕 `#5A5048`，带 1 像素细密的横向微纹（`#4A423C` 暗与
  `#6A5E54` 亮交替）
- 散布 **5–7 个横向皮孔**：宽 2–4 像素、高 1 像素的水平短划
  `#2E2824`，比白桦的皮孔更短更碎
- 皮孔离四边 ≥2 像素，位置错开
- 高光 `#7A6E62` 零星 1 像素点
- 无木节、无苔藓、无树脂、无花瓣

## AI 提示词

```
A seamless tileable pixel art texture of dark cherry tree bark, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: a base of warm dark grayish brown bark #5A5048 with subtle 1-pixel
horizontal micro-grain, dark and light lines alternating. Scattered over it
are five to seven short horizontal lenticel dashes #2E2824, each 2 to 4 final
pixels wide and 1 pixel tall, shorter and scruffier than birch lenticels,
staggered and each kept at least 2 final pixels away from all four edges.
A few single-pixel warm highlights sit near some dashes. No knots, no moss, no
resin, no petals.

Color palette strictly limited to: #2E2824, #4A423C, #5A5048, #6A5E54,
#7A6E62. Warm dark smoke gray with a brown lean, never cold purple gray, never
blue gray, never stone.

CRITICAL: The texture must tile seamlessly on all four edges. The horizontal
micro-grain continues across the left and right edges; tone matches at the top
and bottom edges.

No border, no frame, no outline, no knots, no moss, no resin, no petals, no
flowers, no leaves, no branches, no text, no watermark, no vignette, no drop
shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
knots, moss, resin, petals, flowers, leaves, branches, cold purple gray, blue
gray, stone, cobblestone, white bark, text, watermark, blur, 3D render,
perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查皮孔无被边缘切断
4. 四边偏移半幅自检接缝
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 暖深灰底 + 短碎横皮孔，与 `birch-log.png` 并排明显更深
- [ ] 灰为暖灰（不存在 R > G 且 B > G 的像素）
- [ ] 颜色数 ≤ 5，偏移半幅无接缝
- [ ] 与 `cherry-leaves.png` 并排，深灰干 + 粉白叶对比清晰
