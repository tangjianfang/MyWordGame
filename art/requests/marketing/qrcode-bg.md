# MKT-08 二维码底板

## 用途

商店页/宣传单**放二维码的底板**：中央一大块纯色净区垫二维码，
四周是游戏风格的装饰边。二维码本体由工具生成叠加（二维码绝不能让 AI 画）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 512 × 512 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（整幅满铺） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 中央净区（米白） | `#F2E6C8` |
| 边框深/主/亮 | `#443D34` `#6B6155` `#8B7F6F` |
| 藤蔓绿 | `#4A7E2F` `#5D9C3C` |
| 花 | `#C43C3C` `#F7DA7A` |
| 角落金币 | `#DCAE3A` `#F7DA7A` |

净区用米白（与游戏标题文字色一致），二维码叠上去对比度足够。

## 视觉描述

- **中央约 70% 面积是一整块纯米白净区**——无任何笔触、无阴影、无纹理，
  二维码的家
- 净区外一圈暖棕边框带，四角像素倒角
- 左右两边各一条攀爬的小绿藤（几片像素叶）
- 底边一排小像素花（红/黄交替）；四角附近各一枚小金币
- 所有装饰**只在边框带内**，绝不侵入净区

## AI 提示词

```
A pixel art background plate for displaying a QR code on a store page,
1024x1024, designed to be downscaled to 512x512 pixel art.

Style: chunky pixel art, flat shading, hard edges, warm and friendly, no
perspective.

Content: a plain light cream flat square panel filling the center 70
percent of the image, perfectly flat with no marks, no texture and no
shading — this area stays completely empty for a QR code placed on top
later. Around it a decorative border band of warm dark brown panel with
beveled pixel corners. Along the left and right edges small green pixel
vines climb with a few leaf pairs. Along the bottom edge a row of tiny
red and yellow pixel flowers. Near the four corners, four small gold
coins. All decorations stay strictly inside the border band.

Palette strictly: cream #F2E6C8, border #443D34 #6B6155 #8B7F6F, vines
#4A7E2F #5D9C3C, flowers #C43C3C #F7DA7A, coins #DCAE3A #F7DA7A.

No text, no letters, no numbers, no QR code pattern, no squares in the
center, no watermark, no logo, no gradient, no glow, no 3D render, no
anti-aliasing, no marks inside the cream center panel.
```

## 负面提示词

```
text, letters, numbers, QR code pattern, black squares, watermark,
logo, gradient, glow, 3D render, anti-aliasing, marks in center
```

## 后处理

1. 最近邻降采样到 512 × 512
2. 按上表量化（净区必须量化为单一 `#F2E6C8`）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 512 × 512
- [ ] 中央净区为**单一色值的正方形**，无任何杂点（程序可断言）
- [ ] 净区占画面 ≥ 65%，能容下标准二维码 + 静区
- [ ] 装饰元素全部在边框带内，无文字、无假二维码
