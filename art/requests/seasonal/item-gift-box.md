# SEASON-10 礼盒（物品图标）

## 用途

节日通用**礼物盒物品图标**：圣诞树下/活动奖励的礼盒，进背包要立刻有图。
32×32 物品图标，洋红背景键控（同 `items/stick.md` 范式）。
与 `item-red-envelope` 成对：一个春节红、一个节日通用。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 颜色数 | ≤ 6 色（不含洋红） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 盒体红 | `#C43C3C` |
| 盒体暗红 | `#A83232` |
| 盒体亮红 | `#E05252` |
| 丝带金 | `#DCAE3A` `#F7DA7A` |
| 描边 | `#241A11` |

## 视觉描述

- 一只正视的方形礼盒：盒身主红，右侧面一条暗红竖带表现体积
- 盒面上一横一竖两条**金丝带**，交叉成十字；盒盖上有简单的**金色蝴蝶结**
  （两个三角结耳 + 中间结心）
- 整体 1 像素深描边，主体占画幅约 80%，居中
- 不画礼品签、贺卡、雪花

## AI 提示词

```
A single gift box icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A cubic gift box seen from the
front: red box body with one darker red vertical band on the right side
for volume, one vertical and one horizontal gold ribbon band crossing
on the front face, and on the lid a simple gold bow made of two small
triangles meeting at a center knot. A dark 1-pixel outline around the
whole box. The box fills about 80 percent of the frame, centered, with
pure magenta #FF00FF keyout background on all sides, fully saturated,
hard edges, no anti-aliasing.

Color palette strictly: #C43C3C, #A83232, #E05252, #DCAE3A, #F7DA7A,
#241A11 outline only.

No text, no letters, no gift tag, no card, no snowflake, no watermark,
no shadow, no glow. Hard pixel edges.
```

## 负面提示词

```
text, letters, gift tag, card, snowflake, watermark, shadow, glow,
gradient, 3D render, anti-aliasing, transparent background, checkerboard
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明，去洋红边
3. 量化到上表 6 个色值

## 验收标准

- [ ] 尺寸恰为 32 × 32，32 位 RGBA，Alpha 只有 0/255
- [ ] 十字丝带 + 蝴蝶结结构清楚，缩到 16×16 仍认得出是礼盒
- [ ] 与 `item-red-envelope.png` 并排是同一套红金
- [ ] 图中不含任何文字
