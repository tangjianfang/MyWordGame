# 樱花树叶（cherry-leaves）

## 用途

樱花树的树冠贴图，带洋红镂空（Alpha Test）。樱冠是**粉白的花团**
（任务卡指定粉白系）——七套树叶里唯一无绿色的一种，平原上一眼可辨。

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
| 花团阴影（深玫瑰） | `#D4A092` |
| 阴影（玫瑰） | `#E2B8AC` |
| 主色（浅粉） | `#EECFC6` |
| 亮部（淡粉白） | `#F6E4DE` |
| 高光（近白） | `#FBF0EC` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

**必须全程用暖粉**（B < G 的玫瑰/杏粉）。真实的冷粉
（R > G 且 B > G）会被后处理「去洋红边」当成键控残留整片误删——
这是洋红键控流水线的硬约束，写提示词时就要把粉往暖里压。

## 视觉描述

- 密集交叠的**花团**：2–4 像素的花簇小块，深浅粉交错成蓬松花球
- 从外圈的深玫瑰到中心的近白，5 档粉色都要出现
- 镂空点散布全图，单个孔洞 1–3 像素
- 孔洞边缘不描边，无枝条、无绿叶、无完整单朵花

## AI 提示词

```
A seamless tileable pixel art texture of pink cherry blossom canopy, flat
front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is warm pastel rose pink filling most of the canvas —
soft warm pink like peach blossom, NOT cold pink, NOT purple, NOT magenta, NOT
white.

Content: densely overlapping cherry blossom clusters, 2 to 4 pixel puffy blobs,
deep rose on the outside shading to near-white at the brightest spots, all
five pink layers present, no green leaves, no stems, no single recognizable
flower.

Divide the canvas into an 8x8 grid of 128x128 cells. Pick roughly 12 cells,
scattered evenly (not clustered), and fill each ENTIRELY with one LARGE solid
magenta blob the size of that whole cell — at least 100 pixels across, never a
small speck, never a thin line, never a tiny dot. Gap edges look like organic
rounded holes, not perfect squares.

Every gap is pure magenta #FF00FF, flat, fully saturated, hard edges, no
anti-aliasing. Magenta gaps cover roughly 20 percent of the total image area.

Blossom palette strictly limited to: #D4A092, #E2B8AC, #EECFC6, #F6E4DE,
#FBF0EC. Warm peachy rose pinks only.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no green leaves, no single flowers, no text,
no watermark, no gradient, no dark background, no purple, no cold pink, no
tiny dots, no thin lines.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, green leaves,
single flower, falling petals, purple, violet, cold pink, magenta petals,
white background, text, watermark, blur, 3D render, perspective, glossy, tiny
scattered dots, thin lines, small specks
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. **重点抽查**：若成品花团出现大片被「去洋红边」顶替出来的脏色块，
  说明 AI 画了冷粉——提示词再压暖一档重新生成
4. 统计镂空面积 15% – 25%
5. 偏移半幅自检接缝（含孔洞衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 镂空占比 15% – 25%
- [ ] 全部像素为暖粉（不存在 R > G 且 B > G 的不透明像素）
- [ ] 无绿色叶片、无完整单朵花
- [ ] 偏移半幅后中央看不出接缝，无残留洋红与紫边
