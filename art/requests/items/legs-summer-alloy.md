# 夏季合金护腿

## 用途

热键栏/背包/盔甲栏中的夏季合金护腿图标。合金系盔甲第三件（护腿）。

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
| 合金暗 | `#2A5F75` |
| 合金主 | `#4A9AB8` |
| 合金亮 | `#78C2DC` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `legs-iron` 同一正视护腿剪影，只把三阶铁色换成 m10 合金青蓝系
- 青蓝与夏季合金剑/镐同源（`#2A5F75`/`#4A9AB8`/`#78C2DC`）
- 腰带板暗阶、护胫主阶、膝甲带暗阶
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single summer alloy leggings icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A pair of leg armor seen straight
from the front: one wide horizontal cyan-blue alloy waistband across the
top, and two vertical rectangular alloy leg guards hanging down side by side
with a narrow gap between them; each leg guard has a slightly darker blue
knee band across the middle. The leggings fill the frame, taller than wide.
Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #2A5F75, #4A9AB8, #78C2DC, #1A1A1A outline only.

No text, no watermark, no feet, no boots, no belt buckle, no gradient, no
cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, feet, boots, shoes, belt buckle, legs of a person, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 与 `legs-iron` 并排剪影一致，颜色为青蓝系
