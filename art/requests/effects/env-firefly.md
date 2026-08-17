# FX-02 环境·萤火虫光点

## 用途

环境粒子：夜晚草地/森林群系的萤火虫。粒子系统在低空随机漂移并呼吸式明灭，
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
| 核亮 | `#F8F8D8` |
| 光晕黄绿 | `#D8E878` |
| 外晕暗 | `#9CB040` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

单个发光点：中心一个小的奶白亮核，外圈两层黄绿光晕（内亮外暗），
像夜里的萤火。不画虫体——虫体由粒子系统的小黑点另行绘制。

## AI 提示词

```
A single particle sprite for a retro voxel game, 1024x1024 pixel art
designed to be downscaled to 32x32. One glowing firefly light dot: a
tiny bright cream-white core in the center, surrounded by a yellow-green
halo ring, and a faint darker yellow-green outer edge. Centered on the
canvas. The entire background is solid flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing between glow and magenta.

Glow palette strictly: #F8F8D8, #D8E878, #9CB040 only.

No text, no wings, no insect body, no legs, no shadow, no outline, no
blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, insect,
bug, wings, antenna, night background, stars, moon, gray background
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
- [ ] 光晕同心：亮核在正中，三层色由内向外依次变暗
