# I-11 甜菜根

## 用途

热键栏中的甜菜根物品图标。紫红色球根 + 顶部绿色嫩叶。

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
| 根暗 | `#7A1A3A` |
| 根主 | `#A82A52` |
| 叶绿 | `#5D9C3C` |
| 叶亮 | `#74B84E` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single beetroot icon for an inventory slot, 512x512 pixel art designed to
be downscaled to 16x16. A small purplish-red bulb root with a tuft of 2-3
small green leaves growing from the top. Black 1-pixel outline. Transparent
background (pure magenta #FF00FF keyout color).

Color palette strictly: #7A1A3A, #A82A52, #5D9C3C, #74B84E, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
