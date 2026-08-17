# MKT-05 商店页主视觉横幅

## 用途

商店页/官网首屏**主视觉横幅**：一张能代表整个游戏的世界全景图。
游戏名文字与「进入下载」按钮由网页/商店后台叠加——图里**不画字**，
顶部 40% 留成安静天空给标题用。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无 |

## 调色板

集游戏各群系主色于一图（都与游戏内贴图同源）：

| 用途 | HEX |
| --- | --- |
| 草绿 | `#4A7E2F` `#5D9C3C` `#74B84E` |
| 水 | `#3A6FB5` `#4E88CE` |
| 木建筑 | `#8A6741` `#9C7549` |
| 山石/雪 | `#7E7E7E` / `#F6FAFC` |
| 太阳 | `#FFD84A` |
| 天空 | `#79A6FF` `#A9C8FF` |

## 视觉描述

明亮欢快的体素世界大观景：

- 前景：绿色方块草丘、橡树、花丛，几只方块羊和一头猪
- 中景：山坡上一个小村庄（木板房+烟囱炊烟），蓝河上一座小木桥
- 远景：灰山戴雪帽、方形金色太阳、扁平方块云
- **顶部 40% 是干净简单的大面积天空**（标题区），只有少量云与太阳
- 一切元素都能在游戏里找到出处——这是「游戏画面可信度」的承诺

## AI 提示词

```
Voxel pixel art store banner key art for a sandbox block game, 1024x1024,
used directly as final art with no downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, wide vista, no realistic perspective,
no painterly shading. The sky is flat color bands.

Content: a bright cheerful voxel world vista. Foreground: green blocky
hills with oak trees and flower patches, a few blocky sheep and one pig.
Midground: a small village of plank houses with smoking chimneys on a
hill, a blue river crossed by a small wooden bridge. Background: gray
mountains with snow caps, a square gold sun, flat blocky clouds. The top
40 percent of the image is calm simple mostly empty sky kept as reserved
space for a title added later.

Palette: greens #4A7E2F #5D9C3C #74B84E, water #3A6FB5 #4E88CE, planks
#8A6741 #9C7549, stone #7E7E7E, snow #F6FAFC, sun #FFD84A, sky #79A6FF
#A9C8FF.

No text, no letters, no numbers, no title, no logo, no UI, no buttons,
no watermark, no humans, no photorealism, no 3D render, no blur, no
smooth gradients.
```

## 负面提示词

```
text, letters, numbers, title, logo, UI, buttons, watermark, humans,
photorealism, 3D render, blur, smooth gradients
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 顶部 40% 无复杂细节，叠加游戏名后依然干净
- [ ] 村庄/桥/羊/猪等元素与游戏内容对得上（不放游戏里没有的东西）
- [ ] 天空色带无渐变；太阳无光晕
- [ ] 画面无任何文字与 UI 元素
