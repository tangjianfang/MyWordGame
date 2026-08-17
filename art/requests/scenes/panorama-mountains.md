# SCENE-10 群系全景 · Mountains 山地

## 用途

群系图鉴五张全景之四：**Mountains 山地**（biomes.json id 3，温度 0.2 / 湿度 0.1 /
caveMultiplier 2.0——洞穴最多，画面可暗示矿脉露头）。白天、无文字。

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
| 山石灰 | `#4A4A4A` `#6E6E6E` `#8A8A8A` `#A3A3A3` |
| 雪帽 | `#F6FAFC` `#E8F0F4` |
| 针叶 | `#1E3B2A` `#2A5038` |
| 瀑布/溪水 | `#3A6FB5` `#4E88CE` |
| 谷底草 | `#5D9C3C` |
| 矿脉青/金 | `#4CC6C4` `#DCAE3A` |
| 天空 | `#79A6FF` `#A9C8FF` |

## 视觉描述

- 高耸的灰色方块石峰群，山顶戴白雪帽，崖壁是台阶状方块断面
- 一道像素瀑布从崖壁跌落，落进山脚水潭，一条溪流向地平线流走
- 较低山坡散布深绿针叶树；一处碎石坡
- 一处断崖露头嵌着青色与金色的矿脉小块（呼应 m10 挖矿主线）
- 天上一只老鹰剪影；天空平涂色带

## AI 提示词

```
Voxel pixel art panorama of the Mountains biome for a sandbox block game
biome codex, 1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, wide distant view with the horizon in
the lower third, no realistic perspective, no painterly shading.

Content: tall gray blocky stone peaks with white snow caps, stepped cliff
faces, one pixel waterfall of blue water falling from a cliff into a pool
at the base with a stream flowing away to the horizon, sparse dark green
pine trees on the lower slopes, a rubble scree slope, one cliff cutaway
showing small cyan and gold ore veins embedded in the stone, one eagle
silhouette high in the sky.

Palette: stone grays #4A4A4A #6E6E6E #8A8A8A #A3A3A3, snow caps #F6FAFC
#E8F0F4, pines #1E3B2A #2A5038, water #3A6FB5 #4E88CE, valley grass
#5D9C3C, ore cyan #4CC6C4, ore gold #DCAE3A, sky #79A6FF #A9C8FF.

No text, no letters, no logo, no UI, no watermark, no humans, no
buildings, no photorealism, no 3D render, no blur, no smooth gradients,
no vignette.
```

## 负面提示词

```
text, letters, logo, UI, watermark, humans, buildings, photorealism, 3D
render, blur, smooth gradients, vignette, realistic alpine photo
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 石灰与游戏 `stone`（`#8A8A8A`）同系；矿脉青/金与 diamond/gold 矿石色一致
- [ ] 只有山顶戴雪，山腰是裸岩+针叶（区别于 Snow 群系的全白）
- [ ] 无文字、无人形
