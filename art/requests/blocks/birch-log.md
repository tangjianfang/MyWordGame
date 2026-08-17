# 白桦原木（birch-log）

## 用途

白桦树（`birch`）的树皮侧面贴图，四边无缝（树干由多个原木方块竖叠）。
白桦的识别特征是**白皮 + 黑色横向皮孔**，与 `birch-leaves` 的亮黄绿叶
配成一套。

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
| 皮孔黑（最深） | `#2E2C26` |
| 皮面阴影 | `#B8B2A4` |
| 皮面中间调 | `#C9C4B8` |
| 白皮主色 | `#D7CFC0` |
| 白皮高光 | `#E4E0D6` |

`#D7CFC0` 白皮是任务卡指定的白桦主色——比雪 `#F2F2F2` 更灰更暖，
放进雪原也不会糊成一片。

## 视觉描述

- 底色大面白皮 `#D7CFC0`，上下有 1–2 像素的浅灰横向纹理起伏
  （`#B8B2A4` / `#C9C4B8` 交错的水平细带）
- 散布 **5–7 个黑色横向皮孔**：宽 3–6 像素、高 1–2 像素的水平短划
  `#2E2C26`，位置错开、长短不一
- 皮孔离四边 ≥2 像素（避免平铺时皮孔被切断成半截）
- 高光 `#E4E0D6` 零星点缀在皮孔上方
- 无木节、无苔藓、无纵向深裂纹

## AI 提示词

```
A seamless tileable pixel art texture of pale birch tree bark, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: a base of pale off-white birch bark #D7CFC0 with subtle 1 or 2 pixel
horizontal bands of light gray #B8B2A4 and #C9C4B8 rippling across it.
Scattered over it are five to seven black horizontal lenticel dashes #2E2C26,
each 3 to 6 final pixels wide and 1 or 2 pixels tall, staggered and of varying
length, each kept at least 2 final pixels away from all four edges so they are
never cut. A few single-pixel highlights #E4E0D6 sit just above some dashes.
No knots, no moss, no deep cracks.

Color palette strictly limited to: #2E2C26, #B8B2A4, #C9C4B8, #D7CFC0,
#E4E0D6. Pale warm white bark, not pure white, not gray stone.

CRITICAL: The texture must tile seamlessly on all four edges. The horizontal
bands continue across the left and right edges; bark tone matches at the top
and bottom edges.

No border, no frame, no outline, no knots, no moss, no lichen, no leaves, no
branches, no snow, no text, no watermark, no vignette, no drop shadow, no
gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
knots, moss, lichen, leaves, branches, snow, vertical cracks, pure white,
text, watermark, blur, 3D render, perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查皮孔无一半落在边缘上（有则手动挪进画面）
4. 四边偏移半幅自检接缝
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 白皮 + 5–7 个黑色横向皮孔，一眼读出「白桦」
- [ ] 皮孔无被边缘切断的半截
- [ ] 颜色数 ≤ 5
- [ ] 偏移半幅后看不出接缝
- [ ] 与 `birch-leaves.png` 并排，白干亮叶成套
