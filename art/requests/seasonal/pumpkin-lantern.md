# SEASON-03 南瓜灯方块

## 用途

万圣节/秋日活动的**南瓜灯方块**正面贴图：橙皮 + 竖棱 + 刻出的发光笑脸。
与 A1 的普通南瓜（如有）区分：这张带脸，是「点亮的」那一面。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（竖棱贯通到边） |
| Alpha | 无 |
| 颜色数 | 6 – 8 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 瓜皮橙 | `#D64B0A` `#F79B22` |
| 瓜棱暗 | `#8A2400` |
| 内发光 | `#FFD84A` `#FFF3C4` |
| 瓜蒂绿 | `#4A7E2F` |
| 刻口描边 | `#4A2000` |

橙皮/发光色与岩浆橙、太阳色同族，孩子一眼觉得「暖的、亮的」。

## 视觉描述

- 底子是橙皮：亮橙为主，**竖向暗橙棱线**从顶到底贯穿全图（南瓜的瓣纹），
  棱线跨格对齐
- 中央刻一张**友好的笑脸**，每格出现一次、完整落在格内不碰边：
  两个三角眼 + 一个宽锯齿咧嘴，刻口透出暖黄内光，内光外缘一圈更亮的描边
- 脸上方一颗小绿瓜蒂
- 表情是**友好卡通**，不是凶脸（面向儿童）

## AI 提示词

```
A seamless tileable pixel art block texture of a carved jack-o-lantern
face, flat front view, 1024x1024, designed to be downscaled to 32x32
pixel art.

Style: retro voxel game block texture, flat shading, no perspective,
even illumination.

Content: pumpkin skin in warm orange with vertical darker orange rib
stripes running from top to bottom, the stripes continue across the tile
edges and line up. Centered on the tile one carved friendly face
appearing exactly once per tile, fully inside the tile: two triangle
eyes and one wide jagged grin, all cut through to a glowing warm yellow
interior with a thin brighter rim around each cut. Above the face one
small green stem nub. The face is a happy cartoon face, not scary.

Palette strictly: skin #D64B0A #F79B22, ribs #8A2400, glow #FFD84A
#FFF3C4, stem #4A7E2F, cut outline #4A2000.

CRITICAL: tiles seamlessly on all four edges, rib stripes line up across
tile borders, the face never touches the edges.

No border, no frame, no text, no letters, no watermark, no gradient, no
candle smoke, no 3D render, no anti-aliasing, no scary angry expression,
no purple, no night background.
```

## 负面提示词

```
scary face, angry face, border, frame, text, letters, watermark,
gradient, smoke, candle, 3D render, anti-aliasing, purple, night
background, perspective
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 7 个色值量化
3. 偏移半幅自检接缝（棱线必须贯通对齐）

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝（竖棱跨格对齐）
- [ ] 笑脸每格恰好一次、居中、不碰边，表情友好不吓人
- [ ] 内光是暖黄（`#FFD84A` 系），与太阳色一致
- [ ] 图中不含任何文字
