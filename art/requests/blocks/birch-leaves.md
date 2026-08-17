# 白桦树叶（birch-leaves）

## 用途

白桦树的树冠贴图，带洋红镂空（Alpha Test 渲染，与 `leaves` 同管线）。
白桦叶是七套树叶里唯一的**亮黄绿**——靠色相（明显偏黄）与草地的冷绿
区分，而不是靠明度压暗。

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
| 深叶阴影 | `#5F8A24` |
| 阴影 | `#74A02C` |
| 主色 | `#89B638` |
| 亮部 | `#9FCB46` |
| 高光 | `#B5DE58` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

亮黄绿五档。与 `grass-top`（冷绿 `#5D9C3C`）并排时**明显更黄**，
这是白桦的识别点；其余六种树叶都比草绿深，只有白桦走亮黄路线。

## 视觉描述

- 密集交叠的**小圆叶**团：叶片 2–3 像素的圆钝小块，比橡树叶更小更碎
- 明暗随机交错，偏黄的高光点缀要多一些（阳光下白桦叶的通透感）
- 镂空点散布全图，单个孔洞 1–3 像素，不要集中成大洞
- 孔洞边缘不加描边，无枝、无果实

## AI 提示词

```
A seamless tileable pixel art texture of bright birch tree leaves, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is a bright yellow-green filling most of the canvas —
clearly yellower than lawn grass, fresh spring birch foliage, NOT dark, NOT
teal, NOT navy.

Content: densely overlapping small rounded birch leaves, 2 or 3 pixel irregular
blobs, no veins, no stems. Light and dark yellow-greens interleave randomly,
with plenty of bright highlights for a sunlit airy feel.

Divide the canvas into an 8x8 grid of 128x128 cells. Pick 14 to 16 cells,
scattered evenly (not clustered), and fill each ENTIRELY with one LARGE solid
magenta blob the size of that whole cell — at least 128 pixels across — ENORMOUS, unmissable. Gap edges look organic and rounded, not square.

Every gap is pure magenta #FF00FF, flat, fully saturated, hard edges, no
anti-aliasing. Magenta gaps cover roughly 22 percent of the image.

Leaf palette strictly limited to: #5F8A24, #74A02C, #89B638, #9FCB46, #B5DE58.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no fruit, no catkins, no text, no watermark,
no gradient, no dark background, no navy, no teal, no tiny dots, no specks, no thin lines, fully opaque leaves only in the remaining area.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, twigs, fruit,
catkins, flowers, autumn colors, orange leaves, dark leaves, teal, navy, leaf
veins, realistic leaves, text, watermark, blur, 3D render, perspective, glossy,
tiny scattered dots, thin lines, small specks
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红占比 > 50% 的像素 Alpha = 0，其余 255
3. 去洋红边（不透明像素中 R > G 且 B > G 的替换为相邻叶色）
4. 量化到上表 5 色
5. 统计镂空面积 15% – 25%
6. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 镂空占比 15% – 25%，孔洞散布无规律阵列
- [ ] 与 `grass-top.png` 并排**明显更黄**（色相区分，不靠明度）
- [ ] 与 `birch-log.png` 并排，白干亮叶成套
- [ ] 偏移半幅后中央看不出接缝
- [ ] 无残留洋红与偏紫过渡色
