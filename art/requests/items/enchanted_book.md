# I-38 附魔书

## 用途

热键栏中的附魔书物品图标。紫色封面 + 紫色光晕粒子。

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
| 皮暗 | `#3A2A5A` |
| 皮主 | `#5A4A8A` |
| 光晕 | `#A88AD2` |
| 纸页 | `#F2EAD2` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single enchanted book icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. A small closed book viewed from a slight
3/4 angle above, showing a purple leather cover with a glowing purple
gemstone clasp and a few small purple sparkle particles floating around it.
Black 1-pixel outline. Transparent background (pure magenta #FF00FF keyout
color).

Color palette strictly: #3A2A5A, #5A4A8A, #A88AD2, #F2EAD2, #1A1A1A only.

No text, no shadow on background, no glow blob. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
