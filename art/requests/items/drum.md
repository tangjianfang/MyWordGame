# 鼓

## 用途

热键栏/背包中的鼓物品图标（乐器，可敲击发声）。

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
| 鼓面 | `#D9C89A` |
| 鼓面亮 | `#F2EAD2` |
| 鼓壳木 | `#8A6741` |
| 鼓箍暗 | `#6E5232` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角立式圆筒鼓：顶面皮革鼓面（一道受光亮环）+ 侧面木壳桶身
- 上下两圈深色木箍收紧鼓皮，正面两道交叉绳结示意绷紧
- 鼓壳用家具统一暖木色系，鼓面用草黄/纸页色系
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single drum icon for an inventory slot, 1024x1024 pixel art designed to
be downscaled to 32x32. A cylindrical side drum seen from a slight
three-quarter front angle: a warm tan leather drumhead on top with one
lighter stretched highlight ring, a warm brown wooden shell body, and two
darker wooden tension rim hoops, one above and one below the shell; two
small crisscross rope laces show on the front of the shell. Black 1-pixel
outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #D9C89A, #F2EAD2, #8A6741, #6E5232,
#1A1A1A outline only.

No text, no watermark, no drumsticks, no notes, no music symbols, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, drumsticks, hands, notes, music symbols, gradient, drop shadow, glow,
blur, 3D render, anti-aliasing, extra objects, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 鼓面/鼓壳/双箍三段分明，绳结可见
