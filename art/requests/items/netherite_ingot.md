# I-06 下界合金锭

## 用途

热键栏与合成界面中的下界合金锭图标。深灰近黑色金属，带暗紫色高光。

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
| 合金暗 | `#1A1A22` |
| 合金主 | `#3A2A3A` |
| 合金亮 | `#5A4A6A` |
| 描边 | `#0F0F12` |

## AI 提示词

```
A single netherite ingot icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A small flat dark metal bar (rectangular cuboid)
viewed from a slight 3/4 angle, with a very subtle purple-tinted bevel. Black
1-pixel outline. Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #1A1A22, #3A2A3A, #5A4A6A, #0F0F12 only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
