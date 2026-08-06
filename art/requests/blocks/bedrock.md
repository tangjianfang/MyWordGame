# B-07 基岩

## 用途

世界最底层（Y = −64）的不可破坏方块，作为世界的物理边界。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 – 8 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 最深 | `#242424` |
| 深阴影 | `#333333` |
| 主色 | `#4A4A4A` |
| 亮部 | `#545454` |
| 高光 | `#5E5E5E` |

## 视觉描述

坚硬致密的深色岩石，**对比度是所有方块中最强的**，用视觉重量传达"这里挖不动"。

- 由大块的不规则深浅色块构成，色块直径 4–8 像素，明显比石头的颗粒大
- 深浅交错强烈，形成粗粝斑驳的质感
- 不要画裂缝、不要画尖刺、不要画任何危险符号
- 保持中性灰，不要偏色

## AI 提示词

```
A seamless tileable pixel art texture of dark indestructible bedrock,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat top-down orthographic view, flat shading,
even illumination, no lighting direction.

Content: hard dense dark stone made of large irregular blotches of contrasting dark
grays. The blotches are noticeably chunkier than ordinary stone grain, creating a
rugged mottled appearance. High contrast between the darkest and lightest patches,
conveying weight and impenetrability.

Color palette strictly limited to: #242424, #333333, #4A4A4A, #545454, #5E5E5E.
Neutral dark gray only, no blue tint, no brown tint.

CRITICAL: The texture must tile seamlessly on all four edges. Left edge continues
into right edge, top edge into bottom edge.

No border, no frame, no outline, no cracks, no spikes, no hazard symbols, no lava,
no glow, no text, no watermark, no vignette, no gradient.
```

## 负面提示词

```
cracks, spikes, hazard symbols, warning signs, lava, glow, emissive, gradient,
vignette, lighting direction, drop shadow, border, frame, outline, blue tint,
brown tint, text, watermark, signature, blur, 3D render, perspective, glossy
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. Alpha 全部置为 255
4. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后看不出接缝
- [ ] 颜色数 ≤ 8，全部为中性灰（R、G、B 三通道差值 ≤ 4）
- [ ] 色块明显比 `stone.png` 更大更粗粝，对比度明显更强
- [ ] 与 `stone.png` 并排时一眼能分辨出"这个更硬更沉"
- [ ] 无任何像素的 Alpha < 255
