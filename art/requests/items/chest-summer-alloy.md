# 夏季合金胸甲

## 用途

热键栏/背包/盔甲栏中的夏季合金胸甲图标。合金系盔甲第二件（护躯干）。

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
| 合金暗 | `#2A5F75` |
| 合金主 | `#4A9AB8` |
| 合金亮 | `#78C2DC` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `chest-iron` 同一正视胸甲剪影，只把三阶铁色换成 m10 合金青蓝系
- 青蓝与夏季合金剑/镐同源（`#2A5F75`/`#4A9AB8`/`#78C2DC`）
- 正中竖向亮青脊线，腰部暗青收边

## AI 提示词

```
A single summer alloy chestplate icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A torso chest armor seen straight
from the front: a rounded trapezoid cyan-blue alloy metal breastplate with
two small rounded alloy shoulder pauldrons on the top left and top right
corners, one vertical pale cyan highlight ridge down the center, and
slightly darker blue shading at the waist. The chestplate fills the frame.
Black 1-pixel outline. The empty corners are one flat solid dark backdrop
color #2A2620.

Color palette strictly: #2A5F75, #4A9AB8, #78C2DC, #2A2620, #1A1A1A only.

No text, no watermark, no arms, no hands, no head, no neck, no gradient, no
cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, arms, hands, head, neck, mannequin, gradient, drop shadow,
glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 与 `chest-iron` 并排剪影一致，颜色为青蓝系
