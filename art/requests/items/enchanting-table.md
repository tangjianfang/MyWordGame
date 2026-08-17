# 附魔台物品图标（enchanting_table）

## 用途

附魔台物品（milestone-11 第 2 波注册 `enchanting_table` 物品，numericId=1604，
`blockId` 指向 `enchanting_table` 方块）的**背包/hotbar 图标**。
摆附魔台前孩子会先在背包里看到它，要一眼读出「一本摊开的书 + 悬浮宝石」。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 16 × 16 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体图标） |
| Alpha | **有**，洋红键控 |
| 颜色数 | ≤ 10 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 书页暗 | `#D8CDB4` |
| 书页主 | `#E8E0CC` |
| 书页亮 | `#F4EEDC` |
| 封皮暗 | `#6F5430` |
| 封皮主 | `#8A6741` |
| 封皮亮 | `#9C7549` |
| 宝石暗边 | `#2C4E8C` |
| 宝石主 | `#3E6FBF` |
| 宝石高光 | `#7FA8E8` |

与 `book.png` / `enchanted_book.png` 同一套书页与封皮色，宝石色与方块
`enchanting-table` 顶面宝石一致（青金石联动）。

## 视觉描述

- 下半（约第 7-14 行）：一本**摊开的书**，两页书页 `#E8E0CC` 带 1 像素暗缝，
  周围 1 像素 `#8A6741` 封皮边、底缘 `#6F5430` 暗一档
- 上半（约第 1-5 行）：居中的小菱形宝石（4-5 像素宽），`#3E6FBF` 主体 +
  `#2C4E8C` 暗边 + 顶部 1 像素 `#7FA8E8` 高光，与书之间留 1 行空隙（透明）
- 深色描边 1px 提升背包可读性（封皮色即可，不另加新色）

## AI 提示词

```
A pixel art game item icon of an enchanting table token, flat front view,
1024x1024, designed to be downscaled to 16x16 pixel art.

Style: retro voxel game inventory icon, crisp pixel edges, no perspective.

Content: the lower half is an open book lying flat, two cream pages with a
1-pixel center seam, wrapped in a thin warm brown leather cover. The upper
half is one small floating diamond-shaped sapphire gem, about 5 pixels wide
in final size, with a dark blue outline and a single bright highlight pixel
on top, separated from the book by one transparent row.

Color palette strictly limited to #E8E0CC, #D8CDB4, #F4EEDC for pages,
#8A6741, #9C7549, #6F5430 for the leather cover, and #2C4E8C, #3E6FBF,
#7FA8E8 for the gem.

Everything outside the book and the gem is solid pure magenta #FF00FF,
hard edges, no anti-aliasing against the magenta.

No text, no watermark, no drop shadow, no glow, no sparkle particles,
no closed book, no quill, no candle.
```

## 负面提示词

```
text, watermark, drop shadow, glow, sparkle, quill, candle, closed book,
gradient, blur, photorealistic, 3D perspective
```

## 后处理

1. 最近邻降采样到 16 × 16
2. 洋红键控：`#FF00FF` 占比 > 50% 的连通区 Alpha 置 0，其余置 255
3. 去洋红边（不透明像素中 R>G 且 B>G 的替换为相邻主体色）
4. 量化到上表 9 色，Alpha 归 0/255 两态

## 验收标准

- [ ] 尺寸恰为 16 × 16，Alpha 只有 0/255 两态、无残留紫边
- [ ] 摊开的书 + 悬浮菱形宝石，两者不粘连（中间有透明行）
- [ ] 与 `book.png` 并排，书页/封皮色完全一致；与方块顶面贴图并排，宝石色一致
- [ ] ≤ 9 色 + 透明，无调色板外杂色
