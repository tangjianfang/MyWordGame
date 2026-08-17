# 夏季合金头盔

## 用途

热键栏/背包/盔甲栏中的夏季合金头盔图标。合金系盔甲第一件（护头）。

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
| 合金暗 | `#2A5F75` |
| 合金主 | `#4A9AB8` |
| 合金亮 | `#78C2DC` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `helmet-iron` 同一正视圆顶盔剪影，只把三阶铁色换成 m10 合金青蓝系
- 青蓝与夏季合金剑/镐/锭同源（`#2A5F75`/`#4A9AB8`/`#78C2DC`），「见色知速」（moveSpeed 系）
- 护鼻条与帽檐带用暗阶青蓝，左上一道亮青高光

## AI 提示词

```
A single summer alloy helmet icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A rounded knight helmet seen straight
from the front: a cyan-blue alloy metal dome cap with a narrow vertical nose
guard strip down the center of the face opening, a slightly darker blue rim
band along the bottom edge, and one pale cyan highlight streak on the upper
left of the dome. The helmet fills the frame. Black 1-pixel outline. The
empty corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #2A5F75, #4A9AB8, #78C2DC, #2A2620, #1A1A1A only.

No text, no watermark, no face, no eyes, no crest, no plume, no gradient, no
cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, face, eyes, mouth, crest, plume, feathers, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 与 `helmet-iron` 并排剪影一致，颜色为青蓝系
