# I-15 甜菜汤

## 用途

热键栏中的甜菜汤物品图标。木碗 + 紫红色汤。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 16 × 16 像素 |
| 生成尺寸 | 512 × 512（再降采样） |
| 平铺 | 否 |
| Alpha | **有**（仅 0 / 255） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 木暗 | `#634C33` |
| 木主 | `#7A6042` |
| 汤暗 | `#7A1A3A` |
| 汤主 | `#A82A52` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A beet soup bowl icon for an inventory slot, 512x512 pixel art designed to
be downscaled to 16x16. A small wooden bowl viewed from a slight 3/4 angle
above, filled with deep purplish-red soup showing a tiny white steam wisp
rising. Black 1-pixel outline. Transparent background (pure magenta #FF00FF
keyout color).

Color palette strictly: #634C33, #7A6042, #7A1A3A, #A82A52, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
