# I-02 木棍

## 用途

热键栏与合成界面中的木棍物品图标。细长棕色木条。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 16 × 16 像素 |
| 生成尺寸 | 512 × 512（再降采样） |
| 平铺 | 否 |
| Alpha | **有**（仅 0 / 255） |
| 透明占比 | 50%–60%（木棍细长） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 木暗 | `#634C33` |
| 木主 | `#7A6042` |
| 木亮 | `#8E7350` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single wooden stick icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. A thin vertical (or slightly diagonal) wooden rod,
about 2 pixels wide and 12 pixels tall in the final size, with a small knot
near the center. Black 1-pixel outline. Transparent background (pure magenta
#FF00FF keyout color).

Color palette strictly: #634C33, #7A6042, #8E7350, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体细长居中
- 透明像素 50%–60%
