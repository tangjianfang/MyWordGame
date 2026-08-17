# SEASON-06 冬日雪草方块（顶面）

## 用途

冬季变体的**草地顶面**贴图：雪毯盖在草地上、草叶钻出雪面。
结构与 `blocks/grass-top.md` 同构（颗粒噪点风格），冬天活动期间替换草方块顶面。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无 |
| 颜色数 | 5 – 6 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 雪亮 | `#F6FAFC` |
| 雪主 | `#E8F0F4` |
| 雪影 | `#D0DEE8` |
| 雪坑影点 | `#B8C8D8` |
| 草绿 | `#4A7E2F` `#5D9C3C` |

雪色三档与 `menu-snow-aurora` / `panorama-snow` 的雪一致。

## 视觉描述

俯视的一层新雪盖在草地上：

- 白与淡蓝的雪铺满画面，带圆钝的缓雪丘起伏
- 约 1/4 面积露出**钻出雪面的草叶**：短竖笔 2-3 像素一小簇，散布均匀
- 点缀少量灰蓝雪坑影点做深度
- 雪丘与草簇跨格连续——无缝关键
- 不画脚印、花、冰面（冰是独立方块）

## AI 提示词

```
A seamless tileable pixel art block texture of a snow-covered grass
top, flat top-down view, 1024x1024, designed to be downscaled to 32x32
pixel art.

Style: retro voxel game block texture, flat shading, no perspective,
even illumination. This is the top face texture of a grass block in
winter.

Content: a blanket of fresh snow in white and pale blue tones filling
almost everything, with gentle rounded drift shapes. Through the snow,
about one quarter of the area, small clusters of dark green grass
blades poke out as short vertical strokes of two or three pixels,
scattered evenly, plus a few gray-blue snow shadow speckles for depth.
The snow and grass continue across the tile borders.

Palette strictly: snow #F6FAFC #E8F0F4 #D0DEE8, grass #4A7E2F #5D9C3C,
shadow speckles #B8C8D8.

CRITICAL: tiles seamlessly on all four edges.

No border, no frame, no footprints, no flowers, no ice sheet, no text,
no letters, no watermark, no gradient, no glow, no 3D render, no
anti-aliasing, no dark background, no blue ice.
```

## 负面提示词

```
footprints, flowers, ice, border, frame, text, letters, watermark,
gradient, glow, 3D render, anti-aliasing, dark background, perspective
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 6 个色值量化
3. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝
- [ ] 草叶露出面积约 1/4，散布均匀
- [ ] 雪色与雪原全景/极光菜单的雪同系
- [ ] 颗粒大小与 `grass-top.png` 一致（1-2 像素级噪点）
