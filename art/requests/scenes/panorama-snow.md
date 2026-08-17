# SCENE-11 群系全景 · Snow 雪原

## 用途

群系图鉴五张全景之五：**Snow 雪原**（biomes.json id 4，温度 0.1 / 湿度 0.4 /
树密度 1——只有零星针叶）。白天、柔和冬阳、无文字。与主菜单的极光夜景区分：
这张是**白天**的安静雪原。

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
| 雪 | `#F6FAFC` `#E8F0F4` `#D0DEE8` |
| 湖冰 | `#A9CBD6` `#C8E2EA` |
| 针叶 | `#1E3B2A` `#2A5038` |
| 冰屋门暖光 | `#F79B22` |
| 冬阳 | `#FFF3C4` |
| 冬空 | `#A9C8FF` `#D6E6FF` |

## 视觉描述

- 平缓的白色雪原，白与淡蓝的方块雪丘交替，天上飘着细小像素雪点
- 一排戴厚重雪帽的深绿针叶树（树密度 1：零星几棵）
- 一片冻湖：淡蓝冰面 + 一道裂缝线
- 一座雪块小冰屋，门洞透出一点暖橙光（雪原里唯一暖色，画面焦点）
- 低垂柔和的淡色冬阳；天空是淡蓝白调的平涂色带

## AI 提示词

```
Voxel pixel art panorama of the Snow biome for a sandbox block game
biome codex, 1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, wide distant view with the horizon in
the lower third, no realistic perspective, no painterly shading. The sky
is flat color bands.

Content: a calm snowfield of white and pale blue blocky drifts with small
pixel snowflakes falling, a few dark green pine trees with thick snow
caps standing sparsely, a frozen lake with a pale blue ice surface and
one crack line, one small igloo built of snow blocks with a doorway
glowing warm orange as the only warm accent, a low pale winter sun, soft
pale blue-white sky.

Palette: snow #F6FAFC #E8F0F4 #D0DEE8, ice #A9CBD6 #C8E2EA, pines #1E3B2A
#2A5038, warm doorway #F79B22, pale sun #FFF3C4, sky #A9C8FF #D6E6FF.

No text, no letters, no logo, no UI, no watermark, no humans, no
buildings other than the igloo, no aurora, no night, no photorealism, no
3D render, no blur, no smooth gradients, no vignette.
```

## 负面提示词

```
text, letters, logo, UI, watermark, humans, aurora, night sky,
photorealism, 3D render, blur, smooth gradients, vignette
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 是白天冬景，**无极光无夜空**（那是 `menu-snow-aurora` 的画面）
- [ ] 冰屋门洞的暖橙光是全图唯一暖色焦点
- [ ] 针叶零星几棵（树密度 1），不成林
- [ ] 无文字、无人形
