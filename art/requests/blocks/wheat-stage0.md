# 小麦 · 幼芽期（wheat-stage0）

## 用途

小麦作物 3 阶段的**第 0 阶**（刚播种）。十字面片渲染（与花草同管线），
背景洋红键控为透明。三阶贴图共用一套骨架，只差生长差异句，保证生长动画
连续不跳变。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **80% – 92%**（幼芽极小） |
| 颜色数 | 4 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 芽阴影 | `#3C6626` |
| 芽主色 | `#4A7E2F` |
| 芽亮部 | `#5D9C3C` |
| 新芽高光 | `#74B84E` |
| 键控色 | `#FF00FF`（后处理删除） |

即草绿四色——刚发芽的小麦就是草色，成熟后才转金。

## 视觉描述

- 一排 5–6 个**幼芽**贴着底边：每个只有 2–3 像素高、1–2 像素宽的小竖点/小 V 芽
- 整体只占画面底部约 10% 高度
- 无茎、无穗、无叶
- 左右各留 ≥2 像素洋红边，顶部大片洋红

## AI 提示词

```
A pixel art sprite of a newly planted wheat sprout row on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: just-sprouted wheat: a row of five or six tiny green shoots along the
very bottom edge of the canvas, each shoot only 2 or 3 final pixels tall,
covering only about the bottom 10 percent of the canvas height. No stems, no
ears, no leaves yet. The shoots may touch the bottom edge; keep at least 2
final pixels of magenta on the left and right sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #3C6626, #4A7E2F, #5D9C3C, #74B84E. Fresh grass
green only.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
watering can, no seeds, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, seeds, tools,
tall plant, mature wheat, ears, text, watermark, blur, 3D render, perspective,
anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红占比 > 50% 的像素 Alpha = 0，其余 255
3. 去洋红边（R > G 且 B > G 的不透明像素替换为相邻主体色）
4. 量化到上表 4 色
5. 统计透明面积 80% – 92%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 幼芽总高 ≤ 4 像素，只出现在底部
- [ ] 透明占比 80% – 92%
- [ ] 无残留洋红与紫边
- [ ] 与 `wheat-stage1.png` 并排能看出同一株作物在长大
