# I-04 铁锭

## 用途

热键栏与合成界面中的金属锭图标。银色/亮灰色块，带斜面高光。

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
| 铁暗 | `#5C5C5C` |
| 铁主 | `#8A8A8A` |
| 铁亮 | `#C8C8C8` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single iron ingot icon for an inventory slot, 512x512 pixel art designed to
be downscaled to 16x16. A small flat metal bar (rectangular cuboid) viewed
from a slight 3/4 angle, with a beveled top surface showing one bright highlight
stripe. Black 1-pixel outline. Transparent background (pure magenta #FF00FF
keyout color).

Color palette strictly: #5C5C5C, #8A8A8A, #C8C8C8, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
