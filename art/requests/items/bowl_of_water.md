# I-17 水碗

## 用途

热键栏中的水碗物品图标。木碗 + 蓝色水面。

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
| 水主 | `#3A6FB5` |
| 水亮 | `#4E88CE` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A water bowl icon for an inventory slot, 512x512 pixel art designed to be
downscaled to 16x16. A small wooden bowl viewed from a slight 3/4 angle
above, filled with light blue water showing a tiny white highlight on the
surface. Black 1-pixel outline. Transparent background (pure magenta #FF00FF
keyout color).

Color palette strictly: #634C33, #7A6042, #3A6FB5, #4E88CE, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
