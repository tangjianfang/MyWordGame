# 面包

## 用途

热键栏/背包中的面包物品图标（合成获得的食物）。

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
| 皮暗 | `#8A5A2E` |
| 皮主 | `#B07C42` |
| 皮亮 | `#D8A86A` |
| 面包芯 | `#F2EAD2` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 侧视整条面包，水平放置、两端略翘：上缘金棕脆皮，正面两道斜切浅色划痕
- 底缘一条近白的面包芯色，脆皮三阶由上到下渐深
- 与既有 `beet.md` 等食物图标同等体量（主体占画面约 70%）
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single bread loaf icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A whole bread loaf seen from the side, lying
horizontally with slightly raised rounded ends: a golden-brown crust top
with two short diagonal pale slash marks, a lighter tan side wall, and a
thin pale cream bottom edge. The loaf fills the frame, wider than tall.
Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #8A5A2E, #B07C42, #D8A86A, #F2EAD2, #1A1A1A outline
only.

No text, no watermark, no slices, no plate, no butter, no seeds on crust, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, sliced bread, plate, butter, jam, sesame, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing, extra objects, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 两道划痕与底缘面包芯色可见，水平放置
