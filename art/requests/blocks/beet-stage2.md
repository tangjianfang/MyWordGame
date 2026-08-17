# 甜菜 · 成熟期（beet-stage2）

## 用途

甜菜作物 3 阶段的**第 2 阶**（成熟可收割）。叶簇丰满 + 根冠露头是「能收」
的信号，收割掉落甜菜（第 1 波农业赛道）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **40% – 55%** |
| 颜色数 | 6 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 叶阴影 | `#3C6626` |
| 叶主色 | `#4A7E2F` |
| 叶亮部 | `#5D9C3C` |
| 叶高光 | `#74B84E` |
| 根冠暗红 | `#8E2826` |
| 根冠主红 | `#A63A2E` |
| 键控色 | `#FF00FF`（后处理删除） |

两档红都是 B < G 的暖血红。甜菜真实的紫红在洋红键控流水线上会被误判残留，
必须往暖里压。

## 视觉描述

- 一丛 7–8 片**丰满阔叶**，总高约 85%（约 27 像素），向外舒展
- 叶柄泛红加粗（1–2 像素）
- 底部中央露出一块 6×4 像素的**根冠**：暗红 `#8E2826` 打底、上缘亮红
  `#A63A2E`，形状圆钝像半个球顶出地面
- 左右留 ≥2 像素洋红边，叶顶不触顶边

## AI 提示词

```
A pixel art sprite of a ripe beet plant on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: ripe beets: a lush clump of seven or eight broad green leaves
spreading outward, reaching about 85 percent of the canvas height, with thick
reddish leaf stalks. At the base, centered, a round warm dark red beet root
crown about 6x4 final pixels pokes out of the ground line: deep red #8E2826
body with a brighter red #A63A2E highlight on its top edge. The leaves may
touch the bottom edge on both sides of the crown; keep at least 2 final pixels
of magenta on the left and right sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #3C6626, #4A7E2F, #5D9C3C, #74B84E for leaves,
#8E2826 and #A63A2E for the beet crown. Warm blood red only, never purple,
never magenta tint.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
purple, no text, no watermark, no gradient, no blur, no 3D render, no
anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, purple beet,
cool pink, magenta tint, half buried sphere, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 6 色
3. 统计透明面积 40% – 55%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 叶簇丰满 + 底部中央红根冠，一眼读出「甜菜熟了」
- [ ] 透明占比 40% – 55%
- [ ] 红只有两档暖红，无紫、无残留洋红
- [ ] 与 `beet-stage1.png` 并排明显更茂盛
