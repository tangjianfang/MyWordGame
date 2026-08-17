# S-10 星空·极光

## 用途

星空奇观：雪原群系冬夜垂落的极光帘，天空系统在高天穹按正弦波排布、
缓慢摆动。主菜单「menu-snow-aurora」场景同用本图作素材。
**背景必须整片纯洋红键控为透明**。

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
| 顶绿 | `#66E89A` |
| 青绿 | `#4CC6C4` |
| 尖亮 | `#A8F2EF` |
| 尾淡紫 | `#C8A8F2` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一挂竖向极光帘：数条波浪形光带自上垂下，顶缘最亮（绿），向下过渡青绿，
末端收成淡紫尖，顶缘缀几道更亮的薄荷色细闪。绿→青→紫的经典渐变，
但每档是硬边色块不是平滑渐变。

## AI 提示词

```
A single night sky spectacle sprite for a retro voxel game, 1024x1024
pixel art designed to be downscaled to 128x128. A vertical aurora
curtain: several wavy light ribbons hanging downward across the canvas,
the top edge bright green, fading through teal into pale violet tips at
the bottom, with a few brighter mint spark lines along the top. Each
color step is a hard-edged pixel band, not a smooth gradient. Centered
on the canvas. The entire background is solid flat pure magenta #FF00FF,
fully saturated, hard edges, no anti-aliasing between ribbons and
magenta.

Aurora palette strictly: #66E89A, #4CC6C4, #A8F2EF, #C8A8F2 only.

No stars, no moon, no mountains, no snow ground, no trees, no text, no
blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, blur, gradient, 3D render, stars, moon, mountains
silhouette, snowy landscape, pine trees, camera lens flare, gray
background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 4 个色值

## 验收

- [ ] 128×128 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 4
- [ ] 无偏紫残留（尾淡紫 `#C8A8F2` 落在调色板内，不被误清）
- [ ] 光带上下走向连贯，横向平铺两幅无拼缝感
