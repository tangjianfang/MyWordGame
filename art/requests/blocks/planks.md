# B-11 木板

## 用途

玩家最主要的建材，房屋墙面与地板会**大面积**使用。这是本项目里对平铺最敏感的贴图之一：
木板有明确的横向板条结构，一旦上下接不上，整面墙就会出现规律断层。

## 平铺的特殊性

其它方块要求「看不出接缝」，木板要求「**接缝正好落在板条的分界线上**」。
即：图片顶边和底边必须都恰好是一条板条缝，这样上下堆叠时缝隙自然连成一体。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（上下缝对齐板条边界） |
| Alpha | 无，完全不透明 |
| 颜色数 | 6 – 10 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 板缝（最深） | `#6B4E2E` |
| 阴影木纹 | `#8A6741` |
| 主色 | `#9C7549` |
| 亮部 | `#B98D57` |
| 高光 | `#CFA66B` |

比 `log-top` 的木心稍亮、稍黄，读作「加工过的木材」。不要偏红（那是砖块的领域）。

## 视觉描述

**4 条水平木板**，每条高 8 像素，上下堆叠占满 32 像素。

- 板与板之间是 1 像素的深色缝 `#6B4E2E`
- **顶边第 0 行和底边第 31 行必须分别是板条的起始与结束**，使上下平铺时缝对齐
- 每条板内部有 2–3 道**水平方向**的细木纹（1 像素高，明度略有差异），木纹长短不一、
  不要贯穿整条板
- 每条板可以有 1–2 个小木节（2×2 像素的深色点），但四条板上的木节位置要错开，
  且**不要放在靠近左右边缘的 3 像素内**（避免平铺时相邻两块的木节挨在一起）
- 木纹必须**水平**，不能是竖直或斜向

## AI 提示词

```
A seamless tileable pixel art texture of wooden planks, flat front view, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: exactly 4 horizontal wooden planks of equal height stacked from top to
bottom, separated by thin dark seam lines. The top edge of the image is the start of
the first plank and the bottom edge is the end of the last plank, so the seams line
up when tiled vertically. Each plank has 2 or 3 subtle horizontal wood grain lines of
varying length and one or two small dark knots. Knot positions are staggered between
planks and kept away from the left and right edges.

Color palette strictly limited to these warm wood tones: #6B4E2E, #8A6741, #9C7549,
#B98D57, #CFA66B. Golden brown, no red tint.

CRITICAL: The texture must tile seamlessly on all four edges. Horizontal grain must
continue across the left and right edges. Plank seams must align across the top and
bottom edges.

No border, no frame, no outline, no nails, no screws, no bolts, no vertical planks,
no diagonal grain, no text, no watermark, no vignette, no drop shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline, nails,
screws, bolts, metal, vertical planks, diagonal wood, parquet, herringbone, varnish,
glossy, reflection, text, watermark, blur, 3D render, perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. **检查板条高度**：必须恰好 4 条 × 8 像素，若降采样后变成 7/8/9 不均匀，
   手动修正到均匀的 8 像素
4. 上下偏移半幅自检：板缝应正好接上
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 恰好 4 条板，每条 8 像素高
- [ ] 上下偏移半幅后板缝对齐，看不出「半条板」
- [ ] 左右偏移半幅后木纹自然衔接
- [ ] 颜色数 ≤ 10
- [ ] 4×4 平铺预览中，木节没有形成规律阵列
- [ ] 与 `log-top` 并排，木板明显更亮更「加工过」
