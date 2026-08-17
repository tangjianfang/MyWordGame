# M11-U19 成就徽章弹窗底板（fx-badge-popup）

## 用途

milestone-11 图鉴/成就系统：解锁成就时屏幕上方弹出的「徽章 + 名称」
浮窗底板。徽章图标（`codex/badge-*`）与成就名（像素字体）由运行时叠放，
本图只做底板。金边色与 16 枚徽章的底盘金边一致（`#8B7355`）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素（与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（弹窗单体图，居中显示） |
| Alpha | **有**，仅 0 或 255（底板外全部键控透明） |
| 输出数量 | 1 张：`fx-badge-popup.png` |

## Alpha 的产出方式

底板之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 | `#17140F` |
| 立体边亮 | `#8B7F6F` |
| 边框主体 | `#6B6155` |
| 立体边暗 | `#443D34` |
| 金饰边 | `#8B7355` |
| 内面（放徽章） | `#57503F` |
| 背景键控色 | `#FF00FF` |

与 `panel` 同色系 + 一圈金饰边，「值得庆祝」但不出戏。

## 视觉描述

居中一块 26 × 26 的方形底板（四角切 2 像素圆角）：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 立体边 | 1 像素 | 上/左 `#8B7F6F`，下/右 `#443D34` |
| 3. 边框主体 | 3 像素 | `#6B6155` |
| 4. 金饰边 | 1 像素 | `#8B7355` |
| 5. 内面 | 其余 | `#57503F` 纯色 |

内面完全纯色（徽章与文字运行时叠加），无任何纹理装饰。

## AI 提示词

```
A pixel art achievement popup plaque background for a retro game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Shape: one square plaque centered on the canvas taking about 80 percent of the
image, with its four corners clipped by two pixels. Front view, flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient, no texture.

Content: from outside in: a one-pixel near-black outline #17140F, a one-pixel
bevel light on top and left #8B7F6F and dark on bottom and right #443D34, a
three-pixel flat warm gray-brown band #6B6155, a one-pixel antique gold trim
line #8B7355, and a completely flat darker inner field #57503F.

Color palette strictly limited to: #17140F, #8B7F6F, #6B6155, #443D34, #8B7355,
#57503F.

The inner field must be one single flat color with no texture, no noise and no
pattern.

Everything outside the plaque is flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing.

No badges, no medals, no ribbons, no stars, no text, no numbers, no icons, no
glow, no drop shadow, no 3D render, no watermark.
```

## 负面提示词

```
badges, medals, ribbons, stars, text, numbers, icons, glow, drop shadow, 3D,
gradient, texture, watermark
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 6 个色值
4. 纯色化内面，逐层校正厚度表
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 底板居中、约 26 × 26，四角切 2 像素
- [ ] 内面单一颜色，方差为 0
- [ ] 金饰边 `#8B7355` 连续闭合、恰 1 像素
- [ ] 缩放叠放一枚徽章后，徽章金边与底板金饰边观感一致
