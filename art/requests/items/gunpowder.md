# I-42 火药

## 用途

热键栏中的火药物品图标。黑色粉堆 + 微弱灰色高光。

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
| 粉暗 | `#1A1A1A` |
| 粉主 | `#3A3A3A` |
| 粉亮 | `#5C5C5C` |
| 描边 | `#0F0F0F` |

## AI 提示词

```
A single gunpowder pile icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. A small mound of dark gray-black powder
viewed from a slight 3/4 angle above, with a tiny lighter highlight on the
top to suggest fine grain texture. Black 1-pixel outline around the pile
silhouette. Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #1A1A1A, #3A3A3A, #5C5C5C, #0F0F0F only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
