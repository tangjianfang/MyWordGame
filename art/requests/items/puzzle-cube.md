# 魔方

## 用途

热键栏/背包中的魔方物品图标（玩具，益智拼转）。

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
| 顶面白 | `#F2F2F2` |
| 左面蓝 | `#4E88CE` |
| 右面红 | `#D63838` |
| 立方体暗 | `#5F5F5F` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 视角立方体露出三面：顶白、左蓝、右红，每面 3×3 贴纸小方块
- 贴纸间 1 像素深色缝隙，立方体右下角一圈暗阶压体积
- 只用三面色（32×32 下六面色分不清），扁平贴纸无反光

## AI 提示词

```
A single puzzle cube icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A cube puzzle seen from a slight
three-quarter angle showing three faces: the top face white, the left face
blue, the right face red; each face is divided into a neat 3x3 grid of
small rounded sticker squares with thin dark gaps between the stickers, and
one darker shaded corner along the bottom right of the cube. The cube fills
the frame. Black 1-pixel outline. The empty corners are one flat solid
dark backdrop color #2A2620.

Color palette strictly: #F2F2F2, #4E88CE, #D63838, #5F5F5F, #2A2620,
#1A1A1A only.

No text, no watermark, no hands, no fingers, no more than three visible
faces, no glossy reflections, no gradient, no cast shadow, no glow, no
anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, hands, fingers, spinning, motion blur, glossy reflections, six
visible faces, gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 三面各 3×3 贴纸可辨，缝隙整齐
