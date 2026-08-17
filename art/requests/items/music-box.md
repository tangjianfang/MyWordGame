# 音乐盒

## 用途

热键栏/背包中的音乐盒物品图标（乐器，上弦播放旋律）。

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
| 盒暗 | `#6E5232` |
| 盒主 | `#8A6741` |
| 盒亮 | `#B98D57` |
| 曲柄金 | `#E8C04A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角：暖木小盒，盒盖微开一条缝翘起，盖缘一圈金色细镶嵌线
- 右侧伸出一枚金色 L 形上弦曲柄
- 盒体用家具统一暖木色系；不画音符符号

## AI 提示词

```
A single music box icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A small warm brown wooden box seen from a slight
three-quarter front angle: the lid slightly open with a thin raised gap, a
thin gold inlay line running around the lid edge, and one small golden
crank handle sticking out from the right side of the box in an L shape.
The box fills the frame. Black 1-pixel outline. The empty corners are one
flat solid dark backdrop color #2A2620.

Color palette strictly: #6E5232, #8A6741, #B98D57, #E8C04A, #2A2620,
#1A1A1A only.

No text, no watermark, no ballerina, no notes, no music symbols, no
keyhole, no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel
edges only.
```

## 负面提示词

```
text, ballerina, dancer figurine, notes, music symbols, keyhole, key,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 微开盒盖与右侧金色曲柄可见，无人偶无音符
