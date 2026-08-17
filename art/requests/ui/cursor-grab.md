# M11-U24 抓取光标（cursor-grab）

## 用途

milestone-11 交互光标系列：对准**可拾取/可拖拽对象**（掉落物、可搬运家具）
时的替换光标。张开的手掌 =「能拿」。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（手掌外全部键控透明） |
| 指针热点 | 食指尖（约第 6、4 像素） |
| 输出数量 | 1 张：`cursor-grab.png` |

## Alpha 的产出方式

手掌之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边黑 | `#1A1A1A` |
| 手掌肤色 | `#D2A48A` |
| 手掌阴影 | `#B98A6C` |
| 背景键控色 | `#FF00FF` |

肤色与生物图标范式（`sheep.md` 的脸皮色）一致。

## 视觉描述

一只正面张开的像素手掌，指尖朝上：

- 五指展开：食指最高、位于左上热点附近，拇指在右下侧与其余四指分开
- 手掌体肤色 `#D2A48A`，指根与掌心一侧压 `#B98A6C` 阴影
- 整体包 1 像素黑描边，占画布约 65%

## AI 提示词

```
A pixel art open hand mouse cursor for a game, 1024x1024, designed to be
downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Content: one open human hand seen from the back, fingers pointing up and spread
apart, thumb sticking out to the lower right. The hand is skin-toned with a
slightly darker shading on the finger roots and one side of the palm. The whole
hand is wrapped in a one-pixel black outline and fills about 65 percent of the
canvas. The top of the index finger is the highest point, near the top left.

Color palette strictly limited to: #1A1A1A, #D2A48A, #B98A6C.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No arrow pointer, no glove, no sleeve, no cuff, no grabbing motion lines, no
item in the hand, no text, no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
arrow pointer, glove, sleeve, cuff, motion lines, item in hand, text, glow,
shadow, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 3 个色值
4. 核对描边完整闭合、五指互不粘连
5. 存为 PNG-32，热点登记为食指尖（6, 4）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 五指可数、互不粘连，指尖朝上
- [ ] 食指尖落在左上热点附近
- [ ] 叠在纯白、纯黑、天蓝背景上都清晰可辨
- [ ] 颜色数 ≤ 3（不含透明）
