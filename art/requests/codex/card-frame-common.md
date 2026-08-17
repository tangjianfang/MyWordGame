# M11-C01 图鉴卡框 · 普通（card-frame-common）

## 用途

milestone-11 图鉴收集卡界面：**普通稀有度**卡片的边框，九宫格拉伸。
卡心完全挖空（键控为透明），收集卡插画（`codex/card-*`）从框底下透出。
三档稀有度共用同一几何结构，仅边框色带不同——普通 = 暖灰（UI 基本色）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素（方形卡牌，与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（走九宫格拉伸） |
| Alpha | **有**，仅 0 或 255（框外与框内全部键控透明） |
| 输出数量 | 1 张：`card-frame-common.png` |

### 九宫格切分

四边边界均为 **8 像素**。四角 8 × 8 不拉伸；中间挖空区纯洋红。

## Alpha 的产出方式

边框内部挖空区与画布外全部画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 / 内描边 | `#17140F` |
| 边框主色（暖灰） | `#6B6155` |
| 边框亮线（上/左 1px） | `#8B7F6F` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

由外向内：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 亮线 | 1 像素 | 上/左 `#8B7F6F` |
| 3. 边框主带 | 3 像素 | `#6B6155` |
| 4. 内描边 | 1 像素 | `#17140F` |
| 5. 挖空区 | 中间 | 洋红（键控为透明） |

边框总厚 6 像素，四边均匀、无任何角饰（拉伸区不留装饰）。

## AI 提示词

```
A pixel art trading card frame for a game codex collection, 1024x1024, designed
to be downscaled to 64x64 pixel art. Square, front view, flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient, no texture.

Content: a plain rectangular border frame around a completely hollow center. From
outside in: a one-pixel near-black outline #17140F, a one-pixel lighter warm gray
line #8B7F6F along the top and left edges, a three-pixel warm gray-brown border
band #6B6155, and a one-pixel near-black inner rim #17140F. The border thickness
is perfectly uniform on all four sides with clean right-angle corners.

Both the hollow center of the card and everything outside the card are flat pure
magenta #FF00FF, fully saturated, hard edges, no anti-aliasing.

Color palette strictly limited to: #17140F, #8B7F6F, #6B6155.

No corner ornaments, no symbols, no gems, no text, no numbers, no icons, no glow,
no bevel, no 3D render, no watermark.
```

## 负面提示词

```
corner ornaments, symbols, gems, text, numbers, icons, glow, bevel, 3D, gradient,
texture, watermark
```

## 后处理

1. 最近邻降采样到 64 × 64
2. 键控：全部洋红（含中心挖空区）Alpha = 0，其余 255；去洋红边
3. 量化到 3 个色值
4. 纯色化四条边中段，逐边校正总厚 6 像素
5. 九宫格拉伸测试（拉到 128 × 128 边框不变形）

## 验收标准

- [ ] 尺寸恰为 64 × 64
- [ ] 中间挖空区 Alpha 全为 0
- [ ] 边框四边总厚一致（6 像素）
- [ ] 与 `card-frame-rare/epic` 叠放对比：仅色带颜色不同，几何逐像素一致
- [ ] 透出 `card-ore-iron` 插画时卡角无遮挡
