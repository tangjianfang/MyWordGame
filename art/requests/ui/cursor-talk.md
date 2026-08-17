# M11-U22 对话光标（cursor-talk）

## 用途

milestone-11 交互光标系列：准星对准**可对话 NPC（村民）**时的替换光标。
对话气泡是「能说话」的通用符号。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（气泡外全部键控透明） |
| 指针热点 | 气泡左上角（约第 3、3 像素） |
| 输出数量 | 1 张：`cursor-talk.png` |

## Alpha 的产出方式

气泡之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边黑 | `#1A1A1A` |
| 气泡白 | `#F2F2F2` |
| 气泡阴影 | `#C8C8C8` |
| 三点黑 | `#1A1A1A` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

一个圆角方形对话气泡，尾巴朝左下：

- 气泡体占画布上部约 70%，白色、下缘 1 像素灰阴影，整体包 1 像素黑描边
- 气泡内水平排三个 2 × 2 黑点（正在说话的省略号）
- 气泡左下角伸出 3×3 的小三角尾巴
- 无任何文字

## AI 提示词

```
A pixel art speech bubble mouse cursor for a game dialog, 1024x1024, designed to
be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Content: one rounded-corner rectangular speech bubble in white, taking the upper
70 percent of the canvas, with a one-pixel black outline around it and a thin
gray shading line along its bottom inside edge. Inside the bubble sit three
small black square dots in a horizontal row, evenly spaced. A small triangular
tail points down and to the left from the bottom-left corner of the bubble.

Color palette strictly limited to: #1A1A1A, #F2F2F2, #C8C8C8.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No letters, no words, no question mark, no exclamation mark, no face, no person,
no text, no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
letters, words, question mark, exclamation mark, face, person, text, glow,
shadow, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 3 个色值
4. 校正三个黑点大小一致（各 2 × 2）、等距水平排列
5. 存为 PNG-32，热点登记为气泡左上角（3, 3）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 三点省略号等距、大小一致，无文字符号
- [ ] 尾巴朝左下且不与气泡体断开
- [ ] 叠在纯白、纯黑、天蓝背景上都清晰可辨
- [ ] 颜色数 ≤ 3（不含透明）
