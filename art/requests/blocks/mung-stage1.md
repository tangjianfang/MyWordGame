# 绿豆 · 半熟期（mung-stage1）

## 用途

绿豆作物 3 阶段的**第 1 阶**（半熟）。共用骨架，生长差异句：60% 高的
攀援藤蔓带卷须与心形叶。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **55% – 70%** |
| 颜色数 | 4 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 藤阴影 | `#4E7A2E` |
| 藤主色 | `#63922F` |
| 叶亮部 | `#78A838` |
| 叶高光 | `#8FC24A` |
| 键控色 | `#FF00FF`（后处理删除） |

沿用 stage0 的黄绿四色——同一株作物不同阶段不换色系。

## 视觉描述

- 2–3 根细藤从底部向上攀到约 60%（约 19 像素），藤身 1 像素、略呈 S 形
- 沿藤分布 4–6 片**心形小叶**（2–3 像素）
- 藤顶有 1–2 个 2 像素的**卷须**小弯钩
- 无豆荚（还没结荚）
- 左右留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a half-grown mung bean vine on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: half-grown mung beans: two or three thin S-curved vines climbing from
the bottom to about 60 percent of the canvas height. Along each vine sit four
to six small heart-shaped leaves of 2 or 3 final pixels, and each vine tip ends
in a tiny 2-pixel curling tendril. No pods yet. The vines may touch the bottom
edge; keep at least 2 final pixels of magenta on the left, right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #4E7A2E, #63922F, #78A838, #8FC24A. Warm
yellow-leaning green.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
pole, no trellis, no pods, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, pole, trellis,
stake, pods, beans, flowers, text, watermark, blur, 3D render, perspective,
anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 4 色
3. 统计透明面积 55% – 70%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 藤高约 60%，有心形叶与卷须、无豆荚
- [ ] 透明占比 55% – 70%
- [ ] 无残留洋红与紫边
- [ ] 与 `mung-stage0/2` 并排高度递增
