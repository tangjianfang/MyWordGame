# SCENE-08 群系全景 · Desert 沙漠

## 用途

群系图鉴五张全景之二：**Desert 沙漠**（biomes.json id 1，温度 0.9 / 湿度 0.2 /
树密度 0——除了绿洲棕榈，没有任何树）。白天、烈日、无文字。

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
| 沙亮/主/影 | `#EADDB4` `#D9C89A` `#A8926A` |
| 仙人掌绿 | `#448059` `#56996B` |
| 棕榈叶/干 | `#3F7A2E` / `#8A6741` |
| 绿洲水 | `#4E88CE` |
| 太阳 | `#FFF3C4` `#FFD84A` |
| 天空 | `#79A6FF` `#A9C8FF` |

## 视觉描述

- 大片暖沙丘，迎光面亮 `#EADDB4`、背光面 `#A8926A`，沙脊上有风吹细纹（像素短线）
- 几株方块仙人掌（青绿色、带粉花顶）；一丛枯灌木
- 左侧一小片绿洲：蓝水塘 + 两棵棕榈
- 右侧一座砂岩台地（mesa）台阶；高挂的淡金方形烈日，晴空万里无云（沙漠干燥）

## AI 提示词

```
Voxel pixel art panorama of the Desert biome for a sandbox block game
biome codex, 1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, wide distant view with the horizon in
the lower third, no realistic perspective, no painterly shading. The sky
is flat color bands.

Content: wide warm sand dunes with sunlit bright faces and shaded darker
faces, wind-ripple pixel streaks along the dune crests, a few blocky
green cacti with small pink blossoms on top, one dry brown bush, on the
left a small oasis pond with two palm trees, on the right a stepped
sandstone mesa, a high pale gold square sun in a clear cloudless blue
sky.

Palette: sands #EADDB4 #D9C89A #A8926A, cactus greens #448059 #56996B,
palm green #3F7A2E, palm trunk #8A6741, oasis water #4E88CE, sun #FFF3C4
#FFD84A, sky #79A6FF #A9C8FF.

No text, no letters, no logo, no UI, no watermark, no humans, no
buildings, no pyramids, no camels, no photorealism, no 3D render, no
blur, no smooth gradients, no vignette.
```

## 负面提示词

```
text, letters, logo, UI, watermark, humans, buildings, pyramids, camels,
skeletons, photorealism, 3D render, blur, smooth gradients, vignette
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 沙色与游戏 `sand`（`#D9C89A`）同系
- [ ] 除绿洲棕榈外无树（沙漠树密度 0）
- [ ] 天空无云（干燥）；无金字塔（神庙是独立结构图，别混进来）
- [ ] 无文字、无人形、无骆驼
