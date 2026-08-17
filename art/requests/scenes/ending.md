# SCENE-06 章节过场 · 终局

## 用途

章节过场图之三：**通关终局画面**。击败机元守卫、走完装备升级链后的结局定格图，
配引擎侧像素字体的致谢文字（图里不画字）。全屏展示。

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
| 草原/森林绿 | `#4A7E2F` `#5D9C3C` |
| 河水 | `#3A6FB5` |
| 雪/云海 | `#F6FAFC` `#E8F0F4` |
| 山石 | `#7E7E7E` |
| 朝阳 | `#FFD84A` `#F2A81E` |
| 晨空色带 | `#F7DA7A` `#FFF3C4` `#79A6FF` |
| 玩家剪影蓝 | `#2C5893` `#3A6FB5` |

## 视觉描述

登顶的一刻，从背后看小小的玩家角色：

- 玩家方块小人背对镜头站在雪顶山巅，身旁插着一把钻石镐
- 脚下是展开的黎明世界：草原上的村庄升起几缕细烟、蓝河、深色森林、
  远处沙丘与雪山
- 大幅淡金方形朝阳贴着地平线升起；山顶下铺一层扁平方块云海
- 几群白色像素小鸟横过天空
- 天空横向平涂色带：淡金 → 米白 → 淡蓝

## AI 提示词

```
Voxel pixel art scene for a game ending interstitial: a small hero on a
mountain summit at sunrise looking over a vast blocky world, 1024x1024,
used directly as final art with no downscaling.

Style: chunky voxel pixel art, everything built from visible cubic
blocks, flat color faces, hard pixel edges, view from behind the hero, no
realistic perspective, no painterly shading. The sky is flat horizontal
bands with dithered edges.

Content: a tiny blocky player character seen from behind, standing on a
snow-capped stone summit, a diamond pickaxe planted in the snow beside
them. Below spreads a wide dawn world: green plains with a village
sending up thin smoke threads, a blue river, a dark forest, distant dunes
and snowy peaks. A big pale gold square sun rising on the horizon, flocks
of white pixel birds, a sea of flat blocky clouds below the summit.

Palette: greens #4A7E2F #5D9C3C, water #3A6FB5, snow and clouds #F6FAFC
#E8F0F4, stone #7E7E7E, sun #FFD84A #F2A81E, sky bands #F7DA7A #FFF3C4
#79A6FF, hero blues #2C5893 #3A6FB5.

No text, no letters, no numbers, no watermark, no logo, no UI, no
photorealism, no 3D render, no blur, no smooth gradients, no vignette.
```

## 负面提示词

```
text, letters, numbers, watermark, logo, UI, credits, photorealism, 3D
render, blur, smooth gradients, vignette, lens flare, face visible
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 玩家只见背影，不露正脸（正脸留给游戏内皮肤）
- [ ] 钻石镐是青色系（`#4CC6C4` 族），与矿石配色一致
- [ ] 画面元素能对上游戏内容：村庄/河/森林/雪山/云海
- [ ] 画面无文字（致谢文字引擎侧叠加）
