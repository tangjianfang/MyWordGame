# SCENE-03 主菜单背景 · 丛林日落

## 用途

主菜单三选一背景之三：**丛林日落**。丛林是 m11 新增树种（jungle）对应的群系方向，
这张背景为后续丛林内容预热。上叠标题与按钮。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无 |

## 调色板

叶绿沿用游戏 `leaves` 深冷系，日落暖色与岩浆橙同族：

| 用途 | HEX |
| --- | --- |
| 丛林层绿 | `#24491C` `#2F5D24` `#3F7A2E` `#52963B` |
| 河面落日反光 | `#DCAE3A` |
| 河水 | `#3A6FB5` |
| 太阳 | `#FFD84A` `#F79B22` |
| 天空色带（下→上） | `#8A2400` `#F79B22` `#F7DA7A` `#7B5CB8` |

## 视觉描述

- 层层剪影的密林：前景深暗大叶、中景中绿、远山亮绿，三层拉开纵深
- 大块方块叶、垂下的藤蔓，一侧一棵高大的丛林巨树
- 一条河蜿蜒穿过，水面映着落日的金橙色
- 方形太阳半沉在林冠后；天空调成下深橙 → 琥珀 → 淡金 → 上端灰紫的**平涂色带**
- 几只方块鹦鹉剪影；底部 1/6 是更暗的前景大叶，留给按钮

## AI 提示词

```
Voxel pixel art scene of a blocky jungle at sunset for a sandbox game
main menu background, 1024x1024, used directly as final art with no
downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, layered silhouette depth, no
realistic perspective. The sky is flat horizontal bands with dithered
edges, not smooth gradients.

Content: a dense voxel jungle in layered silhouettes from dark front
leaves to lighter distant canopy, big blocky leaves, hanging vines, one
tall jungle tree on the right side, a winding river glinting the sunset
gold color, a few blocky parrot silhouettes flying. A low square sun disc
half hidden behind the canopy. Sunset sky bands from bottom to top: deep
orange, amber, pale gold, dusty lavender. The bottom sixth is darker
foreground leaves kept simple.

Palette: greens #24491C #2F5D24 #3F7A2E #52963B, river #3A6FB5 with gold
glint #DCAE3A, sun #FFD84A #F79B22, sky bands #8A2400 #F79B22 #F7DA7A
#7B5CB8.

No text, no letters, no logo, no UI, no buttons, no watermark, no humans,
no photorealism, no 3D render, no blur, no smooth gradient, no vignette.
```

## 负面提示词

```
text, letters, logo, UI, buttons, watermark, humans, photorealism, 3D
render, blur, smooth gradient, vignette, lens flare, realistic jungle
photo
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 三层剪影纵深清楚（前景最暗、远山最亮）
- [ ] 叶绿与游戏 `leaves` 同系，不偏蓝不偏黄
- [ ] 天空是色带不是渐变；太阳无光晕
- [ ] 底部 1/6 简洁；画面无文字
