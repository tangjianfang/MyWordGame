# M11-C03 图鉴卡框 · 史诗（card-frame-epic）

## 用途

milestone-11 图鉴收集卡界面：**史诗**卡片的边框。几何与
`card-frame-common` 完全一致，仅把边框色带换成**紫罗兰**（m10 机元矿同款紫，
全游戏「最高级/神秘」的颜色语言）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素（方形卡牌，与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（走九宫格拉伸） |
| Alpha | **有**，仅 0 或 255（框外与框内全部键控透明） |
| 输出数量 | 1 张：`card-frame-epic.png` |

### 九宫格切分

同 `card-frame-common`：四边边界均为 **8 像素**，中间挖空区纯洋红。

## Alpha 的产出方式

边框内部挖空区与画布外全部画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 / 内描边 | `#17140F` |
| 边框主色（紫罗兰） | `#7B4FA6` |
| 边框亮线（上/左 1px） | `#9B6FC9` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

与 `card-frame-common` 同构：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 亮线 | 1 像素 | 上/左 `#9B6FC9` |
| 3. 边框主带 | 3 像素 | `#7B4FA6` |
| 4. 内描边 | 1 像素 | `#17140F` |
| 5. 挖空区 | 中间 | 洋红（键控为透明） |

## AI 提示词

```
A pixel art trading card frame for a game codex collection, 1024x1024, designed
to be downscaled to 64x64 pixel art. Square, front view, flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient, no texture.

Content: a plain rectangular border frame around a completely hollow center. From
outside in: a one-pixel near-black outline #17140F, a one-pixel lighter violet
line #9B6FC9 along the top and left edges, a three-pixel violet purple border
band #7B4FA6, and a one-pixel near-black inner rim #17140F. The border thickness
is perfectly uniform on all four sides with clean right-angle corners.

Both the hollow center of the card and everything outside the card are flat pure
magenta #FF00FF, fully saturated, hard edges, no anti-aliasing.

Color palette strictly limited to: #17140F, #9B6FC9, #7B4FA6.

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
- [ ] 与 `card-frame-common/rare` 叠放对比：仅色带颜色不同，几何逐像素一致
- [ ] 三档卡框并排时稀有度层级一眼可读（灰 < 蓝 < 紫）
