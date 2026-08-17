# S-07 星空·星点

## 用途

星空奇观：夜空穹顶的闪烁星点精灵，天空系统按固定种子撒布、按时间呼吸明灭。
程序生成星星点阵时用本图作为「星星」的贴图（README 的程序撒点方案 +
一张像素星贴图提升质感）。**背景必须整片纯洋红键控为透明**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 星白 | `#F8F8FF` |
| 星金 | `#F7DA7A` |
| 星蓝 | `#A9CBD6` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

五颗大小两档的四芒小星：三颗白、一颗金、一颗淡蓝，散布在画布上，
星与星之间留大片空隙。单颗是简洁的「十/× 四芒」，不画光芒拖尾。

## AI 提示词

```
A single night sky sprite for a retro voxel game, 1024x1024 pixel art
designed to be downscaled to 64x64. A loose cluster of five small
twinkling stars in two sizes: three bright white four-point stars, one
pale golden star, and one pale blue star, scattered with clear space
between them. Centered on the canvas. The entire background is solid
flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing between stars and magenta.

Star palette strictly: #F8F8FF, #F7DA7A, #A9CBD6 only.

No moon, no clouds, no nebula, no galaxy band, no shooting star, no
star trail, no text, no blur, no semi-transparent pixels. Hard pixel
edges.
```

## 负面提示词

```
text, watermark, blur, gradient, 3D render, moon, clouds, nebula,
galaxy, shooting star, constellation lines, night landscape, gray
background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 3 个色值

## 验收

- [ ] 64×64 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 3
- [ ] 无偏紫残留
- [ ] 恰好 5 颗星，最小星在 64×64 下 ≥ 2 像素（不消失）
