# ARCH-01 村庄铁匠铺（概念图）

## 用途

第 3 波「村庄结构生成」的铁匠铺**建筑概念图**。它是结构模板（逐层方块布局）的视觉基准：
模板设计者照这张图定外轮廓、材质配比与配色，游戏内建筑由真实方块拼出，本图不直接进引擎。
挂在需求树上供父子评审与对照，也用于向孩子解释「铁匠铺长什么样」。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无（整幅满铺，不键控洋红） |

## 调色板

以游戏全局调色板为基准，允许同系过渡色至多 3 个：

| 用途 | HEX |
| --- | --- |
| 圆石墙体 | `#4A4A4A` `#7E7E7E` `#A3A3A3` |
| 木板梁柱 | `#8A6741` `#9C7549` `#B98D57` |
| 砖红屋顶 | `#6B3226` `#8B4433` `#A05242` |
| 炉火橙 | `#D64B0A` `#F79B22` |
| 草地绿 | `#5D9C3C` |
| 描边/阴影 | `#241A11` |

## 视觉描述

一座小型铁匠工坊，坐落在草地上：

- 圆石砌基座与烟囱，木板墙夹深色橡木梁，砖红人字屋顶
- 烟囱冒方块状白灰烟；工作面开敞，能看到橘色炉火与铁砧
- 门口一侧放工具架与矿石箱，炉火暖光洒在门前草地上
- 一条土路从门口延伸出画面
- 等距视角、块面平涂，**不出现人形**

## AI 提示词

```
Voxel pixel art concept art of a village blacksmith house for a sandbox
block game, 1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, every surface built from visible cubic
blocks, flat color faces with hard pixel edges, gentle isometric view, no
realistic perspective, no vanishing point, no painterly shading.

Content: a small blacksmith workshop on a grass clearing. A round
cobblestone base, timber-framed plank walls with dark oak beams, a brick
red gable roof, a stone chimney releasing blocky pixel smoke, an open
workshop front showing a glowing orange furnace, an anvil on a stump, tool
racks and ore crates beside the door. Warm furnace light spills onto the
grass. A dirt path leads away from the door.

Palette: cobble grays #4A4A4A #7E7E7E #A3A3A3, plank browns #8A6741
#9C7549 #B98D57, brick reds #6B3226 #8B4433 #A05242, fire orange #D64B0A
#F79B22, grass green #5D9C3C, dark outline #241A11.

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

1. 不降采样（目标即 1024 × 1024，最近邻重采样为恒等操作）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检（非平铺资源）

## 验收标准

- [ ] 尺寸恰为 1024 × 1024，无 Alpha 通道需求（整幅满铺）
- [ ] 画面中不存在任何文字/字母/数字（招牌必须是纯图形）
- [ ] 材质一眼认得出是圆石 / 木板 / 砖：与游戏内方块贴图同色系
- [ ] 块面平涂、硬边像素，无写实透视与柔和渐变
- [ ] 炉火为像素橙色块，不是写实火焰
