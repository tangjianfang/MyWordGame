# 灌木叶（bush-leaves）

## 用途

灌木（`bush`，无主干、1–2 层高的矮丛）的树冠贴图，带洋红镂空
（Alpha Test）。灌木叶是七套树叶里的**灰绿**档——干燥、低饱和，
与树冠的浓绿区分，读作「路边矮丛」。

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
| 深叶阴影 | `#4A5A38` |
| 阴影 | `#5C6E46` |
| 主色 | `#6E8255` |
| 亮部 | `#7F9665` |
| 高光 | `#91AA76` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

灰绿五档：绿里掺了灰与一点点黄（鼠尾草感），全档 R < G，安全且
与其他六种树叶都不撞色。

## 视觉描述

- 密集的**细碎小叶**团：叶片 2–3 像素的小碎块，比橡叶更小更稀疏一点
- 明暗随机交错，整体低饱和、发灰
- 镂空点散布全图，单个孔洞 1–3 像素
- 孔洞边缘不描边，无浆果、无花、无枝刺

## AI 提示词

```
A seamless tileable pixel art texture of dry gray-green shrub leaves, flat
front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is a muted desaturated sage green filling most of the
canvas — dusty gray-green, much less vivid than tree canopy, NOT dark, NOT
brown, NOT gray stone, NOT teal.

Content: densely packed tiny scrub leaves, small 2 or 3 pixel irregular
specksized clusters, slightly sparser and scrappier than tree foliage, no
veins, no stems. Muted greens interleave randomly for a dry roadside thicket
feel.

Divide the canvas into an 8x8 grid of 128x128 cells. Pick 14 to 16 cells,
scattered evenly (not clustered), and fill each ENTIRELY with one LARGE solid
magenta blob the size of that whole cell — at least 128 pixels across — ENORMOUS, unmissable. Gap edges look organic and rounded, not square.

Every gap is pure magenta #FF00FF, flat, fully saturated, hard edges, no
anti-aliasing. Magenta gaps cover roughly 22 percent of the image.

Leaf palette strictly limited to: #4A5A38, #5C6E46, #6E8255, #7F9665,
#91AA76.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no thorns, no berries, no flowers, no
lavender, no text, no watermark, no gradient, no dark background, no brown, no
gray, no tiny dots, no specks, no thin lines, fully opaque leaves only in the remaining area.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, thorns, berries,
flowers, lavender, sage flowers, vivid green, brown leaves, dead branches,
gray stone, teal, text, watermark, blur, 3D render, perspective, glossy, tiny
scattered dots, thin lines, small specks
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计镂空面积 15% – 25%
4. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 镂空占比 15% – 25%
- [ ] 灰绿低饱和，与 `leaves.png` 并排明显更「干」更灰
- [ ] 不读作棕色枯枝或灰石头
- [ ] 偏移半幅后中央看不出接缝，无残留洋红与紫边
