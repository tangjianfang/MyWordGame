# FX-08 战斗·魔法光球

## 用途

战斗特效：Boss「机元守卫」与附魔武器的紫色能量弹本体，飞行途中缓慢自转。
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
| 核亮 | `#F4EEFC` |
| 壳紫 | `#C8A8F2` |
| 环紫 | `#9C6AE8` |
| 边紫 | `#6A4A9C` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一颗圆球状奥术能量球：中心近白亮核，中层亮紫壳，外缘深紫环，
球体四角飘 4 颗小紫色菱形光屑。紫色与 Boss 紫甲同族。

## AI 提示词

```
A single magic effect sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A round arcane orb: a bright
near-white glowing core in the center, a light purple energy shell
around it, a medium purple rim, and a deep purple thin outline, with
four small purple diamond sparkles floating around the orb. Centered
on the canvas. The entire background is solid flat pure magenta #FF00FF,
fully saturated, hard edges, no anti-aliasing between orb and magenta.

Orb palette strictly: #F4EEFC, #C8A8F2, #9C6AE8, #6A4A9C only.

No text, no runes, no stars, no hands, no shadow, no outline outside
the orb, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, crystal,
rune letters, stars, galaxy, hand casting, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 4 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 4
- [ ] 无偏紫残留（去边后紫色全部落在调色板 4 色内）
- [ ] 球体占画布 50% – 75%，光屑不贴画布边缘
