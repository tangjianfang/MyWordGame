# I-46 红石粉（物品图标）

## 用途

热键栏中的红石粉物品图标。注意：物品 JSON 的 `texture` 字段是 `redstone_dust`
（与方块 B-21 同名但走物品路径）。红石粉颗粒散落堆叠。

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
| 粉暗 | `#A02020` |
| 粉主 | `#D03030` |
| 粉亮 | `#E84040` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A small redstone dust pile icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. A small mound of red powder viewed from
a slight 3/4 angle, with 6-10 small bright red particles in a cluster. Black
1-pixel outline around the pile silhouette. Transparent background (pure
magenta #FF00FF keyout color).

Color palette strictly: #A02020, #D03030, #E84040, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
