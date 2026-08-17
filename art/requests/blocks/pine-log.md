# 松树原木（pine-log）

## 用途

松树（`pine`）的树皮侧面贴图，四边无缝。松皮是**深红棕的粗纵裂**，
比通用 `log-side` 更红、裂纹更深，雪原里一眼可辨。

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
| 裂沟底（最深） | `#33221A` |
| 沟壁阴影 | `#45301F` |
| 皮面主色 | `#573D28` |
| 皮脊亮部 | `#694A31` |
| 皮脊高光 | `#7B5840` |

整体比 `log-side`（`#3B2C1C`–`#8E7350`）更红更暗，且对比更强——
松皮的沟要「切得进去」。

## 视觉描述

- **纵向沟脊结构**：3–4 条纵向深沟 `#33221A` 贯穿上下，沟宽 1–2 像素
- 沟与沟之间是隆起皮脊：主色 `#573D28`，脊面亮部 `#694A31`、
  脊顶 1 像素高光 `#7B5840`
- 沟脊宽度与间距不完全均匀（1–3 像素随机），避免「条纹布」感
- 沟必须在上下边缘连续（竖向平铺时沟接沟）
- 无木节、无松脂滴、无苔藓

## AI 提示词

```
A seamless tileable pixel art texture of pine tree bark, flat front view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: coarse pine bark with 3 or 4 vertical furrows running from top to
bottom, each 1 or 2 final pixels wide, separated by raised ridges. The furrow
bottoms are the darkest color, ridge faces the main reddish brown, ridge tops
carry a 1-pixel highlight. Furrow width and spacing vary slightly between 1
and 3 pixels so it never looks like striped fabric. The furrows must run
straight through the top and bottom edges so they connect when tiled.

Color palette strictly limited to: #33221A, #45301F, #573D28, #694A31,
#7B5840. Deep reddish brown, darker and redder than generic oak bark.

CRITICAL: The texture must tile seamlessly on all four edges. Furrows continue
across the top and bottom edges; grain continues across the left and right
edges.

No border, no frame, no outline, no knots, no resin drops, no sap, no moss, no
snow, no leaves, no branches, no text, no watermark, no vignette, no drop
shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
knots, resin, sap, moss, snow, leaves, branches, horizontal cracks, striped
fabric, text, watermark, blur, 3D render, perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查纵沟在上下边缘连续（不连续则手动对齐）
4. 四边偏移半幅自检接缝
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 纵向沟脊清晰，比 `log-side` 更红更暗
- [ ] 沟脊宽窄有随机变化，不是等宽条纹
- [ ] 上下偏移半幅后沟接沟
- [ ] 颜色数 ≤ 5
