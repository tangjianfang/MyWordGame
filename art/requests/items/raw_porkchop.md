# I-08 生猪排

## 用途

热键栏中的生猪排物品图标。粉红色肉块带白色脂肪纹理。

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
| 肉暗 | `#C88A8A` |
| 肉主 | `#F2B0B0` |
| 脂肪 | `#FFE8E8` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single raw porkchop icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A small irregular slab of raw pink meat with a
couple of white fat marbling streaks. Black 1-pixel outline. Transparent
background (pure magenta #FF00FF keyout color).

Color palette strictly: #C88A8A, #F2B0B0, #FFE8E8, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
