# 弓

## 用途

热键栏/背包中的弓物品图标。竖立木弓 + 朋紧弓弦。

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
| 弓木暗 | `#634C33` |
| 弓木主 | `#7A6042` |
| 弓木亮 | `#8E7350` |
| 弓弦 | `#E8E8E8` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视竖立长弓：弓木弧向左弯，弓弦在右侧连两弓梢，弧顶到底撑满画面
- 弓木三阶木色与既有剑/镐握柄同系（`#634C33` 起手），中部一小段深色缠皮
- 弓弦 1–2 像素宽、纯浅灰，绷直不画松垂
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single bow icon for an inventory slot, 1024x1024 pixel art designed to be
downscaled to 32x32. A wooden archery bow seen from the front, standing
vertically: a thick curved wooden arc bending to the left, a straight taut
pale bowstring on the right side connecting the two arc tips, and a short
dark brown leather grip wrap at the middle of the arc. The bow fills the
frame from top to bottom. Black 1-pixel outline around the bow and the
string. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #634C33, #7A6042, #8E7350, #E8E8E8, #1A1A1A outline
only.

No text, no watermark, no arrow, no quiver, no hands, no gradient, no cast
shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, arrow, quiver, hands, archer, gradient, drop shadow, glow,
blur, 3D render, anti-aliasing, extra objects, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 弓体竖向撑满画面，弓弦可见且绷直
