# I-41 线

## 用途

热键栏中的线物品图标（物品 id 为 `string_`，texture 为 `string`）。盘绕的白色线团。

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
| 线暗 | `#A0A0A0` |
| 线主 | `#E0E0E0` |
| 线亮 | `#FFFFFF` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single ball of string icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. A small round ball of white string with
a few loose thread ends sticking out, viewed from a slight 3/4 angle. Black
1-pixel outline. Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #A0A0A0, #E0E0E0, #FFFFFF, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
