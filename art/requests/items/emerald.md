# I-36 绿宝石

## 用途

热键栏与合成界面中的绿宝石图标。绿色宝石形状。

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
| 宝石暗 | `#1A7C3A` |
| 宝石主 | `#4CC66A` |
| 宝石亮 | `#A8F2B6` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single emerald gem icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. Centered, with a 1-pixel black outline, classic
beveled gem shape (rectangular cut with 3 visible facets) in vivid green.
Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #1A7C3A, #4CC66A, #A8F2B6, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
