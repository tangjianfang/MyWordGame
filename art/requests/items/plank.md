# I-01 木板

## 用途

热键栏与合成界面中的木板物品图标。与 B-11 方块面同色系（木板黄）。

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
| 板缝 | `#6B4E2E` |
| 板主 | `#8A6741` |
| 板亮 | `#9C7549` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single wooden plank icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A flat rectangular plank viewed from above, with
2-3 horizontal wood grain lines and a slightly darker seam at the top and
bottom edges. Black 1-pixel outline. Transparent background (pure magenta
#FF00FF keyout color).

Color palette strictly: #6B4E2E, #8A6741, #9C7549, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
