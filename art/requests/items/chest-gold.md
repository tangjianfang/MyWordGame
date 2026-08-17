# 金胸甲

## 用途

热键栏/背包/盔甲栏中的金胸甲图标。金系盔甲第二件（护躯干）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 背景 | 整片纯洋红 `#FF00FF`，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 金暗 | `#A8842E` |
| 金主 | `#E8C04A` |
| 金亮 | `#F7E08A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `chest-iron` 同一正视胸甲剪影，只把三阶铁色换成 m10 金系
- 金色与金剑/金镐同源（`#A8842E`/`#E8C04A`/`#F7E08A`）
- 正中竖向淡金亮脊线，腰部暗金收边，不镶宝石
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single golden chestplate icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A torso chest armor seen straight from
the front: a rounded trapezoid warm gold metal breastplate with two small
rounded gold shoulder pauldrons on the top left and top right corners, one
vertical pale gold highlight ridge down the center, and slightly darker gold
shading at the waist. The chestplate fills the frame. Black 1-pixel outline.
The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #A8842E, #E8C04A, #F7E08A, #1A1A1A outline only.

No text, no watermark, no arms, no hands, no head, no neck, no jewels, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, arms, hands, head, neck, mannequin, jewels, gems, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 与 `chest-iron` 并排剪影一致，颜色为金系
