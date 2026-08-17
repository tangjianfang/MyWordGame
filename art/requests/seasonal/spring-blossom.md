# SEASON-07 春日花瓣草地（顶面）

## 用途

春季变体的**草地顶面**贴图：草地落满樱粉花瓣与小白花。
结构与 `blocks/grass-top.md` 同构，春天活动期间替换草方块顶面。
粉白色与 A1 的 `cherry-leaves`（樱花树）呼应。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无 |
| 颜色数 | 6 – 7 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 草绿（与 grass-top 同） | `#4A7E2F` `#5D9C3C` `#74B84E` |
| 花瓣粉 | `#F2C4D0` `#E8A8BC` |
| 小白花 | `#F6E8EC` |
| 花心金 | `#F7DA7A` |

## 视觉描述

俯视的春日草地：

- 底子就是本游戏的草地颗粒噪点（与 `grass-top` 同款三绿）
- 草地上均匀撒**樱粉花瓣**：圆钝小短笔 1-2 像素，两档粉交替，
  约占 1/3 面积，部分跨过格边（跨边必须对齐）
- 点缀几朵**五点小白花**（中心一粒金点）
- 不画带茎的整花、不画树枝、不画泥土

## AI 提示词

```
A seamless tileable pixel art block texture of spring grass covered
with fallen cherry blossom petals, flat top-down view, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective,
even illumination. This is the top face texture of a grass block in
spring.

Content: a fresh lawn green grass base with the same subtle speckle
grain as a classic grass top texture. Scattered evenly on top, small
soft pink petals as simple rounded pixel dashes in two pink tones,
covering roughly one third of the area, evenly spread, some crossing
the tile borders, plus a few tiny white five-dot flowers each with a
single gold center pixel.

Palette strictly: grass #4A7E2F #5D9C3C #74B84E, petals #F2C4D0
#E8A8BC, flower white #F6E8EC, flower center #F7DA7A.

CRITICAL: tiles seamlessly on all four edges, petals crossing borders
must line up.

No border, no frame, no whole flowers with stems, no branches, no
leaves, no dirt, no snow, no text, no letters, no watermark, no
gradient, no glow, no 3D render, no anti-aliasing.
```

## 负面提示词

```
stems, whole flowers, branches, leaves, dirt, snow, border, frame,
text, letters, watermark, gradient, glow, 3D render, anti-aliasing,
perspective
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 7 个色值量化
3. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝
- [ ] 草绿底与 `grass-top.png` 逐色一致（三绿不许换）
- [ ] 花瓣占约 1/3、散布均匀不扎堆
- [ ] 颗粒大小与 `grass-top.png` 一致
