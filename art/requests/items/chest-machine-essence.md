# 机元胸甲

## 用途

热键栏/背包/盔甲栏中的机元胸甲图标。机元系盔甲第二件（护躯干）。

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
| 机元暗 | `#5E4478` |
| 机元主 | `#7A5A9A` |
| 机元亮 | `#9C82BC` |
| 核心辉光 | `#B9A2D4` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `chest-iron` 同一正视胸甲剪影，材质换 m10 机元紫灰系
- 差异点：护胸正中一枚小圆发光核心（`#B9A2D4` 亮紫），呼应机元矿石的「能量核心」造型语义
- 腰部暗紫收边，不画外发光晕

## AI 提示词

```
A single machine essence chestplate icon for an inventory slot, 1024x1024
pixel art designed to be downscaled to 32x32. A torso chest armor seen
straight from the front: a rounded trapezoid purple-gray metal breastplate
with two small rounded metal shoulder pauldrons on the top left and top
right corners, one small round glowing violet core dot in the center of the
chest, and slightly darker purple shading at the waist. The chestplate fills
the frame. Black 1-pixel outline. The empty corners are one flat solid dark
backdrop color #2A2620.

Color palette strictly: #5E4478, #7A5A9A, #9C82BC, #B9A2D4, #2A2620, #1A1A1A
only.

No text, no watermark, no arms, no hands, no head, no neck, no gradient, no
cast shadow, no large glow halo, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, arms, hands, head, neck, mannequin, gradient, drop shadow,
glow halo, lens flare, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 与 `chest-iron` 并排剪影一致，正中核心光点可见
