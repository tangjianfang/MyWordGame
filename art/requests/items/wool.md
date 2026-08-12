# I-09 羊毛

## 用途

热键栏中的羊毛物品图标。白色蓬松毛线方块。

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
| 毛暗 | `#C8C8C8` |
| 毛主 | `#F2F2F2` |
| 毛亮 | `#FFFFFF` |
| 描边 | `#5C5C5C` |

## AI 提示词

```
A single wool block icon for an inventory slot, 512x512 pixel art designed to
be downscaled to 16x16. A small white fluffy wool block viewed straight-on,
with a subtle crisscross fiber pattern visible on the surface. Dark gray
1-pixel outline. Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #C8C8C8, #F2F2F2, #FFFFFF, #5C5C5C only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
