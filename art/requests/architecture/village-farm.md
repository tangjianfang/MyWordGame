# ARCH-02 村庄农舍（概念图）

## 用途

第 3 波「村庄结构生成」的农舍+农田**建筑概念图**，兼作第 1 波农业玩法的场景基调图。
结构模板照它定农舍轮廓与麦田、栅栏、水渠的配比；本图不直接进引擎。

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
| 麦金 | `#DCAE3A` `#F7DA7A` |
| 茅草屋顶 | `#C8A24E` `#E0BE6A` |
| 木板墙 | `#8A6741` `#9C7549` `#B98D57` |
| 草地绿 | `#5D9C3C` `#4A7E2F` |
| 水渠蓝 | `#3A6FB5` |
| 砖红点缀 | `#8B4433` |
| 描边/阴影 | `#241A11` |

## 视觉描述

一座舒适农舍被农田环绕：

- 木板墙 + 深色梁柱 + 金黄茅草人字顶，小砖砌烟囱
- 屋前三块整齐的金色麦田，中间夹一条灌了水的湿润耕地水渠
- 木栅栏围住田地，旁边有干草垛方块与一捆麦子
- 栅栏边站着一只鸡和一头猪（游戏内既有生物，方块造型）
- 正午亮光、几朵方块小云

## AI 提示词

```
Voxel pixel art concept art of a village farmhouse with crop fields for a
sandbox block game, 1024x1024, used directly as final art with no
downscaling.

Style: chunky voxel pixel art, everything built from visible cubic blocks,
flat color faces, hard pixel edges, gentle isometric view, no realistic
perspective, no painterly shading.

Content: a cozy farmhouse with plank walls, dark timber beams and a golden
thatched gable roof, a small brick chimney. Around it three tidy golden
wheat field plots with a thin blue water channel between them, a wooden
fence, haystack blocks and one wheat bundle, a small compost corner. A
blocky chicken and a blocky pig stand near the fence. Bright midday light,
a few flat blocky clouds.

Palette: wheat golds #DCAE3A #F7DA7A, thatch #C8A24E #E0BE6A, plank browns
#8A6741 #9C7549 #B98D57, grass greens #4A7E2F #5D9C3C, water blue #3A6FB5,
brick red #8B4433, dark outline #241A11.

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
- [ ] 画面中不存在任何文字/字母/数字
- [ ] 麦田金色与 items 表麦子/面包色系一致，不偏柠檬黄
- [ ] 鸡和猪的造型与游戏内 `MobModels` 五生物同款方块风
- [ ] 块面平涂、硬边像素，无写实透视
