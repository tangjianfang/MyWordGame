# S-11 星空·流星

## 用途

星空奇观：晴夜偶现的流星事件，天空系统随机触发、1s 划过后消失。
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
| 头白 | `#FFFFFF` |
| 焰金 | `#F7DA7A` |
| 焰橙 | `#F79B22` |
| 尾冰蓝 | `#C8E2EA` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一颗从右上划向左下的流星：右端小的纯白火球头带金黄内焰，向右后拖
渐细的金橙色焰尾，尾梢散成几粒冰蓝碎光。头亮尾散、方向单一。

## AI 提示词

```
A single night sky spectacle sprite for a retro voxel game, 1024x1024
pixel art designed to be downscaled to 32x32. A single shooting star
streaking diagonally from upper right to lower left: a small bright
white fireball head with a golden yellow inner glow, followed by a
tapering trail of golden yellow fading into orange, ending in a few
pale ice-blue specks at the tail tip. Centered on the canvas. The
entire background is solid flat pure magenta #FF00FF, fully saturated,
hard edges, no anti-aliasing between trail and magenta.

Meteor palette strictly: #FFFFFF, #F7DA7A, #F79B22, #C8E2EA only.

No multiple meteors, no stars scattered around, no moon, no clouds, no
text, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, blur, gradient, 3D render, meteor shower, multiple
streaks, stars, moon, clouds, night landscape, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 4 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 4
- [ ] 无偏紫残留
- [ ] 恰好一条流星（无并行第二条），头端在最左下、尾梢最右上
