# 木门下半（wooden-door-lower）

## 用途

`wooden-door` 方块下半部分的正面贴图。整扇门的把手画在这一半：一块小铁片
横搭在门缝上。本贴图**不含洋红**，完全不透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单面专用贴图） |
| Alpha | 无，完全不透明 |
| 颜色数 | 7 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 门框/底梁（最深） | `#4E3826` |
| 板缝阴影木 | `#6B4E2E` |
| 门板主色 | `#8A6741` |
| 门板亮部 | `#9C7549` |
| 门板高光 | `#B98D57` |
| 铁把手暗部 | `#5C5C5C` |
| 铁把手高光 | `#8A8A8A` |

木色与 `wooden-door-upper` 同一套；铁把手用 `stone` 的石灰系，与箱子搭扣一致。

## 视觉描述

- 四周 2 像素门框 `#4E3826`，与上半的门框同宽
- 内部竖向拼 2 条门板，中间 1 像素竖缝 `#6B4E2E`，**竖缝位置与上半严格对齐**
- 中上部（第 6–9 行）一枚小铁把手：跨在竖缝上的 4×3 横片，主体 `#5C5C5C`、
  上缘 1 像素高光 `#8A8A8A`
- 底部 2 行横梁 `#4E3826`，横梁上方 1 行 `#6B4E2E` 阴影
- 门板有 1–2 道 1 像素竖向木纹，无木节

## AI 提示词

```
A pixel art texture of the lower half of a wooden door, flat front view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a door panel made of 2 vertical wooden boards inside a 2-pixel dark
wooden frame around all four sides. A small horizontal iron handle plate,
about 4x3 final pixels, sits across the center seam in the upper part of the
panel, with a 1-pixel light gray highlight on its top edge. The bottom 2 rows
are a dark horizontal rail with a 1-pixel shadow line above it. Subtle vertical
wood grain, no knots, no windows.

Wood palette strictly limited to: #4E3826, #6B4E2E, #8A6741, #9C7549, #B98D57.
Iron handle only #5C5C5C and #8A8A8A.

This texture does not tile. Fill the whole square canvas edge to edge with
opaque pixels only, no background, no magenta anywhere.

No outer frame around the canvas, no windows, no hinges, no keyhole, no carved
pattern, no text, no watermark, no vignette, no drop shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, drop shadow, windows, hinges, keyhole, carved pattern,
border frame, outline, text, watermark, blur, 3D render, perspective,
photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 7 个色值
3. 核对竖缝位置与 `wooden-door-upper` 对齐（都在中线）
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 铁把手跨在中央竖缝上，读得出「门把手」
- [ ] 底部横梁完整、无窗
- [ ] 颜色数 ≤ 7
- [ ] 与 `wooden-door-upper.png` 竖向拼合，门框与竖缝对齐，读作同一扇门
