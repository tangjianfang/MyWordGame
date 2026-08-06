# B-02 泥土

## 用途

草方块下方的过渡层，以及挖开草地后暴露的表面。同时是 `grass-side` 下部区域的参照，
两者纹理风格必须完全一致。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 6 – 10 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 深阴影 | `#4E3826` |
| 阴影 | `#5F4630` |
| 主色 | `#7A5A3C` |
| 亮部 | `#876643` |
| 高光 | `#91704B` |

## 视觉描述

松散的泥土表面，比石头**颗粒更粗、更松散**，带少量细小的深色土块。

- 颗粒直径 2–4 像素，比石头明显粗一档
- 允许出现少量（3–6 处）稍深的小土块，直径不超过 4 像素，随机分布
- 不要画石子、不要画草根、不要画蚯蚓等任何具象物体
- 整体偏暖棕色，不能发红也不能发灰

## AI 提示词

```
A seamless tileable pixel art texture of loose brown dirt soil, top-down flat view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, completely
even illumination with no lighting direction.

Content: coarse loose soil made of chunky irregular grains, slightly rougher and
more granular than stone. A few scattered darker soil clumps. Warm earthy brown
tone, evenly distributed across the whole image.

Color palette strictly limited to: #4E3826, #5F4630, #7A5A3C, #876643, #91704B.
Warm brown only, no red tint, no gray tint.

CRITICAL: The texture must tile seamlessly on all four edges. Left edge continues
into right edge, top edge into bottom edge.

No border, no frame, no outline, no pebbles, no rocks, no grass, no roots, no worms,
no plants, no text, no watermark, no vignette, no gradient across the image.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
pebbles, stones, grass, roots, plants, worms, insects, text, watermark, signature,
blur, 3D render, perspective, glossy, wet, mud puddle, recognizable shapes
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值（可保留至多 2 个中间色阶）
3. Alpha 全部置为 255
4. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后看不出接缝
- [ ] 颜色数 ≤ 10，全部落在棕色系（R > G > B）
- [ ] 颗粒明显比 `stone.png` 粗，两者并排能看出材质区别
- [ ] 无任何具象物体
- [ ] 无任何像素的 Alpha < 255
