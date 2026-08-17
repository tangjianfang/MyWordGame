# 宝石袋

## 用途

热键栏/背包中的宝石袋图标（宝物，收集类奖励包）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 袋暗 | `#6E5232` |
| 袋主 | `#8A6741` |
| 袋亮 | `#B98D57` |
| 宝石青 | `#4CC6C4` |
| 宝石红 | `#D63838` |
| 宝石绿 | `#5D9C3C` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视束口袋：袋口收紧打结、鼓肚下垂，顶部开缝露出三枚宝石（青/红/绿各一）
- 袋身用家具暖木色系，受光面一道亮阶
- 宝石色取钻石青/主红/草绿，均为小菱形块

## AI 提示词

```
A single gem bag icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A small loot pouch seen straight from the front:
a warm brown drawstring bag gathered and tied at the top with a thin cord,
bulging round at the bottom, with three small faceted gems sticking out of
the open top — one cyan, one red, and one green diamond shape. One lighter
highlight patch on the front of the bag. The bag fills the frame. Black
1-pixel outline. The empty corners are one flat solid dark backdrop color
#2A2620.

Color palette strictly: #6E5232, #8A6741, #B98D57, #4CC6C4, #D63838,
#5D9C3C, #2A2620, #1A1A1A only.

No text, no watermark, no coins, no sparkles, no glow, no hand, no
gradient, no cast shadow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, coins, sparkles, gold coins, hand, treasure chest, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing, extra objects
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 8
- 束口结与三色宝石可见，袋形鼓垂
