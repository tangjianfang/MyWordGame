# 丁香（flower-lilac）

## 用途

花草方块（十字面片渲染）。森林群系的成簇小花，靠「一簇很多小穗」与
单头花区分。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，总高约 70% |
| 透明面积 | **45% – 65%** |
| 颜色数 | 7 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 花穗暗蓝紫 | `#6E74B8` |
| 花穗主色 | `#7E82C9` |
| 花穗亮色 | `#9296D4` |
| 花穗浅色 | `#A8ABDE` |
| 花心米白 | `#F0E8C8` |
| 茎绿 | `#3F7A2E` |
| 叶绿 | `#52993B` |
| 键控色 | `#FF00FF`（后处理删除） |

**关键约束**：四档蓝紫全部 R ≤ G（偏蓝的冷紫）。真正的紫红丁香
（R > G 且 B > G）会被后处理「去洋红边」当键控残留整片误删，这条流水线上
只有偏蓝的丁香能活。

## 视觉描述

- 单株：短茎从底边到约 25% 高度，带 2 片小卵叶
- 茎顶一大簇**丁香花穗**：椭圆形花簇约 12×14 像素，由 8–10 个 2–3 像素
  小穗团组成，四档蓝紫由外到内渐浅
- 每个小穗中心点缀 1 像素米白小花心
- 总高约 70%，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single lilac flower cluster on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one lilac plant centered. A short green stem rises from the bottom
edge to about 25 percent height with two small oval leaves, then one big oval
lilac cluster about 12 by 14 final pixels made of eight to ten tiny 2 or 3
pixel floret tufts. The tufts use four blue-violet layers, darkest on the
outside and palest toward the center, and each tuft carries a single 1-pixel
cream dot as its flower heart. Total plant height is about 70 percent of the
canvas. Keep at least 2 final pixels of magenta on the left, right and top
sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #6E74B8, #7E82C9, #9296D4, #A8ABDE for florets,
#F0E8C8 for the cream dots, #3F7A2E and #52993B for stem and leaves. Cool
blue-violet only, never reddish purple, never pink, never magenta tint.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no bush, no shrub, no
multiple stems, no pink, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, bush, shrub, multiple
stems, reddish purple, pink, magenta tint, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 7 色
3. 统计透明面积 45% – 65%
4. 重点抽查：花簇中心是否存在被去洋红边误删后留下的绿色补洞（R > G 且
   B > G 像素被顶替的痕迹），有则提示词把紫色再压蓝一些重生成

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 70%，一簇蓝紫小穗
- [ ] 所有花簇像素 R ≤ G（偏蓝不偏红）
- [ ] 透明占比 45% – 65%，无残留洋红与紫边
