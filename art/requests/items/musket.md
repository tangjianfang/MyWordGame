# 火枪

## 用途

热键栏/背包/工作台合成产物图标的火枪物品贴图。横置老式燧发枪：木枪托 + 铁枪管 + 黄铜击锤。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 背景 | 整片纯洋红 `#FF00FF`，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 枪身木暗 | `#3A2418` |
| 枪身木主 | `#6B4423` |
| 枪身木亮 | `#8B5A2B` |
| 铁枪管暗 | `#2A2A2A` |
| 铁枪管主 | `#4A4A4A` |
| 铁枪管亮 | `#6E6E6E` |
| 黄铜击锤 | `#C49B3A` |
| 黄铜高光 | `#E8C46A` |
| 描边 | `#1A1A1A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

- 横置火枪：左木枪托（占 3–15 列），中段黄铜击锤（10–13 行的小方块）+ 铁扳机护圈（15–21 行弯钩），右铁枪管（20–30 列 4 px 高长条，管口略出右边）
- 枪托三阶木色（暗 / 主 / 亮），与既有木剑 / 木镐握柄同系（`#6B4423` 起手）
- 击锤一档黄铜高光（最上一行），主色黄铜压暗——金属反光
- 主体水平占 80%，垂直方向枪管 + 枪托挤在中线（紧凑布局，避免留太多透明）
- 背景整片纯洋红 `#FF00FF` 键控为透明

## AI 提示词

```
A single antique musket icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A horizontal flintlock musket: a wooden stock on the
left (warm brown, dark to light three-tone), a long dark iron barrel on the
right (dark gray to mid gray, with a slight muzzle highlight at the very tip),
a small brass hammer mechanism in the middle (warm yellow-gold), and an iron
trigger guard curving below. The barrel takes about 35% of the width, the stock
about 40%, and the mechanism takes the middle 15%. The whole gun is centered
horizontally and vertically, taking about 80 percent of the frame width and 50
percent of the height. Black 1-pixel outline around all parts.

Color palette strictly: #3A2418, #6B4423, #8B5A2B (wood); #2A2A2A, #4A4A4A,
#6E6E6E (iron); #C49B3A, #E8C46A (brass); #1A1A1A outline only.

No text, no watermark, no bullet, no hands, no gunpowder smoke, no gradient, no
cast shadow, no glow, no anti-aliasing. Hard pixel edges only. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing, with clear magenta margins on all sides.
```

## 负面提示词

```
text, watermark, bullet, modern gun, rifle with scope, hands, shooter, smoke,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing, extra objects,
dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 9（暗木 / 主木 / 亮木 + 暗铁 / 主铁 / 亮铁 + 黄铜 / 黄铜高光 + 描边）
- 木枪托、铁枪管、黄铜击锤三段清晰可辨
- 火枪整体居中，不贴边不出框

## 状态

程序占位（art/scripts/gen_m13_w3_musket_placeholders.py），待正式美术替换