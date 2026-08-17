# 雪松树叶（cedar-leaves）

## 用途

雪松的树冠贴图，带洋红镂空（Alpha Test）。雪松叶是七套树叶里的
**蓝绿**档（任务卡指定），与松叶的冷深绿拉开：本种明显偏蓝、更浅一档。

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
| 深叶阴影 | `#1F4A44` |
| 阴影 | `#2A5E56` |
| 主色 | `#36736A` |
| 亮部 | `#42897E` |
| 高光 | `#519E92` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

蓝绿五档的绿通道始终 ≥ 蓝通道——是「带蓝的绿」而不是「带绿的蓝」，
避免被读成水方块（`water` 的蓝更深更纯）。

## 视觉描述

- 密集的**鳞叶小片**：2–3 像素的扁平小鳞团（雪松小枝的鳞叶感），
  比松针更平整、边缘更钝
- 明暗随机交错，冷蓝绿调
- 镂空点散布全图，单个孔洞 1–3 像素
- 孔洞边缘不描边，无球果、无枝条、无雪

## AI 提示词

```
A seamless tileable pixel art texture of blue-green cedar foliage, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is a clearly readable seafoam blue-green filling most of the
canvas — medium bright, green first with a blue lean, like cedar foliage in
daylight; NEVER navy, NEVER near-black, NEVER dark.

Content: densely overlapping flat scaly leaf sprays, 2 or 3 pixel scales like cedar
branchlets, no veins, no stems. Tones interleave randomly for depth.

Divide the canvas into an 8x8 grid of 128x128 cells. Pick 14 to 16 cells,
scattered evenly (not clustered), and fill each ENTIRELY with one LARGE solid
magenta blob the size of that whole cell — at least 128 pixels across — ENORMOUS, unmissable. Gap edges look organic and rounded, not square.

Every hole is flat, fully saturated, hard-edged pure magenta, no
anti-aliasing. Holes cover roughly 25 percent of the image.

Foliage palette strictly limited to: #1F4A44, #2A5E56, #36736A, #42897E,
#519E92.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no cones, no snow, no text, no watermark, no gradient, no dark background, no navy, no pure blue, no tiny dots, no specks, no thin lines, fully opaque leaves only in the remaining area.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, twigs, cones,
snow, frost, water, ocean, waves, pure blue, navy, gray foliage, autumn
colors, text, watermark, blur, 3D render, perspective, glossy, tiny scattered
dots, thin lines, small specks, near-black foliage, deep sea, unbroken foliage, dense wall without holes
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计镂空面积 15% – 25%
4. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 镂空占比 15% – 25%
- [ ] 与 `pine-leaves.png` 并排明显更偏蓝
- [ ] 与 `water.png` 并排仍读作植物（绿 ≥ 蓝），不与水混淆
- [ ] 偏移半幅后中央看不出接缝
- [ ] 无残留洋红与偏紫过渡色
