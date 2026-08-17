# ARCH-08 村庄木牌方块

## 用途

村庄建筑门旁挂的**木牌方块**贴图（正面纹理），第 3 波村庄结构生成的标配件。
刻一座小房子剪影表示「这是村庄的房子」。AI 画不对文字，所以牌子上**只刻图形不刻字**，
真要文字一律引擎侧像素字体叠加。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（木板纹横向贯通） |
| Alpha | 无（整面木板） |
| 颜色数 | 5 – 7 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 木板主色 | `#9C7549` |
| 木板暗 | `#8A6741` |
| 木板亮 | `#B98D57` |
| 板缝 | `#6B4F30` |
| 刻痕 | `#5F4630` |

与游戏 `planks` 同色系，保证村庄建筑墙面与牌子材质咬合。

## 视觉描述

一块横纹木牌：

- 底子是四条横板拼成，板与板之间 1 像素深色板缝，缝线**贯通到边缘**（这是无缝的关键）
- 木板上有轻微颗粒噪点（1–2 像素级，与 `planks` 一致）
- 中央刻一个**小房子剪影**（三角顶 + 方身），刻痕是深棕色凹槽，
  凹槽下缘带 1 像素亮色「凿口高光」
- 剪影每格出现一次、完整落在格内，**不碰边**（每个方块显示一次，属预期重复）
- 不画钉子、不画铁件、不画文字

## AI 提示词

```
A seamless tileable pixel art block texture of a carved wooden village
sign, flat front view, 1024x1024, designed to be downscaled to 32x32
pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a horizontal wooden plank board made of four plank rows, base
warm brown with subtle grain speckles and thin darker seams between
planks. In the center, one simple carved pictogram of a small house with
a triangular roof and a square body, engraved as darker brown cut marks
with a thin light chisel highlight line under each cut. The pictogram
appears exactly once per tile and stays fully inside the tile, never
touching the edges. Plank seams run edge to edge so the texture tiles.

Palette strictly: planks #9C7549 #8A6741 #B98D57, seams #6B4F30, carved
marks #5F4630.

CRITICAL: tiles seamlessly on all four edges, plank grain and seams line
up across tile borders.

No border, no frame, no text, no letters, no nails, no metal parts, no
watermark, no gradient, no glow, no 3D render, no anti-aliasing.
```

## 负面提示词

```
text, letters, numbers, nails, metal, border, frame, watermark, gradient,
glow, 3D render, anti-aliasing, perspective, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 5 个色值量化（无洋红，跳过键控）
3. 偏移半幅自检接缝（板缝必须贯通对齐）

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝（偏移半幅后中央看不出缝）
- [ ] 房子剪影每格恰好一次、居中、不碰边
- [ ] 刻痕是凹进去的深棕，不是画上去的黑线
- [ ] 与 `planks.png` 并排，板材色系一致
- [ ] 图中不含任何文字
