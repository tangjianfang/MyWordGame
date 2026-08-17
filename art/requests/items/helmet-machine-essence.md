# 机元头盔

## 用途

热键栏/背包/盔甲栏中的机元头盔图标。机元系盔甲第一件（护头），科技感最强。

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
| 机元暗 | `#5E4478` |
| 机元主 | `#7A5A9A` |
| 机元亮 | `#9C82BC` |
| 眼缝辉光 | `#B9A2D4` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `helmet-iron` 同一正视圆顶盔剪影，材质换 m10 机元紫灰系
- 紫灰与机元剑/镐/核心同源（`#5E4478`/`#7A5A9A`/`#9C82BC`），「见色知血」（maxHealth 系）
- 差异点：护鼻条换成一条横贯眼位的发光缝（`#B9A2D4` 亮紫），两侧各一粒小光点，不画外发光晕
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single machine essence helmet icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A rounded tech helmet seen straight
from the front: a purple-gray metal dome cap with one horizontal glowing eye
slit of bright pale violet across the face area, a slightly darker rim band
along the bottom edge, and two tiny pale violet light dots on the sides of
the dome. The helmet fills the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #5E4478, #7A5A9A, #9C82BC, #B9A2D4, #1A1A1A outline
only.

No text, no watermark, no face, no mouth, no crest, no gradient, no cast
shadow, no large glow halo, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, face, eyes, mouth, crest, plume, robot face, gradient, drop
shadow, glow halo, lens flare, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 与 `helmet-iron` 并排剪影一致，眼缝亮紫可见且无大面积光晕
