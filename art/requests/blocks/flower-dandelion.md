# 蒲公英（flower-dandelion）

## 用途

花草方块（十字面片渲染）。草原最常见的黄色小花，与虞美人搭配构成
「草原有花」的层次。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，总高约 70% |
| 透明面积 | **45% – 65%** |
| 颜色数 | 6 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 花瓣暗金 | `#DCAE3A` |
| 花瓣亮金 | `#F7DA7A` |
| 花瓣高光 | `#FFF0A8` |
| 花心橙金 | `#C89A28` |
| 茎绿 | `#3F7A2E` |
| 叶绿 | `#52993B` |
| 键控色 | `#FF00FF`（后处理删除） |

金黄三色沿用 `gold-ore` 的金黄系，与向日葵共用，花田色彩统一。

## 视觉描述

- 单株：1 像素绿茎从底边到约 40% 高度，带 2 片锯齿小叶
- 茎顶一个**蓬松圆花头**：8–10 像素直径的圆簇，由许多 1–2 像素小瓣团组成，
  中心一撮 `#C89A28` 橙金
- 瓣团三层金（暗/亮/高光）交错，轮廓圆但边缘松散（绒感）
- 总高约 70%，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single yellow dandelion flower on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one dandelion plant centered. A thin 1-pixel green stem rises from the
bottom edge to about 40 percent height with two small jagged leaves, then one
fluffy round flower head 8 to 10 final pixels across, built from many tiny 1
or 2 pixel petal puffs in three gold layers, with a small orange-gold cluster
in the very center. The outline is round but loosely fringed. Total plant
height is about 70 percent of the canvas. Keep at least 2 final pixels of
magenta on the left, right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #DCAE3A, #F7DA7A, #FFF0A8 for petals, #C89A28 for
the center, #3F7A2E and #52993B for stem and leaves.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no grass, no vase, no
seed clock, no white puffball, no multiple flowers, no text, no watermark, no
gradient, no blur, no 3D render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, grass, vase, seed clock,
puffball, white seeds, multiple flowers, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 6 色
3. 统计透明面积 45% – 65%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 70%，黄色蓬松圆花头
- [ ] 是黄花不是白绒球（种子球另有特效贴图）
- [ ] 透明占比 45% – 65%，无残留洋红与紫边
