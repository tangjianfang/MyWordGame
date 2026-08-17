# 铁胸甲

## 用途

热键栏/背包/盔甲栏中的铁胸甲图标。铁系盔甲第二件（护躯干）。

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
| 铁暗 | `#5F5F5F` |
| 铁主 | `#8A8A8A` |
| 铁亮 | `#C8C8C8` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视胸甲：圆肩两枚小护肩 + 上宽下窄的梯形护胸板，腰部一道略深收边
- 正中竖向亮脊线一片，铁色与铁剑/铁镐同 `#8A8A8A` 系
- 四材料胸甲共用同一剪影，只换材质三阶色

## AI 提示词

```
A single iron chestplate icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A torso chest armor seen straight from
the front: a rounded trapezoid silver-gray metal breastplate with two small
rounded shoulder pauldrons on the top left and top right corners, one
vertical light highlight ridge down the center, and slightly darker shading
at the waist. The chestplate fills the frame. Black 1-pixel outline. The
empty corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #5F5F5F, #8A8A8A, #C8C8C8, #2A2620, #1A1A1A only.

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
- 两枚护肩与正中亮脊线可见，无人形部件
