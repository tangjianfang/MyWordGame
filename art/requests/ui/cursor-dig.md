# M11-U20 挖掘光标（cursor-dig）

## 用途

milestone-11 交互光标系列：准星对准**可挖掘方块**时的替换光标。
镐是「挖」的最强符号。指针热点在镐尖（左上）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（镐外全部键控透明） |
| 指针热点 | 左上镐尖（约第 4、4 像素） |
| 输出数量 | 1 张：`cursor-dig.png` |

## Alpha 的产出方式

镐之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边黑 | `#1A1A1A` |
| 镐头钢灰主色 | `#8A8A8A` |
| 镐头高光 | `#A3A3A3` |
| 镐头暗部 | `#6E6E6E` |
| 木柄亮 | `#B98D57` |
| 木柄暗 | `#8A6741` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

一把斜 45° 的像素镐，整体从左上压向右下：

- 镐头是一段弧形钢灰横梁，位于左上，两端微微下弯，中间最高点接木柄
- 木柄从镐头中心斜向右下延伸到画面右下角附近
- 全部轮廓包 1 像素黑描边（光标叠在任意场景上必须清晰）
- 镐占画布约 70%，镐尖指向左上角热点

## AI 提示词

```
A pixel art pickaxe mouse cursor for a mining game, 1024x1024, designed to be
downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, no perspective, no anti-aliasing.

Content: one single pickaxe drawn diagonally, its head at the top left and its
wooden handle running down to the bottom right. The head is a curved steel-gray
bar with a lighter top edge and a darker underside; the handle is plain wood,
lighter on one side. The whole tool is wrapped in a one-pixel black outline. The
pickaxe fills about 70 percent of the canvas, and the very tip of the pick head
points toward the top-left corner.

Color palette strictly limited to: #1A1A1A, #8A8A8A, #A3A3A3, #6E6E6E, #B98D57,
#8A6741.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No arrow pointer, no hand, no gloves, no blocks, no cubes, no text, no glow, no
shadow, no 3D render, no watermark.
```

## 负面提示词

```
arrow pointer, hand, gloves, blocks, cubes, text, glow, shadow, 3D,
anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 6 个色值
4. 核对描边完整闭合
5. 存为 PNG-32，热点登记为左上镐尖（4, 4）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 镐尖落在左上热点附近（≤ 4, 4）
- [ ] 轮廓描边完整，无一处露底色
- [ ] 叠在纯白、纯黑、天蓝背景上都清晰可辨
- [ ] 缩到实际指针大小后镐头/木柄仍可分辨
