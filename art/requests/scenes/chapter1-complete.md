# SCENE-04 章节过场 · 第一章完成

## 用途

章节过场图之一：**第一章「活过第一夜」完成**。第一章任务链是
挖→合→烧→活过夜（`quests/chapter1.json`），这张图定格「熬过首夜后的清晨」，
任务链完成时全屏展示 2-3 秒。

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
| 木板 | `#8A6741` `#9C7549` `#B98D57` |
| 茅草顶 | `#C8A24E` `#E0BE6A` |
| 草地 | `#4A7E2F` `#5D9C3C` |
| 余烬橙 | `#D64B0A` `#F79B22` |
| 晨空 | `#79A6FF` `#FFF3C4` |
| 炊烟灰 | `#8B8B8B` |
| 描边 | `#241A11` |

## 视觉描述

清晨的小空地，安全感和成就感：

- 一座小木板茅草屋，门开着，屋内透出暖色炉光
- 石烟囱升起细细的方块烟；屋外一滩将熄的篝火只剩橙色余烬
- 屋边放着工作台和小熔炉（首章「合」「烧」两步的呼应）
- 一小圈栅栏围着刚冒芽的麦苗；一只羊和一只鸡在不远处
- 右侧低垂淡金晨光，长长的柔和方块投影；天上几只白色小鸟

## AI 提示词

```
Voxel pixel art scene for a chapter completion interstitial: the morning
after surviving the first night in a sandbox block game, 1024x1024, used
directly as final art with no downscaling.

Style: chunky voxel pixel art, everything built from visible cubic blocks,
flat color faces, hard pixel edges, gentle isometric view, no realistic
perspective, no painterly shading.

Content: a small cozy plank cabin with a thatched roof on a grass
clearing, front door open showing a warm hearth glow inside, thin blocky
pixel smoke rising from the stone chimney, a dying campfire with faint
orange embers outside, a crafting table and a small furnace beside the
cabin, a fenced tiny wheat sprout patch, one blocky sheep and one blocky
chicken nearby, low pale gold morning sun on the right casting long soft
blocky shadows, a few white pixel birds in the sky.

Palette: planks #8A6741 #9C7549 #B98D57, thatch #C8A24E #E0BE6A, grass
#4A7E2F #5D9C3C, ember orange #D64B0A #F79B22, sky #79A6FF #FFF3C4,
smoke gray #8B8B8B, outline #241A11.

No text, no letters, no numbers, no watermark, no logo, no UI, no humans,
no photorealism, no 3D render, no blur, no smooth gradients, no vignette.
```

## 负面提示词

```
text, letters, numbers, watermark, logo, UI, humans, characters,
photorealism, 3D render, blur, smooth gradients, vignette
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 工作台与熔炉在画面里可辨认（首章链的呼应物）
- [ ] 篝火是余烬不是明火（夜已经熬过去了）
- [ ] 羊/鸡造型与游戏内 `MobModels` 同款方块风
- [ ] 画面无文字、无人形
