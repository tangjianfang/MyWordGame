# ARCH-07 沙漠神庙蓝图

## 用途

第 3 波「结构生成」的沙漠神庙**结构蓝图**（Desert 群系地表结构：阶梯金字塔 +
地下密室宝箱）。风格与 `blueprint-mine` / `blueprint-shipwreck` 同系列；本图不直接进引擎。

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
| 砂岩亮/主/暗 | `#EADDB4` `#D9C89A` `#9C8858` |
| 宝藏金 | `#DCAE3A` `#F7DA7A` |
| 钻石青 | `#4CC6C4` |
| 幽灵线框 | `#4E88CE` |

## 视觉描述

深蓝方格图纸上的沙漠神庙剖面：

- 阶梯状砂岩金字塔，正面宽台阶直上门廊，四角各一根立柱
- 剖开地下：门廊下方藏一间密室，中央金色祭坛块 + 两块青色钻石块，
  两侧各一口宝箱，散落断柱碎石
- 周围沙丘只用浅蓝幽灵线框勾出
- 无文字、无箭头

## AI 提示词

```
Voxel pixel art structure blueprint of a desert temple for a sandbox block
game, 1024x1024, used directly as final art with no downscaling.

Style: technical blueprint look drawn in chunky voxel pixel art.
Background is flat deep slate blue graph paper with thin lighter blue
grid lines. The structure is drawn as solid colored voxel blocks in
gentle isometric view; the surrounding desert dunes are only faint
outlined ghost cubes.

Content: a stepped desert sandstone pyramid temple with a wide front
staircase, four corner pillars, an entrance doorway, and a cutaway hidden
chamber below ground level holding a gold altar block flanked by two cyan
diamond blocks, two treasure chests on either side, and some broken
pillar debris.

Palette: blueprint blue #1E2A44 background, grid line #31446B, sandstone
golds #EADDB4 #D9C89A #9C8858, treasure gold #DCAE3A #F7DA7A, diamond
cyan #4CC6C4, ghost outline #4E88CE.

No text, no letters, no numbers, no arrows, no labels, no watermark, no
logo, no UI, no humans, no mummies, no photorealism, no 3D render, no
blur, no smooth gradients, no anti-aliasing.
```

## 负面提示词

```
text, letters, numbers, arrows, labels, annotations, watermark, logo, UI,
humans, mummies, monsters, photorealism, 3D render, blur, smooth
gradients, anti-aliasing
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 画面中不存在任何文字/字母/数字/箭头
- [ ] 与另两张蓝图并排看是同一套图纸风格
- [ ] 砂岩色与游戏 `sand`（`#D9C89A`）同系，只是更厚重
- [ ] 地下密室剖面清楚可见（结构信息不能被沙丘挡住）
