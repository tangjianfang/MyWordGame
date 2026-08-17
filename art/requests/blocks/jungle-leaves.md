# 丛林树叶（jungle-leaves）

## 用途

丛林树的树冠贴图，带洋红镂空（Alpha Test）。丛林叶是七套树叶里的
**浓艳饱和深绿**——比橡叶更绿更「肥」，叶片更大，热带感。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（含镂空） |
| Alpha | 有，仅 0 或 255 |
| 镂空面积 | 占全图 **15% – 25%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 深叶阴影 | `#1D4A16` |
| 阴影 | `#27631C` |
| 主色 | `#337C24` |
| 亮部 | `#42962F` |
| 高光 | `#52AC3C` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

五档全是**高饱和的纯绿**（不含黄调），比橡叶 `#3F7A2E` 更浓更艳，
与松叶的冷感也不同——这是「旺盛生长」的绿。

## 视觉描述

- 密集交叠的**大阔叶**团：叶片 3–5 像素（比橡叶大一档），圆钝厚实
- 明暗对比比其他树叶强一档（热带阳光下的浓影），高光块更整
- 镂空点散布全图，单个孔洞 1–3 像素，稍大稍圆
- 孔洞边缘不描边，无花、无果、无藤蔓

## AI 提示词

```
A seamless tileable pixel art texture of lush vivid jungle leaves, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is a rich saturated tropical green filling most of
the canvas — vivid pure green with no yellow lean, lush and dense, NOT dark,
NOT black, NOT teal, NOT navy.

Content: densely overlapping broad tropical leaves, chunky rounded 3 to 5
pixel blobs, thicker and larger than temperate tree leaves, no veins, no
stems. Dark and bright greens interleave with slightly stronger contrast than
other trees, like sunlit rainforest canopy.

Divide the canvas into an 8x8 grid of 128x128 cells. Pick roughly 12 cells,
scattered evenly (not clustered), and fill each ENTIRELY with one LARGE solid
magenta blob the size of that whole cell — at least 100 pixels across, never a
small speck, never a thin line, never a tiny dot. Gap edges look like organic
rounded holes, not perfect squares.

Every gap is pure magenta #FF00FF, flat, fully saturated, hard edges, no
anti-aliasing. Magenta gaps cover roughly 20 percent of the total image area.

Leaf palette strictly limited to: #1D4A16, #27631C, #337C24, #42962F, #52AC3C.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no flowers, no fruit, no vines, no text, no
watermark, no gradient, no dark background, no navy, no teal, no tiny dots, no
thin lines.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, vines, flowers,
fruit, banana leaves, monstera, recognizable single leaf, yellow-green,
autumn colors, teal, navy, text, watermark, blur, 3D render, perspective,
glossy, tiny scattered dots, thin lines, small specks
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计镂空面积 15% – 25%
4. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 镂空占比 15% – 25%
- [ ] 浓艳纯绿、叶片比 `leaves.png` 大一档
- [ ] 与 `grass-top.png` 并排明显更深更饱和
- [ ] 偏移半幅后中央看不出接缝，无残留洋红与紫边
