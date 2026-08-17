# M11-C19 徽章 · 全成就（badge-completionist）

## 用途

milestone-11 成就系统「全成就达成」徽章：解锁其余全部 15 枚成就后的最终徽章。
前景主题物 = **一顶嵌宝石的金冠**（收集完毕的最高王座）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-completionist.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 皇冠金主色 | `#F7DA7A` |
| 皇冠金阴影 | `#DCAE3A` |
| 宝石红 | `#D42B2B` |
| 宝石青 | `#4CC6C4` |
| 背景键控色 | `#FF00FF` |

红/青宝石即心与钻石的颜色——「血与钻石都拿到了」。

## 视觉描述

圆形底盘同模板。前景：一顶三尖金冠居盘面中央——
冠体金 `#F7DA7A`、下缘压 `#DCAE3A`，三个尖角各嵌一枚小宝石
（左红右青中金），冠底一条压暗横带。包黑描边。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, symmetrical, no perspective,
no anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one royal crown centered on the disc, filling about 40 percent of
the image. The crown is golden #F7DA7A with a shaded lower band #DCAE3A and
three peaks. The left peak holds one small red gem #D42B2B, the right peak one
small cyan gem #4CC6C4, and the middle peak one small golden gem. The crown is
wrapped in a near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #F7DA7A, #DCAE3A,
#D42B2B, #4CC6C4.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No jewels scattered around, no stars, no wings, no text, no letters, no
numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel, no 3D
render, no watermark.
```

## 负面提示词

```
scattered jewels, stars, wings, text, letters, numbers, ribbon, laurel wreath,
gradient, glow, bevel, 3D, anti-aliasing, opaque background
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
- [ ] 三尖冠对称、左右宝石红/青分明
- [ ] 徽章外全部透明（Alpha 0）
