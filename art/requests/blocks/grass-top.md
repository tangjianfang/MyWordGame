# B-03 草方块顶面

## 用途

地表最上层，玩家俯视时看到的主要画面。大面积连续出现，是整个游戏的观感基调。

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
| 深阴影 | `#3C6626` |
| 阴影 | `#4A7E2F` |
| 主色 | `#5D9C3C` |
| 亮部 | `#69AB44` |
| 高光 | `#74B84E` |

## 视觉描述

俯视视角的草地表面，由**短促的草叶颗粒**组成。

- 颗粒呈细小的不规则斑点，直径 1–3 像素，模拟俯视时的草尖
- 明暗交错但整体均匀，不能有明显更亮或更暗的区域
- 不要画单根可辨认的草叶、不要画花朵、不要画露珠
- 绿色要偏自然的黄绿，不能是荧光绿或墨绿

## AI 提示词

```
A seamless tileable pixel art texture of green grass seen from directly above,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, top-down orthographic view,
completely even illumination with no lighting direction and no shadows.

Content: dense short grass viewed from above, made of tiny irregular speckles
suggesting grass tips. Natural yellow-green tone. Speckles are evenly scattered
with subtle light and dark variation, creating a calm organic texture.

Color palette strictly limited to: #3C6626, #4A7E2F, #5D9C3C, #69AB44, #74B84E.
Natural yellow-green, not neon green, not dark forest green.

CRITICAL: The texture must tile seamlessly on all four edges. Left edge continues
into right edge, top edge into bottom edge.

No border, no frame, no outline, no individual identifiable grass blades, no flowers,
no dew drops, no dirt patches, no paths, no text, no watermark, no vignette,
no gradient across the image.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
flowers, dew, water drops, dirt patches, paths, stones, neon green, dark green,
text, watermark, signature, blur, 3D render, perspective, glossy, recognizable shapes
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值（可保留至多 2 个中间色阶）
3. Alpha 全部置为 255
4. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后看不出接缝
- [ ] 颜色数 ≤ 10，全部为绿色系（G 通道最大）
- [ ] 在 8×8 平铺预览中看不出规律性重复
- [ ] 无任何具象物体（花、露珠、石子）
- [ ] 边缘颜色与 `grass-side.png` 顶部草带色调一致
