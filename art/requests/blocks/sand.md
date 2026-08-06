# B-05 沙子

## 用途

海平面附近的岸线方块。与水直接相邻，两者的色彩对比决定了海岸线是否好看。

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
| 深阴影 | `#B09A68` |
| 阴影 | `#C0AC7C` |
| 主色 | `#D9C89A` |
| 亮部 | `#E3D5A9` |
| 高光 | `#EADDB4` |

## 视觉描述

细腻均匀的沙粒表面。**这是所有方块中颗粒最细、对比最弱的一个**。

- 颗粒直径 1–2 像素，密集且均匀
- 明暗对比要非常弱，整体接近平坦的米黄色，只有极轻微的颗粒感
- 不要画沙丘起伏、不要画贝壳、不要画脚印、不要画波纹
- 色调偏暖米黄，不能发橘也不能发灰

## AI 提示词

```
A seamless tileable pixel art texture of fine beach sand seen from directly above,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat top-down orthographic view, flat shading,
completely even illumination, no lighting direction, no shadows.

Content: very fine uniform sand grains, densely and evenly packed. Extremely subtle
value variation, almost flat. The texture should feel smooth and calm, with the
finest grain of any block in the set. Warm pale sandy beige.

Color palette strictly limited to: #B09A68, #C0AC7C, #D9C89A, #E3D5A9, #EADDB4.
Warm beige, no orange tint, no gray tint.

CRITICAL: The texture must tile seamlessly on all four edges. Left edge continues
into right edge, top edge into bottom edge.

No border, no frame, no outline, no dunes, no ripples, no wave patterns, no shells,
no footprints, no rocks, no text, no watermark, no vignette, no gradient.
```

## 负面提示词

```
dunes, ripples, waves, wave patterns, shells, footprints, rocks, pebbles, gradient,
vignette, lighting direction, drop shadow, border, frame, outline, high contrast,
orange, gray, text, watermark, signature, blur, 3D render, perspective, glossy, wet
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. Alpha 全部置为 255
4. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后看不出接缝
- [ ] 颜色数 ≤ 8，全部为暖米黄（R > G > B，且 R − B 在 40–70 之间）
- [ ] 颗粒明显比 `dirt.png` 细，对比度明显更弱
- [ ] 无沙丘、波纹等任何方向性图案
- [ ] 无任何像素的 Alpha < 255
