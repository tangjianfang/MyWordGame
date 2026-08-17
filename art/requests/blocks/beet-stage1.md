# 甜菜 · 半熟期（beet-stage1）

## 用途

甜菜作物 3 阶段的**第 1 阶**（半熟）。共用骨架，生长差异句：60% 高的叶簇、
叶柄开始泛红。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **55% – 70%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 叶阴影 | `#3C6626` |
| 叶主色 | `#4A7E2F` |
| 叶亮部 | `#5D9C3C` |
| 叶高光 | `#74B84E` |
| 叶柄泛红 | `#C96A4A` |
| 键控色 | `#FF00FF`（后处理删除） |

`#C96A4A` 是 B < G 的**暖红**（偏橙），甜菜的紫红调必须往暖里压——冷紫红
会被后处理「去洋红边」当残留误删。

## 视觉描述

- 一丛 5–6 片**阔叶**从底部散开，总高约 60%（约 19 像素）
- 叶片卵圆形、边缘微波浪，2–4 像素宽
- 叶柄 1 像素宽，靠近根部 2–3 像素段泛暖红 `#C96A4A`
- 地面以下不画块根（本阶根还小）
- 左右留 ≥2 像素洋红边

## AI 提示词

```
A pixel art sprite of a half-grown beet plant on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: half-grown beets: a clump of five or six broad oval green leaves
spreading out from the base, reaching about 60 percent of the canvas height.
Leaf blades are 2 to 4 final pixels wide with slightly wavy edges. The thin
1-pixel leaf stalks turn warm reddish orange near the base. No root visible
above ground yet. The leaves may touch the bottom edge; keep at least 2 final
pixels of magenta on the left, right and top sides.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #3C6626, #4A7E2F, #5D9C3C, #74B84E for leaves,
#C96A4A for the warm reddish stalk bases.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
beet root, no purple, no text, no watermark, no gradient, no blur, no 3D
render, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, beet root,
purple leaves, cool pink, magenta tint, text, watermark, blur, 3D render,
perspective, anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计透明面积 55% – 70%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 叶簇高约 60%，叶柄根部有暖红
- [ ] 透明占比 55% – 70%
- [ ] 无残留洋红与紫边，红仅限暖红（B < G）
- [ ] 与 `beet-stage0/2` 并排高度递增
