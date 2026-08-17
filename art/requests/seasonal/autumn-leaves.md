# SEASON-05 秋日落叶方块

## 用途

秋季变体的**落叶树冠方块**贴图：结构与 `blocks/leaves.md` 完全一致
（密叶团 + 洋红镂空 15%-25%），只把叶绿换成秋色。秋天活动期间整片森林换装。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（含镂空孔洞的衔接） |
| Alpha | 有，仅 0 或 255（镂空走洋红键控） |
| 镂空面积 | 全图 **15% – 25%** |
| 颜色数 | 5 色（不含洋红） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 深叶阴影 | `#5A2E1C` |
| 阴影 | `#8A4A28` |
| 主色 | `#C97B3A` |
| 亮部 | `#E0A84C` |
| 高光 | `#F2C878` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

秋叶主色比 `grass-top` 更暖更沉，压在秋日草地上层次分明。

## 视觉描述

与 `leaves` 同一画法、换色：

- 密集交叠的小叶片团，2-4 像素不规则小块，**不画可辨认的单片树叶**
  （不要叶脉、叶柄）
- 明暗暖色随机交错
- 镂空孔洞散布全图、单个 1-3 像素、跨格衔接，不要集中成大洞
- 孔洞边缘不加描边
- 不画树枝、橡果、雪

## AI 提示词

```
A seamless tileable pixel art texture of autumn forest leaves, flat
front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective,
even illumination. Base color is mid warm orange — autumn canopy, not
green.

Content: densely overlapping small leaf clusters in warm autumn tones,
irregular rounded blobs, no veins, no stems. Light and dark tones
interleave randomly for depth. Scattered evenly across the whole image
about twelve large organic rounded holes, each around one eighth of the
tile wide, never a tiny speck, never clustered, with organic rounded
edges. Every hole is pure magenta #FF00FF, flat, fully saturated, hard
edges, no anti-aliasing. Magenta holes cover roughly 20 percent of the
area, including at the tile borders so the holes tile too.

Palette strictly: #5A2E1C, #8A4A28, #C97B3A, #E0A84C, #F2C878.

CRITICAL: tiles seamlessly on all four edges, including the magenta
holes.

No border, no frame, no branches, no acorns, no snow, no green tint, no
text, no letters, no watermark, no gradient, no dark background, no tiny
dots, no thin lines.
```

## 负面提示词

```
green, branches, acorns, snow, border, frame, text, letters, watermark,
gradient, dark background, tiny dots, thin lines, leaf veins, single
leaf
```

## 后处理

1. 最近邻降采样到 32 × 32（**先降采样再键控，去洋红边必须做**）
2. 键控：洋红占比 > 50% 的像素 Alpha = 0，其余 255
3. 去洋红边：不透明像素中 R 与 B 同时高于 G 的判残留，替换为相邻秋叶色
4. 量化到上表 5 个色值
5. 统计镂空面积须落 15% – 25%
6. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 只有 0/255
- [ ] Alpha = 0 占比 15% – 25%
- [ ] 不透明像素无偏紫残留（无 R > G 且 B > G）
- [ ] 与 `leaves.png` 并排：同一画法、不同色季
- [ ] 4×4 平铺孔洞不成规律阵列
