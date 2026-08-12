# I-37 书

## 用途

热键栏中的书物品图标。棕色皮质封面 + 侧面页边。

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
| 皮暗 | `#634C33` |
| 皮主 | `#8A6741` |
| 纸页 | `#F2EAD2` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single book icon for an inventory slot, 512x512 pixel art designed to be
downscaled to 16x16. A small closed book viewed from a slight 3/4 angle
above, showing a brown leather cover with a small gold-tone clasp in the
center and pale paper pages visible on the right edge. Black 1-pixel outline.
Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #634C33, #8A6741, #F2EAD2, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
