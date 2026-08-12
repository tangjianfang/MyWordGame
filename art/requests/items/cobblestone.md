# I-03-2 圆石（物品图标）

## 用途

热键栏中的圆石物品图标。灰色石块堆叠感，与 B-08 方块面同色系。

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
| 石暗 | `#4A4A4A` |
| 石主 | `#7E7E7E` |
| 石亮 | `#A3A3A3` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A small cobblestone block icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. A small cuboid of rough gray stones,
viewed straight-on, with several visible mortar seams between irregular
stones. Black 1-pixel outline. Transparent background (pure magenta #FF00FF
keyout color).

Color palette strictly: #4A4A4A, #7E7E7E, #A3A3A3, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
