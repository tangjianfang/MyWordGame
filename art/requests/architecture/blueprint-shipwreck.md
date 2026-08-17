# ARCH-06 沉船蓝图

## 用途

第 3 波「结构生成」的沉船**结构蓝图**（水域结构：浅海沉船，带宝藏箱）。
风格与 `blueprint-mine` 同系列：深蓝图纸底 + 实体结构 + 幽灵线框地形，方便成套对照。
本图不直接进引擎。

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
| 图纸底 | `#1E2A44` |
| 网格线 | `#31446B` |
| 船体深木 | `#634C33` `#8A6741` `#9C7549` |
| 帆布米白 | `#EADDB4` |
| 海沙金 | `#D9C89A` |
| 宝藏金 | `#DCAE3A` |
| 海草绿 | `#3F7A2E` |
| 幽灵线框 | `#4E88CE` |

## 视觉描述

深蓝方格图纸上的海底沉船剖面：

- 一艘断裂的木帆船倾斜躺在沙质海床上：船壳破洞、断桅杆挂着残帆、瞭望斗还在
- 半埋进沙里的宝箱翻出几块金块
- 船边稀疏几缕海草；**不画鱼**（结构图只画结构）
- 水面线与下方沙丘用浅蓝幽灵线框勾出，不实心
- 无文字、无箭头

## AI 提示词

```
Voxel pixel art structure blueprint of a sunken shipwreck for a sandbox
block game, 1024x1024, used directly as final art with no downscaling.

Style: technical blueprint look drawn in chunky voxel pixel art.
Background is flat deep slate blue graph paper with thin lighter blue
grid lines. The structure is drawn as solid colored voxel blocks in
gentle isometric view; the water surface and seabed are only faint
outlined ghost cubes.

Content: a broken wooden sailing ship lying tilted on a sandy seabed:
hull planks with a gaping hole, a snapped mast with a torn cream sail,
the crow nest still on the mast, a treasure chest half buried in sand
spilling gold blocks, sparse seaweed strands near the bow. Ghost cube
outlines show the water surface line above and sand dunes below.

Palette: blueprint blue #1E2A44 background, grid line #31446B, wood
browns #634C33 #8A6741 #9C7549, sail cream #EADDB4, sand gold #D9C89A,
treasure gold #DCAE3A, seaweed green #3F7A2E, ghost outline #4E88CE.

No text, no letters, no numbers, no arrows, no labels, no watermark, no
logo, no UI, no humans, no fish, no photorealism, no 3D render, no blur,
no smooth gradients, no anti-aliasing.
```

## 负面提示词

```
text, letters, numbers, arrows, labels, annotations, watermark, logo, UI,
humans, fish, sea creatures, photorealism, 3D render, blur, smooth
gradients, anti-aliasing
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 画面中不存在任何文字/字母/数字/箭头
- [ ] 与 `blueprint-mine` 并排看是同一套图纸风格（同底色、同网格、同幽灵线框色）
- [ ] 船体木色与游戏内 log/planks 同系
- [ ] 画面里没有鱼/海洋生物（结构图只画结构）
