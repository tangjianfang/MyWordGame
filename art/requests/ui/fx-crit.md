# M11-U17 暴击数字样式帧（fx-crit）

## 用途

milestone-11 战斗手感：打出暴击时头顶飘出的**暴击数字样式基准帧**。
数字类动效帧统一风格：**白色粗像素数字 + 黑色描边 + 洋红背景键控**；
暴击额外加两道金色速度线，与普通伤害（`fx-damage-number`）拉开层级。
本图固定画「15」，运行时其余数字用同风格像素字体渲染。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素（与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（主体外全部键控透明） |
| 输出数量 | 1 张：`fx-crit.png` |

## Alpha 的产出方式

数字与速度线之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 数字白 | `#FFFFFF` |
| 数字描边黑 | `#1A1A1A` |
| 速度线金 | `#DCAE3A` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

画面中央偏右一行「15」，左上两道金色速度线：

- 白色粗体像素数字，笔画外一圈 **2 像素黑描边**（比普通伤害的 1 像素更厚，
  暴击更重）
- 数字高约占画布高度的 85%，略大、略斜向右上 5° 左右——「击飞感」
- 数字左上两道平行短斜线，金色，各约 8 × 2 像素
- 背景整片纯洋红

## AI 提示词

```
A pixel art critical hit damage number label for a retro game, 1024x1024, designed
to be downscaled to 32x32 pixel art.

Style: chunky bold pixel digits, flat shading, no perspective, no gradient, no
glow, no motion blur.

Content: the number "15" in large bold white pixel numerals, slightly tilted to
the upper right for an impact feel, centered slightly to the right. Every stroke
is wrapped in a thick two-pixel black outline. The digits fill about 85 percent
of the canvas height. To the upper left of the digits sit two short parallel
diagonal speed lines in gold, each about one quarter of the canvas width long,
pointing toward the digits.

Color palette strictly limited to: #FFFFFF, #1A1A1A, #DCAE3A.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No letters, no words, no exclamation marks, no stars, no explosions, no borders,
no gradient, no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
letters, words, exclamation marks, stars, explosions, borders, gradient, glow,
shadow, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 3 个色值
4. 核对描边厚度 2 像素、两道速度线平行等长
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 「15」可读、带 2 像素黑描边、整体向右上微倾
- [ ] 两道金色速度线平行，位于数字左上
- [ ] 颜色数 ≤ 3（不含透明）
- [ ] 与 `fx-damage-number.png` 同屏时暴击版明显更重、更醒目
