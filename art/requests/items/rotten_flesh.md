# I-10 腐肉

## 用途

热键栏中的腐肉物品图标。绿褐色发臭肉块。

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
| 肉暗 | `#4A5A2A` |
| 肉主 | `#6E8E3A` |
| 腐亮 | `#9AB05A` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single rotten flesh icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A small irregular slab of greenish-brown rotting
meat with a couple of darker mold spots. Black 1-pixel outline. Transparent
background (pure magenta #FF00FF keyout color).

Color palette strictly: #4A5A2A, #6E8E3A, #9AB05A, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
