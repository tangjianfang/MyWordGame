# SEASON-02 圣诞树树叶方块

## 用途

圣诞活动的**装饰松叶方块**贴图：深松绿针叶上挂彩球，活动期间把村庄/家园的
松树换成这种「圣诞树」材质。与 A1 的 `pine-leaves` 共用松绿基色，只加挂饰。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无 |
| 颜色数 | 6 – 8 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 松绿（深→亮） | `#1E3B2A` `#2A5038` `#356647` `#448059` |
| 红彩球 | `#C43C3C` |
| 金彩球 | `#F7DA7A` |
| 高光点 | `#F6FAFC` |

松绿四色与计划中 `pine-leaves` 的色板一致（A1 卡片定的松树色）。

## 视觉描述

- 底子是密叠的松针团：不规则圆钝小块，明暗随机交错（同 `leaves` 的画法，换松绿色板）
- 均匀散布约 **9 颗圆彩球**：红球与金球交替，每颗 2-3 像素宽，带 1 像素白高光点
- 彩球分布均匀不扎堆、**不碰边**（每格各自挂球，属预期重复）
- 可有一条细金色彩带弧线穿过中部（同样不碰边）
- 不画星星、不画蜡烛、不画雪（雪是别的方块的事）

## AI 提示词

```
A seamless tileable pixel art block texture of decorated christmas tree
foliage, flat front view, 1024x1024, designed to be downscaled to 32x32
pixel art.

Style: retro voxel game block texture, flat shading, no perspective,
even illumination. Base color is dark cold green — deep pine forest,
darker and colder than lawn grass.

Content: densely packed small pine needle clusters as irregular rounded
blobs in layered pine greens. Scattered evenly across the tile about
nine round ornaments: red balls and gold balls alternating, each two to
three final pixels wide with one tiny white sparkle pixel, evenly
distributed, never clustered, never touching the tile edges. Optionally
one thin gold tinsel strand curves across the middle without touching
the edges.

Palette strictly: pine greens #1E3B2A #2A5038 #356647 #448059, ornaments
#C43C3C and #F7DA7A, sparkle #F6FAFC.

CRITICAL: tiles seamlessly on all four edges, needles continue across
edges, ornaments stay fully inside the tile.

No border, no frame, no star, no candles, no snow, no text, no letters,
no watermark, no gradient, no glow, no 3D render, no anti-aliasing, no
bright lawn green, no brown branches.
```

## 负面提示词

```
star, candles, snow, border, frame, text, letters, watermark, gradient,
glow, 3D render, anti-aliasing, branches, bright green, brown
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 7 个色值量化
3. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝（针叶跨边连续）
- [ ] 彩球完整落在格内、约 9 颗、红金交替
- [ ] 基色是深冷松绿，与草地方块拉开层次
- [ ] 4×4 平铺预览彩球不排成规整网格
