# I-40 红石

## 用途

热键栏与合成界面中的红石粉物品图标（不是方块面）。红色粉粒散落感。

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
| 粉暗 | `#A02020` |
| 粉主 | `#D03030` |
| 粉亮 | `#E84040` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A small pile of redstone dust icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. A small heap of red powder, viewed from
above, with 5-8 small red particles clumped together. Black 1-pixel outline
around the heap silhouette. Transparent background (pure magenta #FF00FF
keyout color).

Color palette strictly: #A02020, #D03030, #E84040, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
