# 甜菜 · 幼芽期（beet-stage0）

## 用途

甜菜作物 3 阶段的**第 0 阶**（刚播种）。十字面片渲染，背景洋红键控为透明。
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
| 芽阴影 | `#3C6626` |
| 芽主色 | `#4A7E2F` |
| 芽亮部 | `#5D9C3C` |
| 新芽高光 | `#74B84E` |
| 键控色 | `#FF00FF`（后处理删除） |

刚发芽的甜菜与小麦幼芽同色（都是草绿四色），靠子叶形状区分。

## 视觉描述

- 底部一排 4–5 个**双子叶幼芽**：每个是 2–3 像素的「V」形两瓣小叶
- 整体只占底部约 10% 高度
- 无块根、无红茎
- 左右留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of newly sprouted beet seedlings on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: just-sprouted beets: a row of four or five tiny V-shaped two-leaf
sprouts along the very bottom edge of the canvas, each only 2 or 3 final pixels
tall, covering only about the bottom 10 percent of the canvas height. No roots,
no red stems yet. The sprouts may touch the bottom edge; keep at least 2 final
pixels of magenta on the left and right sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #3C6626, #4A7E2F, #5D9C3C, #74B84E. Fresh grass
green only.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
seeds, no red beet, no text, no watermark, no gradient, no blur, no 3D render,
no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, seeds, red beet,
root, mature leaves, text, watermark, blur, 3D render, perspective,
anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 4 色
3. 统计透明面积 80% – 92%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 幼芽总高 ≤ 4 像素，呈 V 形双子叶
- [ ] 透明占比 80% – 92%
- [ ] 无残留洋红与紫边
- [ ] 与 `beet-stage1.png` 并排能看出同一株在长大
