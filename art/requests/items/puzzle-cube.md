# 魔方

## 用途

热键栏/背包中的魔方物品图标（玩具，益智拼转）。

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
| 顶面白 | `#F2F2F2` |
| 左面蓝 | `#4E88CE` |
| 右面红 | `#D63838` |
| 立方体暗 | `#5F5F5F` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 视角立方体露出三面：顶白、左蓝、右红，每面 3×3 贴纸小方块
- 贴纸间 1 像素深色缝隙，立方体右下角一圈暗阶压体积
- 只用三面色（32×32 下六面色分不清），扁平贴纸无反光
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single puzzle cube icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A cube puzzle seen from a slight
three-quarter angle showing three faces: the top face white, the left face
blue, the right face red; each face is divided into a neat 3x3 grid of
small rounded sticker squares with thin dark gaps between the stickers, and
one darker shaded corner along the bottom right of the cube. The cube fills
the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #F2F2F2, #4E88CE, #D63838, #5F5F5F,
#1A1A1A outline only.

No text, no watermark, no hands, no fingers, no more than three visible
faces, no glossy reflections, no gradient, no cast shadow, no glow, no
anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, hands, fingers, spinning, motion blur, glossy reflections, six
visible faces, gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 三面各 3×3 贴纸可辨，缝隙整齐
