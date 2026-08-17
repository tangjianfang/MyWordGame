# 音乐盒

## 用途

热键栏/背包中的音乐盒物品图标（乐器，上弦播放旋律）。

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
| 盒暗 | `#6E5232` |
| 盒主 | `#8A6741` |
| 盒亮 | `#B98D57` |
| 曲柄金 | `#E8C04A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角：暖木小盒，盒盖微开一条缝翘起，盖缘一圈金色细镶嵌线
- 右侧伸出一枚金色 L 形上弦曲柄
- 盒体用家具统一暖木色系；不画音符符号
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single music box icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A small warm brown wooden box seen from a slight
three-quarter front angle: the lid slightly open with a thin raised gap, a
thin gold inlay line running around the lid edge, and one small golden
crank handle sticking out from the right side of the box in an L shape.
The box fills the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #6E5232, #8A6741, #B98D57, #E8C04A,
#1A1A1A outline only.

No text, no watermark, no ballerina, no notes, no music symbols, no
keyhole, no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel
edges only.
```

## 负面提示词

```
text, ballerina, dancer figurine, notes, music symbols, keyhole, key,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 微开盒盖与右侧金色曲柄可见，无人偶无音符
