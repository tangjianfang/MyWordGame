# M11-U16 伤害数字样式帧（fx-damage-number）

## 用途

milestone-11 战斗手感：对生物造成伤害时头顶飘出的伤害数字**样式基准帧**。
数字类动效帧统一风格：**白色粗像素数字 + 黑色描边 + 洋红背景键控**。
本图固定画「8」，运行时其余数字用同风格像素字体渲染。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素（与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（数字外全部键控透明） |
| 输出数量 | 1 张：`fx-damage-number.png` |

## Alpha 的产出方式

数字之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 数字白 | `#FFFFFF` |
| 数字描边黑 | `#1A1A1A` |
| 背景键控色 | `#FF00FF` |

普通伤害只有白黑两色——彩色留给暴击（`fx-crit`），层级一眼分明。

## 视觉描述

画面中央一个「8」：

- 白色粗体像素数字，笔画外一圈 **1 像素黑描边**
- 数字高约占画布高度的 80%，比经验飘字（70%）更满——伤害数字是战斗中最需要
  一眼读到的信息
- 无任何附加记号，背景整片纯洋红

## AI 提示词

```
A pixel art damage number label for a retro game, 1024x1024, designed to be
downscaled to 32x32 pixel art.

Style: chunky bold pixel digits, flat shading, symmetric, no perspective, no
gradient, no glow, no motion blur.

Content: centered in the image, one single large digit "8" in bold white pixel
style. Every stroke of the digit is wrapped in a thick one-pixel black outline.
The digit fills about 80 percent of the canvas height and is the only thing in
the image.

Color palette strictly limited to: #FFFFFF, #1A1A1A.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No second digit, no letters, no words, no minus sign, no arrows, no sparkles, no
stars, no borders, no gradient, no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
second digit, letters, words, minus sign, arrows, sparkles, stars, borders,
gradient, glow, shadow, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 2 个色值
4. 核对描边完整：白色像素不与透明区直接相邻
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 恰有一个数字「8」，笔画带完整黑描边
- [ ] 颜色数 ≤ 2（不含透明）
- [ ] 叠在纯白、纯黑、天蓝三种背景上都清晰可辨
- [ ] 与 `fx-xp-float.png` 并排时字体风格一致，且更显眼
