# B-06 水

## 用途

海洋与湖泊。**唯一需要半透明的方块**，走的是独立的透明材质通道。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | **需要**，统一 190（约 75% 不透明） |
| 颜色数 | 4 – 6 色 |

### 关于透明度

RGB 与 Alpha **分开处理**：先按不透明生成彩色纹理，后处理时把整张图的 Alpha
统一设为 190。不要让 AI 生成带透明度的图，也不要出现局部透明度差异。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 深阴影 | `#24497B` |
| 阴影 | `#2C5893` |
| 主色 | `#3A6FB5` |
| 高光 | `#4E88CE` |

## 视觉描述

平静水面的俯视图，带**极其柔和的明暗流动感**。

- 明暗区域呈大块的、边缘柔和的不规则形状，不是细颗粒
- 对比度要低，整体是一片沉静的蓝，只有隐约的深浅变化
- 不要画白色浪花、不要画高光反射点、不要画涟漪圆环
- 不要画任何水下的东西

## AI 提示词

```
A seamless tileable pixel art texture of calm water surface seen from directly above,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat top-down orthographic view, flat shading,
even illumination, no lighting direction, no specular highlights.

Content: still deep blue water with very soft broad variations in tone, suggesting
gentle depth changes rather than surface detail. The shapes are large, soft-edged
and irregular. Very low contrast, calm and uniform overall.

Color palette strictly limited to: #24497B, #2C5893, #3A6FB5, #4E88CE.
Pure blue, no cyan, no teal, no green tint.

Render it fully opaque. Transparency will be applied later in post-processing.

CRITICAL: The texture must tile seamlessly on all four edges. Left edge continues
into right edge, top edge into bottom edge.

No border, no frame, no outline, no white foam, no waves, no ripple rings, no
specular highlights, no reflections, no fish, no bubbles, no underwater objects,
no text, no watermark, no vignette.
```

## 负面提示词

```
foam, white caps, waves, ripples, ripple rings, specular highlight, reflection,
sun glare, fish, bubbles, seaweed, underwater objects, transparency, alpha,
cyan, teal, green, gradient, vignette, border, frame, outline, text, watermark,
blur, 3D render, perspective, glossy
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 4 个色值（可保留至多 2 个中间色阶）
3. **将整张图的 Alpha 通道统一设为 190**，不得有局部差异
4. 存为带 Alpha 的 PNG-32
5. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后看不出接缝
- [ ] 颜色数 ≤ 6，全部为蓝色系（B 通道最大，且 G − B < −30）
- [ ] **所有像素的 Alpha 恰为 190**，无一例外
- [ ] 无白色浪花、无高光点
- [ ] 与 `sand.png` 并排时冷暖对比明确，海岸线清晰好看
