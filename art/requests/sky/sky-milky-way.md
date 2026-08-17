# S-08 星空·银河带

## 用途

星空奇观：晴夜横贯天穹的银河星带，天空系统沿大圆方向平铺条带、按月相轮换
显隐。**背景必须整片纯洋红键控为透明**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 128 × 128 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 带深紫 | `#8A7AB8` |
| 带淡紫 | `#C8BCE8` |
| 星白 | `#F8F8FF` |
| 星蓝 | `#A9CBD6` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一条从左下斜贯右上的银河带：宽带主体淡紫，带内密布细小白色星点与
几颗淡蓝星，深紫团块做「星云块」。带外干净，不撒散星。

## AI 提示词

```
A single night sky sprite for a retro voxel game, 1024x1024 pixel art
designed to be downscaled to 128x128. A diagonal band of milky way star
dust: a wide soft band of pale lavender crossing the canvas from lower
left to upper right, dotted densely with tiny bright white stars, a few
pale blue stars, and small deeper purple clumps inside the band, with
the area outside the band left empty. Centered on the canvas. The
entire background is solid flat pure magenta #FF00FF, fully saturated,
hard edges, no anti-aliasing between band and magenta.

Milky way palette strictly: #8A7AB8, #C8BCE8, #F8F8FF, #A9CBD6 only.

No moon, no shooting star, no aurora, no constellation lines, no text,
no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, blur, gradient, 3D render, moon, shooting star,
aurora, rainbow, clouds, mountains silhouette, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 4 个色值

## 验收

- [ ] 128×128 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 4
- [ ] 无偏紫残留（带内紫色须全部落在调色板 4 色内）
- [ ] 星带沿对角线走向连贯，带外无散星
