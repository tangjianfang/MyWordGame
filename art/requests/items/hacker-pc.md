# 极客主机

## 用途

热键栏/背包中的极客台式主机图标（黑客玩法核心家具，屏显绿色代码）。

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
| 壳暗 | `#443D34` |
| 壳主 | `#6B6155` |
| 壳亮 | `#8B7F6F` |
| 屏底黑 | `#1A1A1A` |
| 代码绿 | `#5D9C3C` |
| 代码亮绿 | `#74B84E` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角：右侧竖立暖灰主机塔（面板两粒小绿灯点），左侧一块深色竖屏横着几条绿色代码亮条
- 壳系与电脑三件套同复古暖灰；绿代码用全局草绿两阶（`#5D9C3C`/`#74B84E`）
- 屏上代码是抽象色条，绝不画可辨认字符

## AI 提示词

```
A single hacker desktop computer icon for an inventory slot, 1024x1024
pixel art designed to be downscaled to 32x32. Seen from a slight
three-quarter front angle: a tall warm dark-gray computer tower on the
right with two tiny green power light dots on its front panel, and a slim
dark monitor standing on the left showing only a few short horizontal green
code bars of different lengths on a near-black screen. Black 1-pixel
outline around both parts. The empty corners are one flat solid dark
backdrop color #2A2620.

Color palette strictly: #443D34, #6B6155, #8B7F6F, #1A1A1A, #5D9C3C,
#74B84E, #2A2620 only.

No text, no watermark, no letters, no readable characters, no numbers, no
keyboard, no mouse, no person, no gradient, no cast shadow, no large glow
halo, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, letters, readable code, numbers, binary, keyboard, mouse, hands,
hooded figure, matrix rain, gradient, drop shadow, glow halo, blur, 3D
render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 7
- 主机塔 + 屏两件分明，屏上绿色为抽象色条非字符
