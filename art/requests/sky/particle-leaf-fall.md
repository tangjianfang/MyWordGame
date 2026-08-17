# S-06 天气·落叶

## 用途

天气粒子：森林/草原群系秋天飘落的叶片，慢速螺旋下落。
**背景必须整片纯洋红键控为透明**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 叶深 | `#3F7A2E` |
| 叶主 | `#5D9C3C` |
| 叶亮 | `#74B84E` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

两片飘落的叶子：简单的圆头尖叶，一片中绿、一片深绿，各带一点亮绿
脉点，朝不同方向倾斜（表现打转下落）。**保持绿色**——秋黄叶走
seasonal 目录的 autumn-leaves，本图用于常绿群的日常氛围。

## AI 提示词

```
A single weather particle sprite for a retro voxel game, 1024x1024
pixel art designed to be downscaled to 32x32. Two small falling leaves:
simple rounded pointed leaves in medium green and darker green, each
with a tiny bright light-green vein dot, tilted in different directions
as if drifting and spinning down. Centered on the canvas. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing between leaves and magenta.

Leaf palette strictly: #3F7A2E, #5D9C3C, #74B84E only.

No tree, no branches, no trunk, no autumn colors, no yellow leaves, no
orange leaves, no ground, no text, no shadow, no outline, no blur, no
semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, tree,
branch, autumn colors, yellow leaves, orange leaves, forest scenery,
gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 3 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 3，全部为绿色系
- [ ] 无偏紫残留
- [ ] 两片叶朝向不同（旋转复用时不显呆板）
