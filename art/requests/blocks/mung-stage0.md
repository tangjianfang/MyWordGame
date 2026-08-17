# 绿豆 · 幼芽期（mung-stage0）

## 用途

绿豆作物 3 阶段的**第 0 阶**（刚播种）。十字面片渲染，背景洋红键控为透明。
三阶共用骨架，只换生长差异句。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **80% – 92%** |
| 颜色数 | 4 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 芽阴影 | `#4E7A2E` |
| 芽主色 | `#63922F` |
| 芽亮部 | `#78A838` |
| 新芽高光 | `#8FC24A` |
| 键控色 | `#FF00FF`（后处理删除） |

绿豆用**偏黄的暖绿**（黄绿系），与小麦/甜菜的草绿拉开一档，三种作物在
田里一眼可分。

## 视觉描述

- 底部一排 5–6 个**幼芽**：2–3 像素高的竖点，个别带 1 像素弯钩（豆芽颈）
- 整体只占底部约 10% 高度
- 无藤蔓、无豆荚
- 左右留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of newly sprouted mung bean seedlings on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: just-sprouted mung beans: a row of five or six tiny yellow-green
shoots along the very bottom edge of the canvas, each only 2 or 3 final pixels
tall, a few with a tiny 1-pixel hooked neck, covering only about the bottom 10
percent of the canvas height. No vines, no pods yet. The shoots may touch the
bottom edge; keep at least 2 final pixels of magenta on the left and right
sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #4E7A2E, #63922F, #78A838, #8FC24A. Warm
yellow-leaning green, clearly yellower than grass.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
beans, no text, no watermark, no gradient, no blur, no 3D render, no
anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, beans, pods,
vines, tall plant, text, watermark, blur, 3D render, perspective,
anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 4 色
3. 统计透明面积 80% – 92%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 幼芽总高 ≤ 4 像素，只出现在底部
- [ ] 透明占比 80% – 92%
- [ ] 绿色偏黄，与 `wheat-stage0.png` 并排能分清两种作物
- [ ] 无残留洋红与紫边
