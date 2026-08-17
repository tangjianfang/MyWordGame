# 小麦 · 半熟期（wheat-stage1）

## 用途

小麦作物 3 阶段的**第 1 阶**（半熟）。与 `wheat-stage0` / `wheat-stage2`
共用提示词骨架，只换生长差异句：本阶是「60% 高的绿秆、穗尖刚开始转金」。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **60% – 75%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 茎阴影 | `#3C6626` |
| 茎主色 | `#4A7E2F` |
| 叶亮部 | `#5D9C3C` |
| 新叶高光 | `#74B84E` |
| 穗尖初金 | `#C8A838` |
| 键控色 | `#FF00FF`（后处理删除） |

四绿沿用 stage0（同一株作物），金色只出现在穗尖，转色是「刚开始」而非「半金」。

## 视觉描述

- 5–7 根**直立绿秆**并排，总高约 60%（约 19 像素），秆间留 1–2 像素缝隙
- 每根秆中下部有 1–2 片斜向上尖叶
- 每根秆顶端 2–3 像素开始泛金 `#C8A838`（未成穗的小穗尖），以下仍是绿秆
- 秆底可触底边，左右留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a half-grown wheat plant on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: half-grown wheat: five to seven upright green stalks side by side,
reaching about 60 percent of the canvas height, with one or two small pointed
leaves angled upward on each stalk. Only the top 2 or 3 final pixels of each
stalk are just starting to turn pale gold, like an unformed young ear; the
stems and leaves below stay green. The stalks may touch the bottom edge; keep
at least 2 final pixels of magenta on the left, right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #3C6626, #4A7E2F, #5D9C3C, #74B84E for green
stalks and leaves, #C8A838 only for the pale gold tips.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
fence, no text, no watermark, no gradient, no blur, no 3D render, no
anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, fence, fully
golden wheat, full ears, dead plant, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计透明面积 60% – 75%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 秆高约 60%（17–21 像素），金色只在顶端 2–3 像素
- [ ] 透明占比 60% – 75%
- [ ] 无残留洋红与紫边
- [ ] 三阶并排（stage0/1/2）高度递增、绿色渐少金色渐多
