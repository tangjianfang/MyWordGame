# ARCH-04 村庄水井（概念图）

## 用途

第 3 波「村庄结构生成」的水井**建筑概念图**。水井是村庄的中心地标
（结构生成时放在广场正中），结构模板照它定井圈、立柱、小顶棚的方块布局；本图不直接进引擎。

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
| 圆石 | `#4A4A4A` `#7E7E7E` `#A3A3A3` |
| 苔藓绿 | `#4A7E2F` |
| 木质件 | `#8A6741` `#9C7549` `#B98D57` |
| 顶棚石板 | `#6E6E6E` `#8A8A8A` |
| 井水蓝 | `#2C5893` |
| 草地绿 | `#5D9C3C` |
| 描边/阴影 | `#241A11` |

## 视觉描述

圆石小广场中央的一口村庄水井：

- 圆形圆石井圈，井内一汪深蓝水面；北侧几块井石长苔藓
- 两根木立柱架起一个小石板人字顶棚
- 横梁垂下一根绳，吊着一只小木桶，悬在水面上方
- 一根立柱上挂灯笼；广场边缘有草和两小丛花
- 清晨亮光，构图居中、四面对称感强（它是地标，要一眼认出）

## AI 提示词

```
Voxel pixel art concept art of a village well for a sandbox block game,
1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, everything built from visible cubic blocks,
flat color faces, hard pixel edges, gentle isometric view, no realistic
perspective, no painterly shading.

Content: a small village well in the center of a cobblestone plaza. A
round cobble well ring with a dark blue water surface inside, two wooden
posts holding a small stone-shingled gable roof, a rope over a crossbar
with a small wooden bucket hanging above the water, a few mossy cobble
blocks on the north side, a lantern hanging on one post, grass and two
small flower patches around the plaza edge. Clear morning light, centered
symmetric composition.

Palette: cobble grays #4A4A4A #7E7E7E #A3A3A3, moss green #4A7E2F, plank
browns #8A6741 #9C7549 #B98D57, roof stones #6E6E6E #8A8A8A, water blue
#2C5893, grass green #5D9C3C, dark outline #241A11.

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
- [ ] 井水色与游戏内 `water` 贴图同系（`#2C5893` 深蓝）
- [ ] 井为主体居中，四周留广场，可作方形地标截图
- [ ] 块面平涂、硬边像素，无写实透视
