# MKT-06 截图分享边框

## 用途

玩家/官方**晒截图用的装饰边框**（16:9）：分享到社交平台前把截图衬在框内。
中央整个键控为透明，运行时/脚本把游戏截图垫在框下面。
边框视觉与游戏 UI 同款（`button`/`panel` 暖灰系）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 576（16:9） |
| 生成尺寸 | 1024 × 576（同尺寸直出） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255（**中央大面积洋红键控**） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 边框深/主/亮 | `#443D34` `#6B6155` `#8B7F6F` |
| 金线 | `#DCAE3A` |
| 角饰草方块 | `#74B84E` `#5D9C3C` `#4A7E2F` `#7A5A3C` |
| 藤蔓绿 | `#4A7E2F` |
| 花 | `#C43C3C` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

- 四周一圈**约 6% 画面厚**的斜切暖灰边框（上下左右同宽，像素倒角），
  内缘一条 1-2 像素金线
- 四个角各嵌一个**小号等距草方块**（与 logo 徽章里的同一颗）
- 底边一行反复的**小绿藤 + 红点小花**
- **中央整个区域是纯洋红**（键控后透明，垫截图）
- 洋红不入侵边框本体，边框元素不越出边框带

## AI 提示词

```
A pixel art decorative frame border for game screenshots, 1024x576
widescreen, flat pixel art, no perspective, hard edges.

Content: a border band about 6 percent thick around all four edges of
the image. The band is beveled dark warm brown panel style with a thin
gold inner line. On each of the four corners sits one small isometric
grass block cube showing three faces, green diamond top and brown soil
sides. Along the bottom edge runs a repeating row of tiny green pixel
vines with small red flower dots. The ENTIRE center area inside the
frame is flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing — it will be keyed out transparent.

Palette strictly: border #443D34 #6B6155 #8B7F6F, gold line #DCAE3A,
grass blocks #74B84E #5D9C3C #4A7E2F #7A5A3C, vines #4A7E2F, flowers
#C43C3C, center magenta #FF00FF.

No text, no letters, no logo, no watermark, no gradient, no glow, no 3D
render, no anti-aliasing, no magenta inside the border band itself, no
objects crossing into the magenta center.
```

## 负面提示词

```
text, letters, logo, watermark, gradient, glow, 3D render,
anti-aliasing, objects in center, semi-transparent pixels
```

## 后处理

1. 不降采样（1024 × 576 直出直用）
2. 键控洋红为透明（中央占比 > 50% 的像素 Alpha = 0），去洋红边
3. 按上表量化边框色值
4. 校验四边边框厚度一致、金线闭合

## 验收标准

- [ ] 尺寸恰为 1024 × 576，32 位 PNG，Alpha 只有 0/255
- [ ] 中央透明区是完整矩形（截图垫入后无遮挡）
- [ ] 四角草方块与徽章/logo 里的同款
- [ ] 边框不带任何文字；边框元素不出框、洋红不进框
