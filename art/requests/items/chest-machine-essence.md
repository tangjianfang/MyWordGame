# 机元胸甲

## 用途

热键栏/背包/盔甲栏中的机元胸甲图标。机元系盔甲第二件（护躯干）。

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
| 机元暗 | `#5E4478` |
| 机元主 | `#7A5A9A` |
| 机元亮 | `#9C82BC` |
| 核心辉光 | `#B9A2D4` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `chest-iron` 同一正视胸甲剪影，材质换 m10 机元紫灰系
- 差异点：护胸正中一枚小圆发光核心（`#B9A2D4` 亮紫），呼应机元矿石的「能量核心」造型语义
- 腰部暗紫收边，不画外发光晕
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single machine essence chestplate icon for an inventory slot, 1024x1024
pixel art designed to be downscaled to 32x32. A torso chest armor seen
straight from the front: a rounded trapezoid purple-gray metal breastplate
with two small rounded metal shoulder pauldrons on the top left and top
right corners, one small round glowing violet core dot in the center of the
chest, and slightly darker purple shading at the waist. The chestplate fills
the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #5E4478, #7A5A9A, #9C82BC, #B9A2D4, #1A1A1A outline
only.

No text, no watermark, no arms, no hands, no head, no neck, no gradient, no
cast shadow, no large glow halo, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, arms, hands, head, neck, mannequin, gradient, drop shadow,
glow halo, lens flare, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 与 `chest-iron` 并排剪影一致，正中核心光点可见
