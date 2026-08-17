# 附魔台侧面（enchanting-table-side）

## 用途

附魔台方块（milestone-11 第 2 波注册 `enchanting_table` 方块）的**侧面与底面**
共用贴图。与 `enchanting-table-top` 组成同一附魔台：顶部深紫黑台沿（与顶面同
一套三阶色）+ 金色符文点 + 下部木身（色值逐像素对齐 `planks.png`）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 左右无缝（顶/底边接不同面，照 grass-side 先例） |
| Alpha | 无，完全不透明 |
| 颜色数 | ≤ 10 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 台沿暗部 | `#1F1430` |
| 台沿主色 | `#32224D` |
| 台沿亮部 | `#4A3568` |
| 符文金 | `#DCAE3A` |
| 木板暗 | `#8A6741`（planks 阴影同值） |
| 木板主 | `#9C7549`（planks 主色同值） |
| 木板亮 | `#B98D57`（planks 高光同值） |

## 视觉描述

- 顶部 9 行深紫黑台沿（与 `enchanting-table-top` 同套三阶噪点）
- 第 10 行撒金色符文点：间隔 3-5 像素的断续点，不连成实线
- 第 11 行以下整片木纹：竖向板条 + 1 像素板缝，色值不出木板黄系，
  与 `planks.png` 并排读作「同一批木板」
- 条带整行恒定 → 左右边缘天然衔接

## AI 提示词

```
A pixel art texture of the side face of an enchanting table block, flat
front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, crisp pixel edges.

Content: a horizontal dark purple-black stone band covers the top 30 percent
of the square, with sparse small gold rune dots scattered in a broken line
along its bottom edge, gaps of 3 to 5 pixels between dots, never forming a
solid line. The remaining lower 70 percent is a plain warm wood plank
texture with simple vertical plank seams and subtle wood grain.

Color palette strictly limited to #1F1430, #32224D, #4A3568 for the dark
stone band, #DCAE3A for the gold rune dots, and #8A6741, #9C7549, #B98D57
for the wood planks.

The horizontal band runs the full width so the left and right edges match
perfectly when tiled. Fill the whole square canvas edge to edge, no
background, no magenta.

No text, no watermark, no vignette, no drop shadow, no gradient, no glow,
no books, no candles, no skulls.
```

## 负面提示词

```
gradient, vignette, glow, drop shadow, text, watermark, blur, photorealistic,
3D perspective, books, candles, skulls, animation frames
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 7 色（木身对照 `planks.png` 色值）
3. 左右无缝自检（水平位移半宽看接缝）
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 台沿高度约 9-10 行、符文点断续不连成线
- [ ] 木身与 `planks.png` 并排，木色完全一致
- [ ] 左右边缘位移自检无缝
- [ ] ≤ 7 色，无调色板外杂色（尤其不许出现 `#FF00FF`）
