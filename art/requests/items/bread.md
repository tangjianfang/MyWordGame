# 面包

## 用途

热键栏/背包中的面包物品图标（合成获得的食物）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 皮暗 | `#8A5A2E` |
| 皮主 | `#B07C42` |
| 皮亮 | `#D8A86A` |
| 面包芯 | `#F2EAD2` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 侧视整条面包，水平放置、两端略翘：上缘金棕脆皮，正面两道斜切浅色划痕
- 底缘一条近白的面包芯色，脆皮三阶由上到下渐深
- 与既有 `beet.md` 等食物图标同等体量（主体占画面约 70%）

## AI 提示词

```
A single bread loaf icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A whole bread loaf seen from the side, lying
horizontally with slightly raised rounded ends: a golden-brown crust top
with two short diagonal pale slash marks, a lighter tan side wall, and a
thin pale cream bottom edge. The loaf fills the frame, wider than tall.
Black 1-pixel outline. The empty corners are one flat solid dark backdrop
color #2A2620.

Color palette strictly: #8A5A2E, #B07C42, #D8A86A, #F2EAD2, #2A2620, #1A1A1A
only.

No text, no watermark, no slices, no plate, no butter, no seeds on crust, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, sliced bread, plate, butter, jam, sesame, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing, extra objects
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 两道划痕与底缘面包芯色可见，水平放置
