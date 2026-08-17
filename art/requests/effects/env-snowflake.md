# FX-04 环境·雪花晶

## 用途

环境粒子：雪原群系的大片雪花（与天气系统的小雪粒 `particle-snow` 区分：
本图是**单片六角晶**特写，数量少、贴镜头近）。
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
| 雪白 | `#F8F8FF` |
| 冰蓝 | `#C8E2EA` |
| 蓝灰 | `#A9CBD6` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

单片六角雪花晶：六条等长白色主臂，臂端冰蓝渐层，臂间浅蓝灰细枝。
严格六重对称、居中。色系取玻璃青白，与雪方块同族。

## AI 提示词

```
A single particle sprite for a retro voxel game, 1024x1024 pixel art
designed to be downscaled to 32x32. One six-armed snowflake crystal:
bright white arms with pale ice-blue tips and light blue-gray detail
branches between the arms, perfectly six-fold symmetrical, centered on
the canvas. The entire background is solid flat pure magenta #FF00FF,
fully saturated, hard edges, no anti-aliasing between snowflake and
magenta.

Snow palette strictly: #F8F8FF, #C8E2EA, #A9CBD6 only.

No text, no multiple snowflakes, no clouds, no ground, no shadow, no
outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, multiple
snowflakes, snowfall, sky, clouds, winter scenery, gray background
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
- [ ] 六臂等长，旋转 60° 后与原图基本重合
