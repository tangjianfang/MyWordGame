# M11-C18 徽章 · 渔夫（badge-fisherman）

## 用途

milestone-11 成就系统「渔夫」徽章：钓起累计 100 条鱼。
前景主题物 = **一条蓝鱼**（水蓝系，与水/钻石拉开的具体鱼形）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-fisherman.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 鱼身蓝主色 | `#3A6FB5` |
| 鱼背深蓝 | `#2C5893` |
| 鱼肚浅青白 | `#C8E2EA` |
| 眼黑 | `#1A1A1A` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

圆形底盘同模板。前景：一条侧视小鱼横游盘面中央——
鱼身水蓝 `#3A6FB5`、背脊压深 `#2C5893`、鱼肚浅青白 `#C8E2EA`，
一枚黑点眼、三角尾鳍。全部包黑描边。不画钓钩/水花（保持一物一义）。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, side view of the fish only, no
perspective, no anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one small fish seen from the side, swimming horizontally across the
center of the disc, filling about 40 percent of the image. Its body is water
blue #3A6FB5 with a darker back #2C5893 and a pale belly #C8E2EA, one black dot
eye and one triangular tail fin. The fish is wrapped in a near-black outline and
faces left.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #3A6FB5, #2C5893,
#C8E2EA, #1A1A1A.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No fishing rod, no hook, no water waves, no bubbles, no second fish, no text, no
letters, no numbers, no ribbon, no laurel wreath, no gradient, no glow, no
bevel, no 3D render, no watermark.
```

## 负面提示词

```
fishing rod, hook, water waves, bubbles, second fish, text, letters, numbers,
ribbon, laurel wreath, gradient, glow, bevel, 3D, anti-aliasing, opaque
background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 7 个色值
4. 与其余 15 枚徽章并排核对：底盘逐像素一致
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 底盘与 `badge-first-night` 逐像素一致（仅前景不同）
- [ ] 鱼头/鱼尾/鱼眼可辨，鱼朝左
- [ ] 徽章外全部透明（Alpha 0）
