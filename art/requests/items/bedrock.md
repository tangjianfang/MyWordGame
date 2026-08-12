# I-14 基岩（物品图标）

## 用途

热键栏中的基岩物品图标。灰黑色方块带裂缝纹理，与方块面同色系。

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
| 基岩暗 | `#242424` |
| 基岩主 | `#3A3A3A` |
| 基岩亮 | `#4A4A4A` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A small bedrock block icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A dark gray cuboid block with a few irregular
crack lines on its surface, viewed straight-on. Black 1-pixel outline.
Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #242424, #3A3A3A, #4A4A4A, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
