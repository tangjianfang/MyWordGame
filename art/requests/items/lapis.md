# I-39 青金石

## 用途

热键栏与合成界面中的青金石图标。深蓝色宝石，带金色斑点。

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
| 宝石暗 | `#1A3A7C` |
| 宝石主 | `#3A5AC4` |
| 宝石亮 | `#6A8AE8` |
| 金斑 | `#DCAE3A` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single lapis lazuli gem icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. Centered, with a 1-pixel black outline,
a faceted deep blue gem with a few small gold-colored specks scattered on its
surface. Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #1A3A7C, #3A5AC4, #6A8AE8, #DCAE3A, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
