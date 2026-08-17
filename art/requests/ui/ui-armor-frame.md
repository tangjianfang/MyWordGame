# M11-U08 盔甲穿戴栏槽框（ui-armor-frame）

## 用途

milestone-11 第 2 波「盔甲穿戴栏」：装备面板里放头盔/胸甲/护腿/靴子四件盔甲的
槽位边框。结构与 `hotbar-slot`（U-02）同构（尺寸缩小一档为 32×32，
与 `postprocess_art.ASSETS` 注册值一致），仅把灰边换成**木板色**，
让玩家一眼分清「这是装备槽，不是物品槽」。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（槽位单体图） |
| Alpha | **有**：外部 0、内部半透明黑 110、边框 255 |
| 输出数量 | 1 张：`ui-armor-frame.png` |

### 九宫格边距（同 hotbar-slot 约定，按 32 尺寸等比缩小）

| 边 | 像素 |
| --- | --- |
| 上 / 下 / 左 / 右 | 各 4 像素 |

边框粗细必须四边均匀，否则拉伸后粗细不一。

## Alpha 的产出方式

槽位外部的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明；
内部填充在生成图上是深灰近黑，后处理精确置为 Alpha 110。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边（黑） | `#1A1A1A` |
| 边框木板主色 | `#9C7549` |
| 边框木板暗边 | `#8A6741` |
| 内部填充 | `#000000`（Alpha 110） |
| 背景键控色 | `#FF00FF` |

取自 `art/README.md` 全局调色板的木板黄系。

## 视觉描述

方形边框 + 半透明深色内底，与 hotbar-slot 同构：

| 区域 | 内容 |
| --- | --- |
| 外描边 | 1 像素 `#1A1A1A` |
| 边框主体 | 2 像素 `#9C7549`（内 1 像素压暗为 `#8A6741`） |
| 内部填充 | 黑，Alpha 110 |

四角直角、无任何装饰，槽内不画盔甲图案（图标由 `ItemSlotDrawer` 运行时叠加）。

## AI 提示词

```
A pixel art armor equipment slot frame for a game inventory panel, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Shape: a square border frame centered on the canvas with only a tiny even margin.
The border is perfectly uniform in thickness on all four sides and the corners are
clean right angles. The inside of the square is a flat dark fill.

Style: crisp hard-edged pixel art, no anti-aliasing, no blur, no bevel, no 3D
effect, completely flat.

Content: from outside in, a one-pixel dark outline #1A1A1A, then a two-pixel
wood-tone border band #9C7549 whose innermost pixel is the darker wood #8A6741.
The interior fill is solid black, meant to sit at 43 percent opacity so the game
world shows through faintly.

Color palette strictly limited to: #1A1A1A, #9C7549, #8A6741, #000000.

Everything outside the square frame is flat pure magenta #FF00FF, fully saturated,
hard edges, no anti-aliasing.

No rounded corners, no bevel, no emboss, no gradient, no glow, no shadow, no
armor pictures inside, no icons, no numbers, no text, no watermark, no decoration.
```

## 负面提示词

```
rounded corners, bevel, emboss, 3D, gradient, glow, drop shadow, armor icons,
helmet, chestplate, numbers, text, watermark, uneven border, anti-aliasing
```

## 后处理

1. 最近邻降采样到 48 × 48
2. 键控：外部洋红 Alpha = 0，边框 Alpha = 255
3. 量化到调色板，去洋红边
4. 内部填充 Alpha 精确设为 110
5. 逐边核对边框粗细四边一致
6. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 边框四边粗细完全相同，四角直角
- [ ] 内部填充 Alpha 恰为 110，边框与描边 Alpha 恰为 255
- [ ] 与 `hotbar-slot.png` 并排时，木色调明显区别于灰色调
- [ ] 槽内叠 16×16 盔甲图标（`ItemSlotDrawer`）后仍有清晰留边
