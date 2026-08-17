# ARCH-09 商店木牌方块

## 用途

村庄**商店/交易点**门旁挂的木牌方块贴图。刻三枚摞起来的金币表示「这里能交易」，
与村民交易 UI 呼应。与 `sign-village` 同底板不同刻纹，成对生产、成对验收。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无 |
| 颜色数 | 6 – 8 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 木板主色 | `#9C7549` |
| 木板暗 | `#8A6741` |
| 木板亮 | `#B98D57` |
| 板缝 | `#6B4F30` |
| 金币主色 | `#DCAE3A` |
| 金币高光 | `#F7DA7A` |
| 金币描边 | `#241A11` |

## 视觉描述

与 `sign-village` 同款横纹木牌，中央刻纹换成**三枚摞起的金币**：

- 下两上一摞成小金字塔，每枚是带 1 像素深描边的圆片，左上带高光点
- 金币用「嵌进木头」的画法：描边是刻痕深色，金币面略凸出于木板
- 板缝贯通到边、刻纹不碰边——同 ARCH-08 的两条铁律
- 不画钱袋绳子、不画文字

## AI 提示词

```
A seamless tileable pixel art block texture of a carved wooden shop sign,
flat front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a horizontal wooden plank board made of four plank rows, warm
brown with subtle grain speckles and thin darker seams between planks. In
the center, one pictogram of three gold coins stacked in a small pyramid,
two on the bottom and one on top. Each coin is a round disc with a dark
engraved outline and one tiny bright highlight pixel on its upper left,
drawn as inlaid gold slightly raised from the wood. The pictogram appears
exactly once per tile, fully inside the tile, never touching the edges.
Plank seams run edge to edge so the texture tiles.

Palette strictly: planks #9C7549 #8A6741 #B98D57, seams #6B4F30, coins
#DCAE3A #F7DA7A, coin outline #241A11.

CRITICAL: tiles seamlessly on all four edges, plank grain and seams line
up across tile borders.

No border, no frame, no text, no letters, no money bag, no nails, no
watermark, no gradient, no glow, no 3D render, no anti-aliasing.
```

## 负面提示词

```
text, letters, numbers, money bag, dollar sign, nails, border, frame,
watermark, gradient, glow, 3D render, anti-aliasing, perspective
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 7 个色值量化
3. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝
- [ ] 金币每格恰好一组、居中、不碰边
- [ ] 金币色与 items 表金锭/金币图标同系（`#DCAE3A` 主色）
- [ ] 与 `sign-village.png` 并排：底板完全同款，只有刻纹不同
- [ ] 图中不含任何文字
