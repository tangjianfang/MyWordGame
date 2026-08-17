# 铁护腿

## 用途

热键栏/背包/盔甲栏中的铁护腿图标。铁系盔甲第三件（护腿）。

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

- 正视护腿：顶部宽腰带板 + 左右两条竖直护胫板，中间一道窄缝
- 每条护胫中部一道略深膝甲横带，铁色与铁剑/铁镐同 `#8A8A8A` 系
- 四材料护腿共用同一剪影，只换材质三阶色

## AI 提示词

```
A single iron leggings icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A pair of leg armor seen straight from
the front: one wide horizontal silver-gray metal waistband across the top,
and two vertical rectangular metal leg guards hanging down side by side with
a narrow gap between them; each leg guard has a slightly darker knee band
across the middle. The leggings fill the frame, taller than wide. Black
1-pixel outline. The empty corners are one flat solid dark backdrop color
#2A2620.

Color palette strictly: #5F5F5F, #8A8A8A, #C8C8C8, #2A2620, #1A1A1A only.

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
- 两条护胫中间留缝，顶部腰带板完整
