# 草丛（tall-grass）

## 用途

花草方块（十字面片渲染）。草原/森林地表最常见的「无花植被」，让地面
不至于只有平整草方块。背景洋红键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 主体占比 | 居中，丛高约 70% – 85% |
| 透明面积 | **35% – 55%** |
| 颜色数 | 4 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 叶阴影 | `#3C6626` |
| 叶主色 | `#4A7E2F` |
| 叶亮部 | `#5D9C3C` |
| 叶尖高光 | `#74B84E` |
| 键控色 | `#FF00FF`（后处理删除） |

即 `grass-top` 的草绿四色——草丛长在草方块上，色系必须同源，
只是多了竖向叶片形状。

## 视觉描述

- 一丛 **6–8 根草叶**从底部散开：根部聚拢在中央 6–8 像素宽，叶尖向
  左右两侧弯垂
- 每根叶 1 像素宽，根部用深绿、中段主绿、叶尖亮绿/高光
- 叶尖高低错落（70%–85% 高度不等），不要剪平头
- 丛底触底边，左右各留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a tuft of tall grass on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one clump of six to eight grass blades sprouting from the bottom
center and arching outward to the left and right. Each blade is 1 final pixel
wide, dark green at the root, main green in the middle, bright green at the
tip. Blade tips end at different heights between 70 and 85 percent of the
canvas height, never cut flat. The clump may touch the bottom edge; keep at
least 2 final pixels of magenta on the left and right sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the grass. No partial
transparency.

Palette strictly limited to: #3C6626, #4A7E2F, #5D9C3C, #74B84E. The same
grass green family as a grass block top.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no flowers, no seed
heads, no wheat ears, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, flowers, seed heads,
wheat, ears, cattail, text, watermark, blur, 3D render, perspective,
anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 4 色
3. 统计透明面积 35% – 55%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 一丛 6–8 根弯垂草叶，叶尖高低错落
- [ ] 与 `grass-top.png` 并排，绿色逐色一致
- [ ] 透明占比 35% – 55%，无残留洋红与紫边
