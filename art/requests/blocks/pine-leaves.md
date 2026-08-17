# 松树叶（pine-leaves）

## 用途

松树的树冠贴图，带洋红镂空（Alpha Test）。松针是**冷深绿**，任务卡指定
的松叶五色是七套树叶的基准示例；比 `leaves`（橡叶）更冷、比
`cedar-leaves` 更绿不偏蓝。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（含镂空） |
| Alpha | 有，仅 0 或 255 |
| 镂空面积 | 占全图 **15% – 22%**（针叶密，孔略少） |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 深针阴影 | `#1E3B2A` |
| 阴影 | `#2A5038` |
| 主色 | `#356647` |
| 亮部 | `#448059` |
| 高光 | `#56996B` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

任务卡示例色板原样使用。主色 `#356647` 比 `grass-top` 的 `#5D9C3C`
**明显更深更冷**，树冠压在草地上层次分明。

## 视觉描述

- 密集的**细针簇**：2–4 像素的短针束团，边缘比橡叶更「刺」一些
  （针尖感），但仍不画单根可辨的松针
- 明暗随机交错，冷调为主
- 镂空点散布全图，单个孔洞 1–3 像素，密度略低于橡叶
- 孔洞边缘不描边，无松果、无枝条、无雪

## AI 提示词

```
A seamless tileable pixel art texture of dark pine needles, flat front view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is a deep cold forest green filling most of the
canvas — dark cool green, NOT black, NOT navy, NOT teal, not bright lawn
green.

Content: densely packed clusters of short pine needle tufts, irregular 2 to 4
pixel bundles with slightly prickly edges, but never a single recognizable
needle, no veins, no stems. Dark and mid greens interleave randomly for depth.

Divide the canvas into an 8x8 grid of 128x128 cells. Pick roughly 11 cells,
scattered evenly (not clustered), and fill each ENTIRELY with one LARGE solid
magenta blob the size of that whole cell — at least 100 pixels across, never a
small speck, never a thin line, never a tiny dot. Gap edges look like organic
rounded holes, not perfect squares.

Every gap is pure magenta #FF00FF, flat, fully saturated, hard edges, no
anti-aliasing. Magenta gaps cover roughly 18 percent of the total image area.

Needle palette strictly limited to: #1E3B2A, #2A5038, #356647, #448059,
#56996B.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no pine cones, no snow, no text, no
watermark, no gradient, no dark background, no navy, no teal, no tiny dots, no
thin lines.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, twigs, pine
cones, snow, frost, autumn colors, yellow leaves, bright green, teal, navy,
single needle, leaf veins, text, watermark, blur, 3D render, perspective,
glossy, tiny scattered dots, thin lines, small specks
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计镂空面积 15% – 22%
4. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 镂空占比 15% – 22%
- [ ] 与 `grass-top.png` 并排明显更深更冷
- [ ] 与 `cedar-leaves.png` 并排：本种更绿、雪松更偏蓝
- [ ] 偏移半幅后中央看不出接缝
- [ ] 无残留洋红与偏紫过渡色
