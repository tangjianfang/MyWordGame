# ARCH-05 矿井入口蓝图

## 用途

第 3 波「结构生成」的矿井入口**结构蓝图**。蓝图系列是结构模板设计者的参照图：
深蓝图纸底上画实体结构 + 幽灵线框地形，模板照它排「支架—竖梯—铁轨—火把」的方块序列。
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
| 木质支架 | `#8A6741` `#9C7549` |
| 石层 | `#7E7E7E` `#A3A3A3` |
| 火把橙 | `#F79B22` |
| 矿脉金/青 | `#DCAE3A` `#4CC6C4` |
| 幽灵线框 | `#4E88CE` |

## 视觉描述

深蓝方格图纸上的矿井入口剖面：

- 结构本体用实色体素块画：木支架门框、横梁、一层竖梯
- 铁轨从入口伸进矿道，轨上停一辆矿车
- 矿道壁上插两支火把（橙色火苗），石壁里嵌金色与青色矿脉
- 周围山体只用**浅蓝幽灵线框**勾出方块轮廓，不实心
- 蓝图风格但**无任何文字、箭头、标注**——信息全靠图形

## AI 提示词

```
Voxel pixel art structure blueprint of a mine entrance for a sandbox block
game, 1024x1024, used directly as final art with no downscaling.

Style: technical blueprint look drawn in chunky voxel pixel art.
Background is flat deep slate blue graph paper with thin lighter blue
grid lines. The structure itself is drawn as solid colored voxel blocks in
gentle isometric view; the surrounding terrain is only faint outlined
ghost cubes.

Content: a cutaway mine entrance: timber support frame and beams, a
ladder shaft going down one level, a rail track with one minecart, wall
torches with orange flame, gold and cyan ore veins embedded in the stone
walls, a braced side corridor. Ghost cube outlines suggest the hill
around the entrance.

Palette: blueprint blue #1E2A44 background, grid line #31446B, wood browns
#8A6741 #9C7549, stone grays #7E7E7E #A3A3A3, torch orange #F79B22, gold
#DCAE3A, cyan #4CC6C4, ghost outline #4E88CE.

No text, no letters, no numbers, no arrows, no labels, no watermark, no
logo, no UI, no humans, no photorealism, no 3D render, no blur, no smooth
gradients, no anti-aliasing.
```

## 负面提示词

```
text, letters, numbers, arrows, labels, annotations, watermark, logo, UI,
humans, photorealism, 3D render, blur, smooth gradients, anti-aliasing
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 画面中不存在任何文字/字母/数字/箭头/标注线
- [ ] 图纸底是满铺深蓝 + 均匀网格，网格不带透视
- [ ] 结构实体与幽灵线框视觉上明确分层（实心 vs 轮廓）
- [ ] 矿脉金/青与游戏内 gold-ore / diamond-ore 斑块色一致
