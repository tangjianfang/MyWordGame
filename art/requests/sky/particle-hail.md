# S-05 天气·冰雹

## 用途

天气粒子：暴风雪/雷暴天气的冰雹粒，比雪粒更大更快、略带弹跳。
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
| 雹体冰蓝 | `#C8E2EA` |
| 雹高光白 | `#F8F8FF` |
| 雹底灰蓝 | `#A9CBD6` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

两颗斜落的冰雹球：每颗是圆滚滚的冰蓝实心球，左上一颗白高光点、
底缘一圈灰蓝阴影。球体饱满有体积感，与雪粒的「平面圆点」拉开差距。

## AI 提示词

```
A single weather particle sprite for a retro voxel game, 1024x1024
pixel art designed to be downscaled to 32x32. Two small hailstones
falling diagonally: round pale ice-blue balls, each with a bright white
highlight dot in the upper left and a gray-blue shaded bottom edge.
Centered on the canvas. The entire background is solid flat pure
magenta #FF00FF, fully saturated, hard edges, no anti-aliasing between
hail and magenta.

Hail palette strictly: #A9CBD6, #C8E2EA, #F8F8FF only.

No rain streaks, no snow, no clouds, no ground, no text, no shadow
outside the stones, no outline, no blur, no semi-transparent pixels.
Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, rain,
lightning, storm clouds, hailstorm scenery, gray background
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
- [ ] 每颗雹粒径 6–10 像素，明显大于 particle-snow 的雪粒
