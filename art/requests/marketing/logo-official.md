# MKT-01 官方 LOGO 徽章

## 用途

对外宣传的**官方徽章版 logo**：深底金边圆角方牌 + 等距草方块。
文字部分（游戏名）照 `ui/title-logo.md` 的结论**绝不交给 AI**，
由设计侧像素字体叠加——本资源只出徽章图形本体。
用于商店页头像、社交媒体头像、视频角标。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 256 × 256 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255（洋红键控） |
| 颜色数 | 8 – 12 色 |

## 调色板

草方块部分与 `ui/title-logo.md`（title-decor-block）完全同款，保证徽章与主菜单装饰是同一个方块：

| 用途 | HEX |
| --- | --- |
| 牌底深 | `#241A11` |
| 牌底棕 | `#3A2E20` |
| 金边 | `#DCAE3A` `#F7DA7A` |
| 草绿 | `#74B84E` `#5D9C3C` `#4A7E2F` `#3B6626` |
| 泥土 | `#7A5A3C` `#5F4630` |

## 视觉描述

- 圆角方形深棕底牌，外圈 2-4 像素金边，左上边框带一段亮金高光
- 牌面中央一个大号**等距草方块**：顶面菱形草绿最亮、左面中、右面最暗，
  侧面「上 1/4 草边 + 下 3/4 泥土」——与主菜单装饰方块完全同构
- 徽章外围留均匀洋红边距
- 无文字、无光效

## AI 提示词

```
A pixel art game logo emblem for a sandbox block game, 1024x1024,
designed to be downscaled to 256x256 pixel art.

Style: chunky pixel art, flat shading, isometric 2:1 projection for the
cube, thick outlines, no perspective distortion, no realistic lighting.

Content: a rounded square badge plate in deep charcoal brown with a gold
rim border and one bright gold highlight segment on its upper left rim.
Centered on the plate, one large isometric grass block cube seen from a
45 degree angle showing exactly three faces: a green diamond top face,
and two soil side faces whose upper quarter is green grass edge and
lower three quarters is brown soil. The top face is the brightest, the
left face mid tone, the right face the darkest.

Palette strictly: plate #241A11 #3A2E20, rim #DCAE3A #F7DA7A, greens
#74B84E #5D9C3C #4A7E2F #3B6626, soil #7A5A3C #5F4630.

Everything outside the badge is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing, with an even magenta margin on
all sides.

No text, no letters, no words, no title, no watermark, no glow, no
sparkle, no cast shadow, no reflection, no 3D render, no photorealism,
no gradient.
```

## 负面提示词

```
text, letters, words, title, watermark, glow, sparkle, shadow,
reflection, 3D render, photorealism, gradient, transparent background,
checkerboard
```

## 后处理

1. 最近邻降采样到 256 × 256
2. 键控洋红为透明，去洋红边
3. 量化到上表 9 个色值
4. 检查金边闭合、圆角对称

## 验收标准

- [ ] 尺寸恰为 256 × 256，32 位 PNG，Alpha 只有 0/255
- [ ] 图中不含任何文字
- [ ] 草方块三面明暗关系与 `title-decor-block` 一致，并排认得出同一材质
- [ ] 缩到 64×64（社媒小头像尺寸）后徽章轮廓仍清晰
