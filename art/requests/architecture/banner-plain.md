# ARCH-10 素色旗帜方块

## 用途

村庄庆典/大门装饰用的**素色旗帜布面**贴图（横幅方块的正面纹理）。
纯布面无图案，是 `banner-crest` 的母版：程序换色（`generate_art.py --variants`）
可以基于它量产不同村庄的染色旗帜。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无 |
| 颜色数 | 3 – 4 色（刻意少，方便程序换色） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 布面主色 | `#A05242` |
| 织纹/褶暗 | `#8B4433` |
| 褶亮 | `#B4635A` |

## 视觉描述

一整幅垂挂的暖红布：

- 细微的编织格纹：每几像素交错的深色针脚点，像布的织孔
- 几条**贯通上下**的宽幅竖向褶带，明暗交替，表现布挂着自然下垂的褶
- 织纹与褶带都延伸到边缘并在接缝处对齐——这是无缝的关键
- **没有任何徽记、图案、流苏、旗杆**——素布就是素布
- 颜色刻意压到 3-4 个：这张是换色母版，色越少换色越干净

## AI 提示词

```
A seamless tileable pixel art block texture of plain dyed cloth banner,
flat front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a single sheet of warm red cloth. A subtle woven checker pattern
of tiny darker red stitch dots alternating every few pixels, plus broad
vertical fold bands alternating slightly darker and lighter to suggest
cloth hanging naturally. No emblem and no pattern other than the weave.
The weave dots and fold bands run edge to edge and line up across tile
borders.

Palette strictly: base #A05242, weave and folds #8B4433, highlights
#B4635A.

CRITICAL: tiles seamlessly on all four edges, fold bands line up across
tile borders.

No border, no frame, no emblem, no symbol, no text, no letters, no
tassels, no fringe, no flag pole, no watermark, no gradient, no glow, no
3D render, no anti-aliasing, no dark background.
```

## 负面提示词

```
emblem, symbol, crest, pattern, border, frame, tassels, fringe, pole,
text, letters, watermark, gradient, glow, 3D render, anti-aliasing,
perspective, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 3 个色值（多一个都不留——它是换色母版）
3. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝
- [ ] 不透明像素颜色数 ≤ 4
- [ ] 褶带方向竖直（布是挂着的），不出现横褶
- [ ] 画面里没有任何徽记/图案
- [ ] 4×4 平铺预览不出现规律大格子
