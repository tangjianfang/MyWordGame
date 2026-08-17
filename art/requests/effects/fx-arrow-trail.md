# FX-06 战斗·箭矢尾迹

## 用途

战斗特效：箭飞行时拖在后面的速度线，贴箭尾方向拉伸。
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
| 迹白 | `#F8F8F8` |
| 迹黄 | `#F7E8B0` |
| 迹灰 | `#C8C8C8` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

三条横向并排的渐细速度线：中间最长最亮（白），上下各一条较短
（淡黄/浅灰），左端尖、右端渐隐。不画箭本身——箭是物品模型。

## AI 提示词

```
A single combat effect sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A horizontal arrow speed trail:
three tapering streaks side by side, the longest bright white streak in
the middle, flanked by shorter pale cream and light gray streaks, all
with sharp pointed left ends and thinner pointed right ends. Centered
on the canvas. The entire background is solid flat pure magenta #FF00FF,
fully saturated, hard edges, no anti-aliasing between streaks and
magenta.

Trail palette strictly: #F8F8F8, #F7E8B0, #C8C8C8 only.

No arrow, no bow, no feathers, no fletching, no text, no shadow, no
outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, arrow
projectile, bow, feather, wind lines, speed lines background, gray
background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 3 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 3
- [ ] 无偏紫残留
- [ ] 三条迹线左端齐头、右端渐细，镜像翻转后可复用为反向尾迹
