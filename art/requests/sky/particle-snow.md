# S-04 天气·雪粒

## 用途

天气粒子：雪天漫天飘落的小雪粒，粒子系统大量撒布、慢速下落带左右摆动。
与 `env-snowflake`（单片六角晶特写）区分：本图是**简单圆粒**。
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
| 粒白 | `#F8F8FF` |
| 粒冰白 | `#E7F4F8` |
| 粒底蓝 | `#C8E2EA` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

三颗大小不一的圆雪粒沿对角线散布：每颗主体亮白，底缘压一线冰蓝
（表现背光），颗粒间留足空隙。就是「小圆点」，不要画成六角晶。

## AI 提示词

```
A single weather particle sprite for a retro voxel game, 1024x1024
pixel art designed to be downscaled to 32x32. Three small simple round
snow pellets scattered diagonally, two different sizes: each pellet is
a bright white dot with a pale ice-blue bottom edge. Centered on the
canvas. The entire background is solid flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing between pellets and magenta.

Snow palette strictly: #F8F8FF, #E7F4F8, #C8E2EA only.

No six-armed snowflakes, no crystals, no clouds, no ground, no snowman,
no text, no shadow, no outline, no blur, no semi-transparent pixels.
Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, six-armed
snowflake, crystals, snowfall scenery, clouds, winter landscape, gray
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
- [ ] 三颗雪粒都是圆点（无六角晶形状），单颗粒径 2–4 像素
