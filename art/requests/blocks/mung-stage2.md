# 绿豆 · 成熟期（mung-stage2）

## 用途

绿豆作物 3 阶段的**第 2 阶**（成熟可收割）。满株豆荚是「能收」的信号，
收割掉落绿豆（第 1 波农业赛道）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **40% – 55%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 藤阴影 | `#4E7A2E` |
| 藤主色 | `#63922F` |
| 叶亮部 | `#78A838` |
| 叶高光 | `#8FC24A` |
| 豆荚黄绿 | `#AFC85E` |
| 键控色 | `#FF00FF`（后处理删除） |

豆荚 `#AFC85E` 比叶色更浅更黄，挂在藤上要一眼挑得出来。

## 视觉描述

- 3–4 根藤攀到约 90%（约 28 像素），整株茂密
- 沿藤 8–10 片心形叶
- 挂 **4–5 支豆荚**：每支 4–5 像素长、2 像素宽的细长荚，微弯下垂，
  颜色 `#AFC85E`，荚身有 1 像素暗缝线
- 藤底触底边，左右留 ≥2 像素洋红边，藤顶不触顶边

## AI 提示词

```
A pixel art sprite of a ripe mung bean plant on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: ripe mung beans: three or four vines climbing to about 90 percent of
the canvas height, densely covered with eight to ten small heart-shaped leaves.
Four or five slender bean pods hang from the vines, each 4 or 5 final pixels
long and 2 pixels wide, slightly curved and drooping, in a paler yellow-green
#AFC85E with a 1-pixel darker seam line down the pod. The vines may touch the
bottom edge; keep at least 2 final pixels of magenta on the left and right
sides, and the top never touches the top edge.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #4E7A2E, #63922F, #78A838, #8FC24A for vines and
leaves, #AFC85E for the pods.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
pole, no trellis, no flowers, no text, no watermark, no gradient, no blur, no
3D render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, pole, trellis,
stake, flowers, text, watermark, blur, 3D render, perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计透明面积 40% – 55%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 满株 + 4–5 支浅黄绿豆荚，荚与叶色差明显
- [ ] 透明占比 40% – 55%
- [ ] 无残留洋红与紫边
- [ ] 与 `mung-stage1.png` 并排明显更密更高
