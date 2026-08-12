# I-43 骨头

## 用途

热键栏中的骨头物品图标。白色骨头对角放置，斜向 45°。

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
| 骨暗 | `#C8C8B8` |
| 骨主 | `#EAEAD8` |
| 骨亮 | `#F8F8E8` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single bone icon for an inventory slot, 512x512 pixel art designed to be
downscaled to 16x16. A small dog-bone shape (two knobs at the ends and a
thinner middle) in off-white, oriented diagonally from bottom-left to
top-right. Black 1-pixel outline. Transparent background (pure magenta
#FF00FF keyout color).

Color palette strictly: #C8C8B8, #EAEAD8, #F8F8E8, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体对角线放置
- 透明像素 ≥ 30%
