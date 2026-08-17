# SCENE-07 群系全景 · Plains 平原

## 用途

群系图鉴（codex）五张全景之一：**Plains 平原**（biomes.json id 0，
温度 0.5 / 湿度 0.5 / 树密度 8）。图鉴页头图，也是「这个群系长什么样」的权威参照。
白天、晴天、无文字。

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
| 草地绿 | `#4A7E2F` `#5D9C3C` `#74B84E` |
| 树冠 | `#2F5D24` `#3F7A2E` |
| 水 | `#3A6FB5` `#4E88CE` |
| 花 | `#C43C3C` `#F7DA7A` |
| 泥土路 | `#7A5A3C` |
| 天空 | `#79A6FF` `#A9C8FF` |
| 云 | `#E7F4F8` |

## 视觉描述

- 宽阔安静的草原：台阶状方块草丘，地平线压在画面下 1/3
- 稀疏的圆滚滚体素橡树（对应树密度 8：少但不空）
- 红/黄像素小花成小丛，一只羊一头猪在吃草，一方小池塘
- 一条土路横向穿到地平线；几朵扁平方块云

## AI 提示词

```
Voxel pixel art panorama of the Plains biome for a sandbox block game
biome codex, 1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, wide distant view with the horizon in
the lower third, no realistic perspective, no painterly shading. The sky
is flat color bands.

Content: a wide calm grass plain in fresh green with gently stepped
blocky hills, a few round voxel oak trees scattered sparsely, small
patches of red and yellow pixel flowers, one blocky sheep and one blocky
pig grazing, a small pond of blue water, a dirt path crossing to the
horizon, a few flat blocky clouds in a clear blue sky.

Palette: greens #4A7E2F #5D9C3C #74B84E, tree crowns #2F5D24 #3F7A2E,
water #3A6FB5 #4E88CE, flowers #C43C3C #F7DA7A, dirt path #7A5A3C, sky
#79A6FF #A9C8FF, clouds #E7F4F8.

No text, no letters, no logo, no UI, no watermark, no humans, no
photorealism, no 3D render, no blur, no smooth gradients, no vignette.
```

## 负面提示词

```
text, letters, logo, UI, watermark, humans, buildings, photorealism, 3D
render, blur, smooth gradients, vignette
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 草绿主色 = `grass-top` 的 `#5D9C3C` 一眼同款
- [ ] 树少而疏（平原树密度低），不能画成森林
- [ ] 无建筑、无文字、无人形
