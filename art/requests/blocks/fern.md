# 蕨（fern）

## 用途

花草方块（十字面片渲染）。森林/丛林阴湿处的蕨类，与草丛区分点是
**羽状复叶**（有中脉和对生小叶），不是简单草叶。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，株高约 75% – 90% |
| 透明面积 | **40% – 60%** |
| 颜色数 | 4 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 叶阴影 | `#2F5D24` |
| 叶主色 | `#3F7A2E` |
| 叶亮部 | `#4E9238` |
| 新叶高光 | `#66AC4A` |
| 键控色 | `#FF00FF`（后处理删除） |

用 `leaves` 的叶绿系（深冷绿）而非草绿——蕨长在树荫下，与树叶同系
比与草地同系更协调。

## 视觉描述

- **3 支羽状蕨叶**从底部中心呈扇形展开：中间一支近直立（高 85%–90%），
  两侧各一支向外弯垂（高 70%–80%）
- 每支蕨叶有 1 像素中脉，沿中脉两侧对生 6–9 对 1–2 像素小羽片，
  越靠尖越小
- 中脉深绿、羽片主绿、受光面亮绿、卷头新芽高光
- 丛底触底边，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a fern plant on a solid background, 1024x1024, designed
to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: three feathery fronds fanning out from the bottom center: the middle
frond nearly upright reaching 85 to 90 percent of the canvas height, two side
fronds arching outward at 70 to 80 percent. Each frond has a 1-pixel dark
green midrib with six to nine pairs of tiny 1 or 2 pixel leaflets on opposite
sides, shrinking toward the tip, and the youngest curled tip is the brightest
green. The fronds may touch the bottom edge; keep at least 2 final pixels of
magenta on the left and right sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the fern. No partial
transparency.

Palette strictly limited to: #2F5D24, #3F7A2E, #4E9238, #66AC4A. Deep cool
green, darker than grass.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no forest floor, no
spore dots, no flowers, no grass blades, no text, no watermark, no gradient,
no blur, no 3D render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, forest floor, mushroom,
spores, flowers, plain grass blades, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 4 色
3. 统计透明面积 40% – 60%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 3 支羽状蕨叶、有中脉与对生小羽片（不是光杆草叶）
- [ ] 绿色比 `tall-grass.png` 更深更冷
- [ ] 透明占比 40% – 60%，无残留洋红与紫边
