# SEASON-01 春节灯笼方块

## 用途

春节活动的**灯笼方块**贴图：整面就是一盏红灯笼的纸面（挂起来一串就是灯街）。
节日装扮方块，活动期间替换/新增进世界装饰。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（金箍与纸褶贯通到边） |
| Alpha | 无 |
| 颜色数 | 5 – 6 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 灯纸红 | `#C43C3C` |
| 灯纸暗红 | `#A83232` |
| 灯纸亮红 | `#E05252` |
| 金箍 | `#DCAE3A` `#F7DA7A` |
| 暖光点 | `#FFD84A` |

与 `logo-spring-festival` / `item-red-envelope` 共用同一套红金节日色。

## 视觉描述

红灯笼纸面铺满整格：

- 底子是亮红纸面，几条**竖向暗红纸褶**（灯笼的瓦楞）贯通上下
- 三条**横向金箍带**横贯全宽：近顶、正中、近底各一条
- 最顶与最底各一条细金端盖线，同样贯通
- 纸面上散几粒暖黄光点（透光感）
- 所有横带与竖褶**延伸到边缘并跨格对齐**——多格拼成整串灯笼墙不露馅
- 不画流苏、不画吊绳、不画汉字

## AI 提示词

```
A seamless tileable pixel art block texture of a spring festival red
lantern face, flat front view, 1024x1024, designed to be downscaled to
32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective,
even illumination.

Content: the paper face of a round red lantern filling the whole tile:
bright red paper with subtle darker red vertical paper creases running
top to bottom, three horizontal gold rib bands crossing the full width —
one near the top, one in the middle, one near the bottom — plus thin
gold cap strips along the very top and bottom edges. A few small warm
yellow glow speckles scattered on the paper. All bands, caps and creases
run edge to edge so the texture tiles.

Palette strictly: reds #C43C3C #A83232 #E05252, gold #DCAE3A #F7DA7A,
glow speckles #FFD84A.

CRITICAL: tiles seamlessly on all four edges, gold bands and paper
creases line up across tile borders.

No border, no frame, no tassels, no rope, no text, no letters, no
Chinese characters, no watermark, no gradient, no glow halo, no 3D
render, no anti-aliasing, no dark background.
```

## 负面提示词

```
tassels, rope, text, letters, Chinese characters, border, frame,
watermark, gradient, glow halo, 3D render, anti-aliasing, dark
background, perspective
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 6 个色值量化
3. 偏移半幅自检接缝（金箍必须贯通对齐）

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝
- [ ] 三条金箍横贯全宽且跨格对齐
- [ ] 红/金与春节 logo、红包图标并排是同一套色
- [ ] 图中不含任何文字/汉字
