# 藏宝图

## 用途

热键栏/背包中的藏宝图图标（宝物，寻宝任务道具）。

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
| 纸主 | `#F2EAD2` |
| 纸侧 | `#D9C89A` |
| 旧痕 | `#A8842E` |
| 标记红 | `#D63838` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 视角微微斜放的一张羊皮旧地图：纸面奶白、边缘一圈焦茶旧化晕
- 纸面一条暗金虚线路径蜿蜒，右上角一小枚红色叉形藏宝标记，两道淡折痕
- 图上**绝不画字**、不画可辨认地形（纯抽象小色块示意即可省略）

## AI 提示词

```
A single treasure map icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. An old parchment map seen from a slight
three-quarter angle, lying slightly diagonal: a pale cream paper sheet with
a warm tan aged edge tint, one faint darker dotted path line curving across
the sheet, one small red cross-shaped mark near the top right corner, and
two soft folded crease lines across the paper. The map fills the frame.
Black 1-pixel outline. The empty corners are one flat solid dark backdrop
color #2A2620.

Color palette strictly: #F2EAD2, #D9C89A, #A8842E, #D63838, #2A2620,
#1A1A1A only.

No text, no letters, no writing, no compass, no islands, no drawings of
mountains or seas, no scroll rolls, no gradient, no cast shadow, no glow,
no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, letters, writing, compass, islands, coastline, mountains, sea
drawings, scroll, spyglass, gradient, drop shadow, glow, blur, 3D render,
anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 红叉标记与虚线路径可辨，纸面无文字无地形
