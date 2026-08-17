# FX-03 环境·蒲公英绒毛

## 用途

环境粒子：草原群系风吹起的蒲公英绒毛，飘过镜头前的柔化元素。
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
| 绒白 | `#F8F8F8` |
| 种子灰 | `#D8D8D8` |
| 茎绿 | `#5D9C3C` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一粒飘飞的蒲公英种子：顶部一团放射状的白色绒毛小伞，中心向下连一粒
浅灰种子与一小段绿色茎梗。只画绒毛不画黄花——那是方块层的事。

## AI 提示词

```
A single particle sprite for a retro voxel game, 1024x1024 pixel art
designed to be downscaled to 32x32. A drifting dandelion seed fluff: a
small bright white puff of radiating fluff threads at the top, a tiny
light gray seed dot at the bottom center, and one short green stem stub.
Centered on the canvas. The entire background is solid flat pure magenta
#FF00FF, fully saturated, hard edges, no anti-aliasing between fluff and
magenta.

Fluff palette strictly: #F8F8F8, #D8D8D8, #5D9C3C only.

No text, no full flower, no yellow petals, no yellow bloom, no shadow,
no outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, dandelion
flower, yellow bloom, field, grass, meadow, wind lines, gray background
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
- [ ] 白绒毛中不含黄色（黄色会被误认成花）
