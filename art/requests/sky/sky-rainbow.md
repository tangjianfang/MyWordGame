# S-09 星空·彩虹

## 用途

星空奇观：雨后白天出现的彩虹拱，天空系统在天穹上沿大圆弧平铺本图。
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
| 虹红 | `#C43A2E` |
| 虹橙 | `#F79B22` |
| 虹黄 | `#F7DA7A` |
| 虹绿 | `#74B84E` |
| 虹蓝 | `#4E88CE` |
| 虹紫 | `#9C6AE8` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一段斜贯画布的彩虹弧带：红→橙→黄→绿→蓝→紫六条等宽硬边平行弧条，
无间隙无渐变，条带方向沿对角线（平铺后拼成整拱）。色值全部取自
全局调色板，与全游戏色系一致。

## AI 提示词

```
A single sky spectacle sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 128x128. A segment of a rainbow arc
crossing the canvas diagonally: six hard-edged parallel curved bands in
order red, orange, yellow, green, blue and purple, all bands equal
width, no gap between them, no gradient inside any band. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing between bands and magenta.

Rainbow palette strictly: #C43A2E, #F79B22, #F7DA7A, #74B84E, #4E88CE,
#9C6AE8 only.

No clouds, no sun, no rain, no pot of gold, no text, no shadow, no
outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, clouds,
sun, rain, landscape, hills, pot of gold, seven bands, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 6 个色值

## 验收

- [ ] 128×128 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 6
- [ ] 无偏紫残留（虹紫 `#9C6AE8` 不得被去边误清——逐像素比对色值）
- [ ] 六条带等宽且顺序为红橙黄绿蓝紫
