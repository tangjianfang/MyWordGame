# I-03 煤炭

## 用途

热键栏与合成界面中的煤炭图标。黑色不规则煤块，带微弱高光。

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
| 煤暗 | `#0F0F0F` |
| 煤主 | `#1F1F1F` |
| 煤亮 | `#3A3A3A` |
| 描边 | `#0A0A0A` |

## AI 提示词

```
A single coal lump icon for an inventory slot, 512x512 pixel art designed to
be downscaled to 16x16. A small irregular chunk of black coal with slightly
faceted edges, one subtle highlight on the top edge. Black 1-pixel outline.
Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #0F0F0F, #1F1F1F, #3A3A3A, #0A0A0A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
