# MKT-02 春节 LOGO 徽章

## 用途

**春节限定版徽章 logo**：官方徽章换红底、加灯笼与铜钱元素，
春节活动期间替换商店页/社媒头像。结构与 `logo-official` 完全同构，只换配色与角饰。

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
| 牌底红 | `#A83232` `#C43C3C` |
| 金边/铜钱 | `#DCAE3A` `#F7DA7A` |
| 灯笼红 | `#C43C3C` `#E05252` |
| 草绿 | `#74B84E` `#5D9C3C` `#4A7E2F` `#3B6626` |
| 泥土 | `#7A5A3C` `#5F4630` |

## 视觉描述

与官方徽章同构：圆角方牌 + 中央等距草方块，差异三处：

- 牌底换节日红（暗红为主、微亮红做层次）
- 牌面上两角各挂一盏**小圆红灯笼**（金顶盖 + 金流苏短线）
- 牌面下两角各一小摞**金铜钱**（两枚叠放）
- 草方块本体不动——它是品牌识别锚点
- 无文字（尤其不画汉字，AI 画不对）

## AI 提示词

```
A pixel art spring festival game logo emblem for a sandbox block game,
1024x1024, designed to be downscaled to 256x256 pixel art.

Style: chunky pixel art, flat shading, isometric 2:1 projection for the
cube, thick outlines, no perspective distortion, no realistic lighting.

Content: a rounded square badge plate in festive deep red with a gold rim
border. Centered on the plate, one large isometric grass block cube seen
from a 45 degree angle showing exactly three faces: a green diamond top
face, and two soil side faces whose upper quarter is green grass edge and
lower three quarters is brown soil, top face brightest, right face
darkest. Hanging at the top two corners of the plate, two small round red
lanterns with gold caps and short gold tassels; at the bottom two
corners, two small stacks of two gold coins each.

Palette strictly: plate reds #A83232 #C43C3C, rim and coins #DCAE3A
#F7DA7A, lantern reds #C43C3C #E05252, greens #74B84E #5D9C3C #4A7E2F
#3B6626, soil #7A5A3C #5F4630.

Everything outside the badge is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing, with an even magenta margin on
all sides.

No text, no letters, no words, no Chinese characters, no title, no
watermark, no glow, no sparkle, no cast shadow, no 3D render, no
photorealism, no gradient.
```

## 负面提示词

```
text, letters, words, Chinese characters, title, watermark, glow,
sparkle, shadow, 3D render, photorealism, gradient, transparent
background, checkerboard
```

## 后处理

1. 最近邻降采样到 256 × 256
2. 键控洋红为透明，去洋红边
3. 量化到上表色值
4. 检查灯笼/铜钱不与草方块或金边重叠

## 验收标准

- [ ] 尺寸恰为 256 × 256，Alpha 只有 0/255
- [ ] 图中不含任何文字/汉字
- [ ] 中央草方块与 `logo-official` 的是同一颗（色值、明暗关系一致）
- [ ] 灯笼、铜钱只做角饰，不喧宾夺主
