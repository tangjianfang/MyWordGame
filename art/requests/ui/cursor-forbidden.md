# M11-U23 禁止光标（cursor-forbidden）

## 用途

milestone-11 交互光标系列：对准**不可交互目标**（等级不够的矿、不可对话对象）
时的替换光标。红圈加斜杠的通用「禁止」符号。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（符号外全部键控透明） |
| 指针热点 | 图标中心（约第 16、16 像素） |
| 输出数量 | 1 张：`cursor-forbidden.png` |

## Alpha 的产出方式

符号之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边黑 | `#1A1A1A` |
| 禁止红主色 | `#D42B2B` |
| 禁止红暗部 | `#8C1B1B` |
| 背景键控色 | `#FF00FF` |

红色与 `heart` 同源——全游戏唯一的「警告红」。

## 视觉描述

一个禁止符号，居中：

- 圆环：外径约 24 像素，环体粗 3 像素，红色 `#D42B2B`，下缘 1 像素 `#8C1B1B` 压暗
- 斜杠：从左上 45° 贯穿圆心到右下，与环同粗 3 像素、同红色
- 圆环与斜杠整体包 1 像素黑描边
- 圆环内部（斜杠之外）为透明（洋红键控）

## AI 提示词

```
A pixel art forbidden sign mouse cursor for a game, 1024x1024, designed to be
downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, perfectly symmetrical, no
anti-aliasing.

Content: one round prohibition symbol centered in the canvas: a thick red ring
about 24 pixels across when downscaled, with a red diagonal bar of the same
thickness crossing from the top left to the bottom right through the center.
The ring and the bar share the same red, with a thin darker red line along their
lower edges. The whole symbol is wrapped in a one-pixel black outline.

Color palette strictly limited to: #1A1A1A, #D42B2B, #8C1B1B.

The background and the hollow areas inside the ring are flat pure magenta
#FF00FF, fully saturated, hard edges, no anti-aliasing.

No hand, no arrow pointer, no cursor arrow, no text, no icons inside the ring,
no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
hand, arrow pointer, cursor arrow, text, icons inside the ring, glow, shadow,
3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0（含环心挖空区），其余 Alpha = 255；去洋红边
3. 量化到 3 个色值
4. 核对圆环左右对称、斜杠过圆心
5. 存为 PNG-32，热点登记为中心（16, 16）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 圆环与斜杠同粗（3 像素）、同色
- [ ] 圆环内部除斜杠外透明（Alpha 0）
- [ ] 叠在纯白、纯黑、天蓝背景上都清晰可辨
- [ ] 颜色数 ≤ 3（不含透明）
