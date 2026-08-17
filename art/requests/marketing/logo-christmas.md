# MKT-03 圣诞 LOGO 徽章

## 用途

**圣诞限定版徽章 logo**：官方徽章换深蓝夜底、草方块戴雪帽、缀雪花点，
圣诞活动期间替换对外头像。与 `logo-official` / `logo-spring-festival` 三件套同构。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 256 × 256 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255（洋红键控） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 牌底夜蓝 | `#1E2A44` `#2C3A52` |
| 金边 | `#DCAE3A` `#F7DA7A` |
| 雪白 | `#F6FAFC` `#E8F0F4` |
| 草绿（雪下） | `#5D9C3C` `#4A7E2F` `#3B6626` |
| 泥土 | `#7A5A3C` `#5F4630` |

## 视觉描述

与官方徽章同构：圆角方牌 + 中央等距草方块，差异三处：

- 牌底换深夜蓝（与蓝图系列的图纸底色同族，冷而稳）
- 草方块**戴雪帽**：顶面整个盖白雪，两个侧面顶部 1/4 也是积雪，
  只露出侧面下段的草边与泥土——「冬天的同一颗方块」
- 牌面四角缀小雪花点（3-4 像素白点十字）
- 无文字、无圣诞老人、无驯鹿

## AI 提示词

```
A pixel art christmas game logo emblem for a sandbox block game,
1024x1024, designed to be downscaled to 256x256 pixel art.

Style: chunky pixel art, flat shading, isometric 2:1 projection for the
cube, thick outlines, no perspective distortion, no realistic lighting.

Content: a rounded square badge plate in deep night blue with a gold rim
border. Centered on the plate, one large isometric grass block cube seen
from a 45 degree angle showing exactly three faces: the diamond top face
is fully covered in white snow, and both side faces have white snow
covering their upper quarter with the remaining soil and grass edge
showing below, top face brightest, right face darkest. In the four
corners of the plate, small white snowflake dots of three or four pixels.

Palette strictly: plate blues #1E2A44 #2C3A52, rim #DCAE3A #F7DA7A, snow
#F6FAFC #E8F0F4, grass greens #5D9C3C #4A7E2F #3B6626, soil #7A5A3C
#5F4630.

Everything outside the badge is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing, with an even magenta margin on
all sides.

No text, no letters, no words, no title, no Santa, no reindeer, no
Christmas tree, no watermark, no glow, no sparkle, no cast shadow, no 3D
render, no photorealism, no gradient.
```

## 负面提示词

```
text, letters, words, title, Santa, reindeer, Christmas tree, watermark,
glow, sparkle, shadow, 3D render, photorealism, gradient, transparent
background, checkerboard
```

## 后处理

1. 最近邻降采样到 256 × 256
2. 键控洋红为透明，去洋红边
3. 量化到上表色值
4. 检查雪花点不与金边粘连

## 验收标准

- [ ] 尺寸恰为 256 × 256，Alpha 只有 0/255
- [ ] 图中不含任何文字，也不出现圣诞老人/驯鹿
- [ ] 草方块轮廓与 `logo-official` 完全同构（同一颗方块的冬装版）
- [ ] 雪、夜蓝、金边的三色关系清楚，缩到 64×64 仍可读
