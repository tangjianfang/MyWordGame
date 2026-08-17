# 床侧面（bed-side）

## 用途

`bed` 方块的**侧面**贴图（床头/床尾两半共用）。侧视：上沿红被垂边盖住
下部木床箱，底部两只短床腿。红被色与顶面两张一致。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 左右无缝（多格长床拼接） |
| Alpha | 无，完全不透明 |
| 颜色数 | 8 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 床腿/床箱阴影（最深） | `#4E3826` |
| 床箱板缝 | `#6B4E2E` |
| 床箱木板主色 | `#8A6741` |
| 床箱木板亮部 | `#9C7549` |
| 被垂边深红 | `#8E2A24` |
| 被垂边主红 | `#A03028` |
| 被垂边亮红 | `#C03A30` |
| 被沿阴影线 | `#6E241E` |

红系沿用 `bed-head-top` 的暖红（B < G），另加一档 `#6E241E` 专画被沿折阴影。

## 视觉描述

- 上部 10 行是红被垂边：主红 `#A03028`，底缘 1 行 `#8E2A24`，
  顶缘 1 行 `#6E241E` 折阴影，垂边上有 2–3 段 1 像素 `#C03A30` 短亮褶
- 中部 18 行是木床箱：2 条水平木板（板缝 `#6B4E2E`），板面 1 像素横向木纹
- 底部 4 行：左端与右端各一只 4×4 床腿 `#4E3826`，中间凹进去 2 行
  （床腿间的空隙也画 `#4E3826`，保持不透明）
- 左右两边缘的垂边褶与木板纹**必须连续**（长床由多格拼接）

## AI 提示词

```
A seamless tileable pixel art texture of a bed seen from the side, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: the top 10 rows are a warm red blanket overhang with a dark fold
shadow line on its very top edge, a darker red line on its bottom edge and a
few short bright red fold marks. The middle 18 rows are a wooden bed base of
two horizontal planks with thin seam lines and subtle horizontal grain. The
bottom 4 rows form two short dark wooden legs, one at each side, with the
recessed gap between them filled with the same dark wood.

Palette strictly limited to: #4E3826, #6B4E2E, #8A6741, #9C7549 for wood,
#6E241E, #8E2A24, #A03028, #C03A30 for the red blanket. All reds are warm
orange-leaning, never cool pink, never magenta.

CRITICAL: The texture must tile seamlessly on the left and right edges —
blanket folds and wood grain continue across the seam.

No pillow, no headboard, no person, no pattern, no text, no watermark, no
vignette, no drop shadow, no gradient, no 3D perspective.
```

## 负面提示词

```
gradient, vignette, drop shadow, pillow, headboard, person, pattern, text,
watermark, blur, 3D render, perspective, photorealistic, cool pink, magenta
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 8 个色值
3. 左右偏移半幅自检接缝
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 上红被 / 中木箱 / 下床腿三段比例约为 10 : 18 : 4
- [ ] 颜色数 ≤ 8，红全部为暖红（不存在 B > G 的像素）
- [ ] 左右偏移半幅后垂边褶与木纹自然衔接
- [ ] 与 `bed-head-top.png` 并排，红被三色逐色一致
