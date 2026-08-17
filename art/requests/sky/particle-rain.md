# S-03 天气·雨滴

## 用途

天气粒子：雨天从天上落下的雨滴划痕，粒子系统整屏撒布、快速下落。
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
| 雨白 | `#E7F4F8` |
| 雨亮蓝 | `#A8D0F0` |
| 雨蓝 | `#4E88CE` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

两条平行的斜向雨痕：每条是细长划线，顶端近白，向尾部过渡淡蓝、蓝，
整体斜约 30°（表现下落时的风偏）。

## AI 提示词

```
A single weather particle sprite for a retro voxel game, 1024x1024
pixel art designed to be downscaled to 32x32. Two parallel raindrops
falling diagonally: each drop is a thin long streak with a bright
near-white top fading into pale blue then medium blue toward the bottom
tail, both streaks tilted about thirty degrees from vertical. Centered
on the canvas. The entire background is solid flat pure magenta #FF00FF,
fully saturated, hard edges, no anti-aliasing between drops and
magenta.

Rain palette strictly: #4E88CE, #A8D0F0, #E7F4F8 only.

No clouds, no puddle, no lightning, no splash, no umbrella, no text,
no shadow, no outline, no blur, no semi-transparent pixels. Hard pixel
edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, storm
clouds, lightning, splash, umbrella, rain scenery, gray background
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
- [ ] 两条雨痕斜角一致（约 30°），全屏撒布时方向统一
