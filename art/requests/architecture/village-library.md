# ARCH-03 村庄图书馆（概念图）

## 用途

第 3 波「村庄结构生成」的图书馆**建筑概念图**。图书馆是村民职业点之一
（librarian 交易场所），结构模板照它定两层小楼的开窗与书架布局；本图不直接进引擎。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 木板墙 | `#8A6741` `#9C7549` `#B98D57` |
| 石板屋顶 | `#6E6E6E` `#8A8A8A` `#A3A3A3` |
| 玻璃青白 | `#C8E2EA` |
| 书脊彩色 | `#A05242` `#DCAE3A` `#3F7A2E` |
| 草地绿 | `#5D9C3C` |
| 描边/阴影 | `#241A11` |

## 视觉描述

一座两层小楼，村庄里最高的建筑：

- 木板墙 + 深色橡木角柱，陡峭的石板人字顶，带小圆阁楼窗
- 二楼两扇大玻璃窗，窗内露出书架——书脊画成简单的彩色竖条
- 底层门开着，能看到阅读桌和一个小地球仪
- 门边挂一盏灯笼，窗台摆两盆小花；午后安静暖光

## AI 提示词

```
Voxel pixel art concept art of a village library house for a sandbox block
game, 1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, everything built from visible cubic blocks,
flat color faces, hard pixel edges, gentle isometric view, no realistic
perspective, no painterly shading.

Content: a taller two-story village library. Warm plank walls with dark
oak corner beams, a steep stone gable roof, a small round attic window,
two large glass windows on the upper floor revealing colorful book spines
inside drawn as simple vertical color bars, an open ground-floor doorway
showing a reading desk and a small globe, a hanging lantern by the door,
flower pots on the windowsills. Quiet warm afternoon light.

Palette: plank browns #8A6741 #9C7549 #B98D57, stone grays #6E6E6E
#8A8A8A #A3A3A3, glass pale cyan #C8E2EA, book colors #A05242 #DCAE3A
#3F7A2E, grass green #5D9C3C, dark outline #241A11.

No text, no letters, no numbers, no watermark, no logo, no UI, no humans,
no photorealism, no 3D render, no blur, no smooth gradients, no
anti-aliasing.
```

## 负面提示词

```
text, letters, numbers, watermark, logo, UI, humans, characters,
photorealism, 3D render, blur, smooth gradients, anti-aliasing, realistic
perspective, vanishing point
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 画面中不存在任何文字/字母/数字——书一律画成色条，不画书名
- [ ] 玻璃窗色与游戏内 `glass` 贴图同系（青白，不偏深蓝）
- [ ] 屋顶是石灰色系，与铁匠铺的砖红顶一眼可区分
- [ ] 块面平涂、硬边像素，无写实透视
