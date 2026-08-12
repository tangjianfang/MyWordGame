# I-18 木剑

## 用途

热键栏中的木剑物品图标。短木柄 + 木质刀身，斜向 45°。

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
| 木主 | `#7A6042` |
| 刃亮 | `#B98D57` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single wooden sword icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A small short sword with a wooden handle and a
flat tan blade, oriented diagonally from bottom-left to top-right (handle at
bottom-left, blade tip at top-right). The blade is about 2 pixels wide and
8 pixels long, the handle is darker brown. Black 1-pixel outline. Transparent
background (pure magenta #FF00FF keyout color).

Color palette strictly: #634C33, #7A6042, #B98D57, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体对角线放置
- 透明像素 ≥ 30%
