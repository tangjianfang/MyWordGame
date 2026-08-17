# 盾牌

## 用途

热键栏/背包中的盾牌物品图标。铁框木面圆顶盾。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 铁框暗 | `#8A8A8A` |
| 铁框亮 | `#C8C8C8` |
| 盾面木暗 | `#6E5232` |
| 盾面木主 | `#9C7549` |
| 盾面木亮 | `#B98D57` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视圆顶盾（heater shield）：铁色包边 + 木板盾面 + 中央铁铆钉
- 盾面两条横向板缝，与 planks 木板黄同系
- 铁色与铁剑/铁镐同 `#8A8A8A` 系，不画纹章图案

## AI 提示词

```
A single shield icon for an inventory slot, 1024x1024 pixel art designed to
be downscaled to 32x32. A round-topped heater shield seen straight from the
front: a silver iron border frame around a warm wooden plank face with two
horizontal plank seam lines, and one small silver boss rivet in the center of
the face. The shield fills the frame, slightly taller than wide. Black
1-pixel outline. The empty corners are one flat solid dark backdrop color
#2A2620.

Color palette strictly: #6E5232, #9C7549, #B98D57, #8A8A8A, #C8C8C8, #2A2620,
#1A1A1A only.

No text, no watermark, no heraldry emblem, no crest, no stripes, no gradient,
no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, heraldry, emblem, cross symbol, lion, stripes, flag,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 7
- 铁边完整环包盾面，中央铆钉可见
