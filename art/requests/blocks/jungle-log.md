# 丛林原木（jungle-log）

## 用途

丛林树（`jungle`）的树皮侧面贴图，四边无缝。丛林皮是**深暖棕 +
沟里积苔**——热带湿林的粗壮树干，颜色最深的一档树皮。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 6 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 裂沟底（最深） | `#2E2418` |
| 沟壁阴影 | `#40301F` |
| 皮面主色 | `#523D28` |
| 皮脊亮部 | `#644B31` |
| 皮脊高光 | `#76593B` |
| 沟内苔藓 | `#4E6B2E` |

五档深暖棕 + 一档苔绿。苔绿即 `leaves` 的深叶绿系，只用点在沟底
（湿度感），不上皮脊。

## 视觉描述

- **宽纵沟结构**：2–3 条纵沟（宽 2 像素）贯穿上下，沟底 `#2E2418`
- 沟底散布**苔藓绿点**：1–2 像素的 `#4E6B2E` 小块，只在沟内，
  总面积 ≤ 全图 8%
- 沟间皮脊宽（3–5 像素）：主色 `#523D28`，亮部 `#644B31`，
  脊顶高光 `#76593B`
- 沟在上下边缘连续
- 无木节、无藤蔓、无气生根

## AI 提示词

```
A seamless tileable pixel art texture of dark jungle tree bark, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: thick jungle bark with 2 or 3 wide vertical furrows, each about 2
final pixels wide, running from top to bottom. Furrow bottoms are almost black
and carry small 1 or 2 pixel moss-green patches here and there, only inside
the furrows, never on the ridges — moss covers less than 8 percent of the
whole image. Between furrows the ridges are broad, 3 to 5 pixels wide, with a
1-pixel highlight on top. The furrows must run straight through the top and
bottom edges so they connect when tiled.

Color palette strictly limited to: #2E2418, #40301F, #523D28, #644B31,
#76593B for bark, #4E6B2E for moss in the furrows. Deep warm brown, the
darkest bark in the set.

CRITICAL: The texture must tile seamlessly on all four edges. Furrows continue
across the top and bottom edges; grain continues across the left and right
edges.

No border, no frame, no outline, no knots, no vines, no hanging roots, no
leaves, no flowers, no text, no watermark, no vignette, no drop shadow, no
gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
knots, vines, lianas, hanging roots, leaves, flowers, orchid, excessive moss,
green bark, text, watermark, blur, 3D render, perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 6 个色值
3. 检查苔藓只落在沟内、占比 ≤ 8%（超了手动删减）
4. 四边偏移半幅自检接缝
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 深棕宽沟 + 沟内少量苔绿，读作「湿热带树干」
- [ ] 苔绿占比 ≤ 8%，皮脊上无苔
- [ ] 颜色数 ≤ 6，上下偏移半幅沟接沟
- [ ] 是本批 6 种树皮里最暗的一种
