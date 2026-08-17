# 红杉树叶（sequoia-leaves）

## 用途

红杉的树冠贴图，带洋红镂空（Alpha Test）。红杉叶是七套树叶里的
**锈褐**档（任务卡指定）——像晒足的高山针叶，与红杉锈红的树皮成套，
在雪山石坡上识别度极高。

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
| 深叶阴影 | `#5A2E1E` |
| 阴影 | `#6E3A26` |
| 主色 | `#824730` |
| 亮部 | `#94563C` |
| 高光 | `#A6674A` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

锈褐五档与 `sequoia-log` 同源（树皮更深、树叶稍浅）。要与
`bricks` 砖红区分：本种无砖缝结构、噪点更碎。

## 视觉描述

- 密集的**细碎针叶/细枝**团：2–3 像素的锈褐小团，比绿叶种更「干」、
  颗粒更碎
- 明暗随机交错，微带暖锈色的体积感
- 镂空点散布全图，单个孔洞 1–3 像素
- 存在少量更深色（`#5A2E1E`）细枝断点，但不画可辨认的枝条
- 孔洞边缘不描边，无球果、无雪

## AI 提示词

```
A seamless tileable pixel art texture of rusty red-brown sequoia foliage,
flat front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is a warm rust brown foliage filling most of the
canvas — sun-dried mountain conifer, NOT green, NOT orange, NOT gray, NOT
bricks.

Content: densely packed fine rusty foliage twigs, small 2 or 3 pixel crumbly
clusters, drier and more fragmented than green tree foliage, with a few
darkest brown twig dots mixed in, but never a recognizable branch, no veins,
no stems. Rust tones interleave randomly for depth.

Divide the canvas into an 8x8 grid of 128x128 cells. Pick 14 to 16 cells,
scattered evenly (not clustered), and fill each ENTIRELY with one LARGE solid
magenta blob the size of that whole cell — at least 128 pixels across — ENORMOUS, unmissable. Gap edges look organic and rounded, not square.

Every gap is pure magenta #FF00FF, flat, fully saturated, hard edges, no
anti-aliasing. Magenta gaps cover roughly 22 percent of the image.

Foliage palette strictly limited to: #5A2E1E, #6E3A26, #824730, #94563C,
#A6674A.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no cones, no snow, no green leaves, no brick
pattern, no text, no watermark, no gradient, no dark background, no tiny dots, no specks, no thin lines, fully opaque leaves only in the remaining area.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, twigs, cones,
snow, green leaves, orange leaves, autumn orange, brick pattern, mortar,
planks, wood grain, text, watermark, blur, 3D render, perspective, glossy,
tiny scattered dots, thin lines, small specks
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计镂空面积 15% – 25%
4. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 镂空占比 15% – 25%
- [ ] 锈褐无绿，与 `sequoia-log.png` 并排成套（皮深叶浅）
- [ ] 与 `bricks.png` 并排不混淆（无砖缝、颗粒碎）
- [ ] 偏移半幅后中央看不出接缝，无残留洋红与紫边
