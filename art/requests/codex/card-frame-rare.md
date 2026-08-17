# M11-C02 图鉴卡框 · 稀有（card-frame-rare）

## 用途

milestone-11 图鉴收集卡界面：**稀有**卡片的边框。几何与
`card-frame-common` 完全一致，仅把边框色带换成**水蓝**（全局调色板水蓝系）——
「稀有 = 蓝」的通用直觉。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素（方形卡牌，与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（走九宫格拉伸） |
| Alpha | **有**，仅 0 或 255（框外与框内全部键控透明） |
| 输出数量 | 1 张：`card-frame-rare.png` |

### 九宫格切分

同 `card-frame-common`：四边边界均为 **8 像素**，中间挖空区纯洋红。

## Alpha 的产出方式

边框内部挖空区与画布外全部画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 / 内描边 | `#17140F` |
| 边框主色（水蓝） | `#3A6FB5` |
| 边框亮线（上/左 1px） | `#4E88CE` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

与 `card-frame-common` 同构：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 亮线 | 1 像素 | 上/左 `#4E88CE` |
| 3. 边框主带 | 3 像素 | `#3A6FB5` |
| 4. 内描边 | 1 像素 | `#17140F` |
| 5. 挖空区 | 中间 | 洋红（键控为透明） |

## AI 提示词

```
A pixel art trading card frame for a game codex collection, 1024x1024, designed
to be downscaled to 64x64 pixel art. Square, front view, flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient, no texture.

Content: a plain rectangular border frame around a completely hollow center. From
outside in: a one-pixel near-black outline #17140F, a one-pixel lighter blue line
#4E88CE along the top and left edges, a three-pixel water blue border band
#3A6FB5, and a one-pixel near-black inner rim #17140F. The border thickness is
perfectly uniform on all four sides with clean right-angle corners.

Both the hollow center of the card and everything outside the card are flat pure
magenta #FF00FF, fully saturated, hard edges, no anti-aliasing.

Color palette strictly limited to: #17140F, #4E88CE, #3A6FB5.

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
5. 九宫格拉伸测试

## 验收标准

- [ ] 尺寸恰为 64 × 64
- [ ] 中间挖空区 Alpha 全为 0
- [ ] 边框四边总厚一致（6 像素）
- [ ] 与 `card-frame-common` 叠放对比：仅色带颜色不同，几何逐像素一致
- [ ] 蓝色带与 `water` 方块同屏时不混淆（UI 框带黑描边）
