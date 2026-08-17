# 弓

## 用途

热键栏/背包中的弓物品图标。竖立木弓 + 朋紧弓弦。

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
| 弓木暗 | `#634C33` |
| 弓木主 | `#7A6042` |
| 弓木亮 | `#8E7350` |
| 弓弦 | `#E8E8E8` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视竖立长弓：弓木弧向左弯，弓弦在右侧连两弓梢，弧顶到底撑满画面
- 弓木三阶木色与既有剑/镐握柄同系（`#634C33` 起手），中部一小段深色缠皮
- 弓弦 1–2 像素宽、纯浅灰，绷直不画松垂

## AI 提示词

```
A single bow icon for an inventory slot, 1024x1024 pixel art designed to be
downscaled to 32x32. A wooden archery bow seen from the front, standing
vertically: a thick curved wooden arc bending to the left, a straight taut
pale bowstring on the right side connecting the two arc tips, and a short
dark brown leather grip wrap at the middle of the arc. The bow fills the
frame from top to bottom. Black 1-pixel outline around the bow and the
string. The empty corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #634C33, #7A6042, #8E7350, #E8E8E8, #2A2620, #1A1A1A
only.

No text, no watermark, no arrow, no quiver, no hands, no gradient, no cast
shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, arrow, quiver, hands, archer, gradient, drop shadow, glow,
blur, 3D render, anti-aliasing, extra objects
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 弓体竖向撑满画面，弓弦可见且绷直
