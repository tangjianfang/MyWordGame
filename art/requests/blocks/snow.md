# B-08 雪

## 用途

雪原 biome（F2 follow-up，spec C4）的地表方块。覆盖温度 < 0.15 的极寒区，
大面积连续出现，是与草地/沙地区分度最强的群系标识色。

> 当前 `Assets/StreamingAssets/blocks/textures/snow.png` 是**程序生成的 5 色占位图**
> （确定性哈希铺颗粒，仅为让材质库测试通过）。正式美术入库后按下面规格替换。

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
| 阴影 | `#DEE5ED` |
| 亮阴影 | `#E6ECF2` |
| 主色 | `#F0F4F8` |
| 亮部 | `#EAEFF5` |
| 高光 | `#F6F9FB` |

冷白微蓝，颗粒感与沙子同级（细、弱对比）。不能发灰也不能发紫。

## 视觉描述

- 细腻均匀的雪粒表面，颗粒直径 1–2 像素，密集均匀
- 明暗对比非常弱，整体接近平坦的冷白
- 不要画积雪起伏、不要画冰晶闪光、不要画脚印
- 色调冷白偏一点点蓝，不能发灰、不能发紫

## AI 提示词

```
A seamless tileable pixel art texture of fresh snow surface seen from directly above,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat top-down orthographic view, flat shading,
completely even illumination, no lighting direction, no shadows.

Content: very fine uniform snow grains, densely and evenly packed. Extremely subtle
value variation, almost flat. The texture should feel smooth, cold and calm. Cool
white with the faintest blue tint.

Color palette strictly limited to: #DEE5ED, #E6ECF2, #F0F4F8, #EAEFF5, #F6F9FB.
Cool white, no gray tint, no purple tint.

CRITICAL: The texture must tile seamlessly on all four edges. Left edge continues
into right edge, top edge into bottom edge.

No border, no frame, no outline, no drifts, no footprints, no ice crystals, no
sparkles, no text, no watermark, no vignette, no gradient.
```

## 负面提示词

```
drifts, dunes, footprints, ice crystals, sparkles, glitter, gradient, vignette,
lighting direction, drop shadow, border, frame, outline, high contrast, gray,
purple, blue tint too strong, text, watermark, signature, blur, 3D render,
perspective, glossy, wet
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. Alpha 全部置为 255
4. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后看不出接缝
- [ ] 颜色数 ≤ 8，全部为冷白（B ≥ R ≥ G − 2，且 B − G 在 4–14 之间）
- [ ] 与 `sand.png` 并排看，两者颗粒细度同级、色温明显相反
- [ ] 无积雪起伏、冰晶等任何方向性图案
- [ ] 无任何像素的 Alpha < 255
