# 玫瑰（flower-rose）

## 用途

花草方块（十字面片渲染）。森林群系的高档红花，任务链/装饰玩法用。
背景洋红键控为透明。

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
| 花瓣暗红 | `#8E2420` |
| 花瓣主红 | `#A83228` |
| 花瓣亮红 | `#C24434` |
| 茎绿 | `#3F7A2E` |
| 叶绿 | `#52993B` |
| 刺/叶阴影 | `#2F5D24` |
| 键控色 | `#FF00FF`（后处理删除） |

红系是 B < G 的深血红，比虞美人更深更暗——两种红花靠明度区分。

## 视觉描述

- 单株：1 像素绿茎从底边到约 40% 高度，茎上两侧各 2 个 1 像素小刺
  （`#2F5D24`）与 2–3 片圆齿复叶
- 茎顶一朵**重瓣玫瑰**：12–14 像素的近圆形花头，三层同心花瓣
  （外层暗红大瓣、中层主红、心部亮红螺旋），瓣缘有 1 像素暗线分隔
- 总高约 70%，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a single deep red rose on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one rose plant centered. A thin 1-pixel green stem rises from the
bottom edge to about 40 percent height, with two tiny 1-pixel thorns and two
or three rounded leaflets. On top sits one double rose bloom 12 to 14 final
pixels across, nearly round: three concentric layers of petals (dark red outer
petals, main red middle layer, bright red spiral center), with a 1-pixel dark
line between petal edges. Total plant height is about 70 percent of the
canvas. Keep at least 2 final pixels of magenta on the left, right and top
sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #8E2420, #A83228, #C24434 for petals, #3F7A2E and
#52993B for stem and leaves, #2F5D24 for thorns and leaf shading. Deep blood
red, never cool pink, never magenta tint.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no vase, no bouquet,
no multiple roses, no fallen petals, no text, no watermark, no gradient, no
blur, no 3D render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, vase, bouquet, multiple
roses, fallen petals, dew drops, text, watermark, blur, 3D render, perspective,
anti-aliasing, cool pink, magenta petals
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 6 色
3. 统计透明面积 45% – 65%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 单株居中、总高约 70%，重瓣深红花头
- [ ] 与 `flower-poppy.png` 并排，本种更深更暗、花头更大更圆
- [ ] 透明占比 45% – 65%，无残留洋红与紫边
