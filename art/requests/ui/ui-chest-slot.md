# M11-U12 箱子界面物品格（ui-chest-slot）

## 用途

milestone-11 第 2 波「箱子 UI」：箱子界面里的物品格边框。
结构与 `hotbar-slot`（U-02）同构，但用**木板色边框 + 四角铁包角**——
「铁包角木箱」的直觉，与灰色的背包格、木色的盔甲槽三分开来。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 48 × 48 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（槽位单体图） |
| Alpha | **有**：外部 0、内部半透明黑 110、边框 255 |
| 输出数量 | 1 张：`ui-chest-slot.png` |

### 九宫格边距（同 hotbar-slot 约定）

上 / 下 / 左 / 右各 6 像素。铁包角只画在四角 6 × 6 的不拉伸区内。

## Alpha 的产出方式

槽位外部的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明；
内部填充后处理精确置为 Alpha 110。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边（黑） | `#1A1A1A` |
| 边框木板主色 | `#9C7549` |
| 边框木板暗边 | `#8A6741` |
| 铁包角 | `#8A8A8A` |
| 内部填充 | `#000000`（Alpha 110） |
| 背景键控色 | `#FF00FF` |

## 视觉描述

方形边框 + 半透明深色内底，四角各一枚 2 × 2 铁灰色包角钉在木框上：

| 区域 | 内容 |
| --- | --- |
| 外描边 | 1 像素 `#1A1A1A` |
| 边框主体 | 3 像素 `#9C7549`，最内 1 像素压暗为 `#8A6741` |
| 铁包角 | 四角各 2 × 2 `#8A8A8A`，压在木框外描边内侧 |
| 内部填充 | 黑，Alpha 110 |

## AI 提示词

```
A pixel art storage chest slot frame for a game inventory UI, 1024x1024,
designed to be downscaled to 48x48 pixel art.

Shape: a square border frame centered on the canvas with only a tiny even margin.
The border is perfectly uniform in thickness on all four sides, corners are clean
right angles. The inside of the square is a flat dark fill.

Style: crisp hard-edged pixel art, no anti-aliasing, no blur, no bevel, no 3D
effect, completely flat.

Content: from outside in, a one-pixel dark outline #1A1A1A, then a three-pixel
wood-tone border band #9C7549 whose innermost pixel is the darker wood #8A6741.
On each of the four corners of the wood band sits one small two-by-two iron-gray
bracket #8A8A8A, like the metal corner fittings of a storage chest. The interior
fill is solid black, meant to sit at 43 percent opacity.

Color palette strictly limited to: #1A1A1A, #9C7549, #8A6741, #8A8A8A, #000000.

Everything outside the square frame is flat pure magenta #FF00FF, fully saturated,
hard edges, no anti-aliasing.

No items inside, no icons, no numbers, no text, no rounded corners, no bevel, no
gradient, no glow, no shadow, no watermark, no decoration other than the four
corner brackets.
```

## 负面提示词

```
items inside, icons, numbers, text, rounded corners, bevel, emboss, gradient,
glow, shadow, watermark, anti-aliasing, uneven border
```

## 后处理

1. 最近邻降采样到 48 × 48
2. 键控：外部洋红 Alpha = 0，边框 Alpha = 255
3. 量化到调色板，去洋红边
4. 内部填充 Alpha 精确设为 110
5. **校正四角包角**：确认四枚 2 × 2 铁包角位于同一边框位置（四角对称）
6. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 48 × 48
- [ ] 边框四边粗细完全相同，四角直角
- [ ] 四枚铁包角大小一致、位置四角对称
- [ ] 内部填充 Alpha 恰为 110
- [ ] 与 `hotbar-slot.png`、`ui-armor-frame.png` 并排时三者一眼可分
