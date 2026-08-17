# 小麦 · 成熟期（wheat-stage2）

## 用途

小麦作物 3 阶段的**第 2 阶**（成熟可收割）。整株转金、麦穗饱满低垂，
是玩家判断「能收了」的视觉信号。收割掉落小麦与种子（第 1 波农业赛道）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | **45% – 60%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 秆阴影 | `#7E7226` |
| 秆主色 | `#A8862A` |
| 穗主色 | `#C8A838` |
| 穗亮部 | `#E0C44C` |
| 芒/高光 | `#F0D868` |
| 键控色 | `#FF00FF`（后处理删除） |

成熟小麦全套金棕色，与 `gold-ore` 的金黄系同源但更黄更哑光。

## 视觉描述

- 5–7 根**成熟金秆**几乎占满全高（28–31 像素），秆间留 1–2 像素缝隙
- 每根秆顶端一支饱满麦穗（4–5 像素高、2–3 像素宽，穗粒用亮暗两金交替），
  穗顶 1–2 像素短芒 `#F0D868`
- 部分穗因重量微微低垂（顶端偏斜 1–2 像素）
- 秆上有 1 片下垂的枯叶
- 秆底触底边，左右留 ≥2 像素洋红边，穗顶不触顶边

## AI 提示词

```
A pixel art sprite of a ripe golden wheat plant on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: ripe wheat: five to seven tall golden stalks side by side, reaching
almost the full canvas height, with small gaps between them. Each stalk carries
a plump golden ear about 4 or 5 final pixels tall with short bristles on top;
some ears bend slightly under their own weight. Each stalk also has one small
drooping dry leaf. The stalks may touch the bottom edge; keep at least 2 final
pixels of magenta on the left and right sides and the ears never touch the top
edge.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the plant. No partial
transparency.

Palette strictly limited to: #7E7226, #A8862A, #C8A838, #E0C44C, #F0D868. The
whole plant is golden; no green remains.

The sprite does not tile; the background stays solid magenta to all four edges.

No outline, no frame, no drop shadow, no ground, no soil, no farmland rows, no
sickle, no text, no watermark, no gradient, no blur, no 3D render, no
anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, ground, soil, farmland, sickle, tools,
green stalks, unripe wheat, text, watermark, blur, 3D render, perspective,
anti-aliasing
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控 + 去洋红边 + 量化到上表 5 色
3. 统计透明面积 45% – 60%

## 验收标准

- [ ] 尺寸恰为 32 × 32，Alpha 仅 0/255
- [ ] 整株金黄无绿色残留，麦穗饱满可辨
- [ ] 透明占比 45% – 60%
- [ ] 无残留洋红与紫边
- [ ] 与 `wheat-stage1.png` 并排，本阶明显更高更金
