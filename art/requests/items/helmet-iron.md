# 铁头盔

## 用途

热键栏/背包/盔甲栏中的铁头盔图标。铁系盔甲第一件（护头）。

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
| 铁暗 | `#5F5F5F` |
| 铁主 | `#8A8A8A` |
| 铁亮 | `#C8C8C8` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视圆顶盔：银灰金属穹顶 + 正中窄条护鼻 + 底缘一圈略深的帽檐带
- 左上一道亮色高光，铁色与铁剑/铁镐同 `#8A8A8A` 系
- 四材料头盔共用同一剪影，只换材质三阶色（见 gold/summer-alloy/machine-essence 同名件）

## AI 提示词

```
A single iron helmet icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A rounded knight helmet seen straight from the
front: a cool silver-gray metal dome cap with a narrow vertical nose guard
strip down the center of the face opening, a slightly darker metal rim band
along the bottom edge, and one light highlight streak on the upper left of
the dome. The helmet fills the frame. Black 1-pixel outline. The empty
corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #5F5F5F, #8A8A8A, #C8C8C8, #2A2620, #1A1A1A only.

No text, no watermark, no face, no eyes, no crest, no plume, no gradient, no
cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, face, eyes, mouth, crest, plume, feathers, horns, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 护鼻条与底缘帽檐带可见，无脸/无眼
