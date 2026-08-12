# I-21 钻石剑

## 用途

热键栏中的钻石剑物品图标。棕色木柄 + 青色钻石刀身。

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
| 木暗 | `#634C33` |
| 钻主 | `#4CC6C4` |
| 钻亮 | `#A8F2EF` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single diamond sword icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A small short sword with a wooden handle and a
bright cyan diamond blade, oriented diagonally from bottom-left to top-right
(handle at bottom-left, blade tip at top-right). Black 1-pixel outline.
Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #634C33, #4CC6C4, #A8F2EF, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体对角线放置
- 透明像素 ≥ 30%
