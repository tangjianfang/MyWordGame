# 机元护腿

## 用途

热键栏/背包/盔甲栏中的机元护腿图标。机元系盔甲第三件（护腿）。

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
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `legs-iron` 同一正视护腿剪影，材质换 m10 机元紫灰系
- 紫灰与机元剑/镐同源（`#5E4478`/`#7A5A9A`/`#9C82BC`）
- 腰带板暗阶、护胫主阶、膝甲带暗阶，不加发光件（发光留给头盔/胸甲）

## AI 提示词

```
A single machine essence leggings icon for an inventory slot, 1024x1024
pixel art designed to be downscaled to 32x32. A pair of leg armor seen
straight from the front: one wide horizontal purple-gray metal waistband
across the top, and two vertical rectangular metal leg guards hanging down
side by side with a narrow gap between them; each leg guard has a slightly
darker purple knee band across the middle. The leggings fill the frame,
taller than wide. Black 1-pixel outline. The empty corners are one flat
solid dark backdrop color #2A2620.

Color palette strictly: #5E4478, #7A5A9A, #9C82BC, #2A2620, #1A1A1A only.

No text, no watermark, no feet, no boots, no belt buckle, no gradient, no
cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, feet, boots, shoes, belt buckle, legs of a person, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 与 `legs-iron` 并排剪影一致，颜色为紫灰系
