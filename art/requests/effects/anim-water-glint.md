# FX-15 方块动态·水面波光

## 用途

方块动态帧：静止水面随机闪烁的菱形波光点，水面着色器按格子采样叠加。
单帧图（非帧序列）。**背景必须整片纯洋红键控为透明**。

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
| 波光白 | `#E7F4F8` |
| 波光亮蓝 | `#A8D0F0` |
| 涟漪蓝 | `#4E88CE` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一枚菱形波光：中心一个小的近白亮点，外面包一层亮蓝菱形光斑，
后面交叉两条细长的水蓝斜线（涟漪方向感）。整体读作「水面一闪」。

## AI 提示词

```
A single water animation sprite for a retro voxel game, 1024x1024
pixel art designed to be downscaled to 32x32. A diamond-shaped water
surface glint: a small bright near-white sparkle dot in the center, a
light blue diamond sheen shape around it, and two thin medium blue
diagonal ripple streaks crossing behind it. Centered on the canvas.
The entire background is solid flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing between glint and magenta.

Water palette strictly: #4E88CE, #A8D0F0, #E7F4F8 only.

No fish, no bubbles, no waves scenery, no water body, no text, no
shadow, no outline, no blur, no semi-transparent pixels. Hard pixel
edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, lake,
river, ocean, waves, fish, bubbles, sun reflection scenery, gray
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
- [ ] 波光占画布 ≤ 50%，叠在水面上不会糊成一片白
