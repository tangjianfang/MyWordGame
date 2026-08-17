# 蓝兰花（flower-orchid）

## 用途

花草方块（十字面片渲染）。湿地/河边群系的花（投放密度由 `flowers.json`
配置）。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，总高约 70% |
| 透明面积 | **45% – 65%** |
| 颜色数 | 6 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 花瓣暗蓝 | `#5A78C8` |
| 花瓣主蓝 | `#7B93DC` |
| 花瓣亮蓝 | `#9CB4EC` |
| 唇瓣米白 | `#F0E8C8` |
| 茎绿 | `#3F7A2E` |
| 叶绿 | `#52993B` |
| 键控色 | `#FF00FF`（后处理删除） |

蓝三色全部 R < G < B（真蓝而非紫），冷蓝在去洋红边判据下安全。

## 视觉描述

- 单株：一根 1 像素花茎从底边弯到约 45% 高度，茎上 2 片**宽剑形叶**从底部
  向上张开（每片 3 像素宽、10–12 像素长）
- 茎顶开花序：**2 朵蓝兰花**上下错开，每朵约 7–8 像素：三枚上瓣蓝
  （暗/主/亮三层）+ 中央 2×3 像素米白唇瓣
- 总高约 70%，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single blue orchid plant on a solid background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one orchid plant centered. Two broad sword-shaped leaves, about 3
final pixels wide, rise from the bottom and fan outward. A thin 1-pixel stem
curves up to about 45 percent height and carries two blue orchid blooms offset
from each other, each bloom 7 or 8 final pixels: three upper petals in three
layers of blue plus a small 2x3 cream lip petal in the center. Total plant
height is about 70 percent of the canvas. Keep at least 2 final pixels of
magenta on the left, right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #5A78C8, #7B93DC, #9CB4EC for petals, #F0E8C8 for
the lip, #3F7A2E and #52993B for stem and leaves. True blue, never purple,
never magenta tint.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no water, no pond, no
pot, no multiple stems, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, water, pond, pot, vase,
purple, violet, magenta tint, multiple stems, bouquet, text, watermark, blur,
3D render, perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 6 色
3. 统计透明面积 45% – 65%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 70%，蓝花白唇
- [ ] 蓝为真蓝（所有花瓣像素 R < G），不发紫
- [ ] 透明占比 45% – 65%，无残留洋红与紫边
