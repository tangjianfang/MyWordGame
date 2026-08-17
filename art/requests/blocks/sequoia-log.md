# 红杉原木（sequoia-log）

## 用途

红杉（`sequoia`，高山巨木、树干 8–11 格）的树皮侧面贴图，四边无缝。
红杉皮是**锈红棕的厚板块状**——七种树皮里最红、最厚实的一种。

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
| 裂沟底（最深） | `#4A2318` |
| 沟壁阴影 | `#5E2F1E` |
| 皮面主色 | `#723D26` |
| 板块亮部 | `#864C30` |
| 板块高光 | `#9A5C3A` |

锈红棕五档，全部 B < G 的暖锈色。与 `pine-log` 相比更红更亮一档
（松偏深棕、杉偏锈红）。

## 视觉描述

- **厚板块状树皮**：纵向的宽厚皮板（4–6 像素宽），板与板之间是
  1–2 像素的深裂沟 `#4A2318`
- 板面主色 `#723D26`，受光侧亮部 `#864C30`，板棱 1 像素高光 `#9A5C3A`
- 皮板边缘略不规则（有 1 像素的进退），避免「红砖墙」感
- 裂沟在上下边缘连续
- 无木节、无树脂、无火烧痕

## AI 提示词

```
A seamless tileable pixel art texture of giant sequoia redwood bark, flat
front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: thick chunky vertical bark plates, 4 to 6 final pixels wide,
separated by 1 or 2 pixel deep dark fissures running from top to bottom. Plate
faces are the main rust red-brown, lit sides slightly lighter, and each plate
edge carries a 1-pixel highlight. Plate edges are slightly irregular, jogging
in and out by 1 pixel, so it never looks like a brick wall. The fissures must
run straight through the top and bottom edges so they connect when tiled.

Color palette strictly limited to: #4A2318, #5E2F1E, #723D26, #864C30,
#9A5C3A. Rusty red-brown, redder and richer than pine bark, never orange,
never bricks.

CRITICAL: The texture must tile seamlessly on all four edges. Fissures
continue across the top and bottom edges; grain continues across the left and
right edges.

No border, no frame, no outline, no knots, no resin, no fire scars, no
charcoal, no leaves, no branches, no text, no watermark, no vignette, no drop
shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
knots, resin, sap, fire scars, charcoal, soot, leaves, branches, brick wall,
brick pattern, mortar, orange, text, watermark, blur, 3D render, perspective,
photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查裂沟上下边缘连续
4. 四边偏移半幅自检接缝
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 锈红棕厚皮板 + 深裂沟，与 `pine-log.png` 并排明显更红
- [ ] 不读作砖墙（板缘不规则、无横缝）
- [ ] 颜色数 ≤ 5，上下偏移半幅沟接沟
