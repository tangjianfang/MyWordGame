# B-10 原木（顶面 + 侧面）

一次需求包含**两张贴图**：`log-top`（年轮切面）与 `log-side`（树皮）。
它们必须看起来是同一棵树，所以放在同一份需求里一起生成、一起验收。

## 用途

树干方块。树是玩家在世界里最先看到的地表结构，也是最早的资源来源。
原木的六个面里，上下用 `log-top`，四个侧面用 `log-side`。

## 平铺要求（两张不同，注意区分）

| 贴图 | 平铺要求 | 原因 |
| --- | --- | --- |
| `log-top` | **不需要无缝**，但必须**上下左右对称到边缘不突兀** | 树干顶面通常只露出 1 格，很少大面积平铺 |
| `log-side` | **左右无缝 + 上下无缝** | 树干竖直堆叠，上下接缝会连成一条树；横排的树林也会左右相邻 |

`log-side` 的上下无缝尤其重要：一棵树是 4–6 格原木竖着叠起来的，
上下接不上会在每格之间出现明显的横线。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 各 32 × 32 像素 |
| 生成尺寸 | 各 1024 × 1024（再降采样） |
| Alpha | 无，完全不透明 |
| 颜色数 | 各 6 – 10 色 |

## 调色板（严格使用）

树皮（`log-side`）：

| 用途 | HEX |
| --- | --- |
| 沟槽最深 | `#3B2C1C` |
| 阴影 | `#4E3B27` |
| 主色 | `#634C33` |
| 亮部 | `#7A6042` |
| 高光 | `#8E7350` |

年轮切面（`log-top`）：

| 用途 | HEX |
| --- | --- |
| 树皮环（外圈） | `#4E3B27` |
| 木心阴影 | `#8A6A45` |
| 木心主色 | `#A5814F` |
| 木心亮部 | `#BE9A63` |
| 中心点 | `#8A6A45` |

## 视觉描述

### `log-side` 树皮

竖直方向的树皮纹理。

- 由 **4–6 条竖直沟槽**构成，沟槽宽 2–4 像素，位置随机、宽窄不一
- 沟槽必须**从图片顶边一直贯穿到底边**（这是上下无缝的关键），中途可以有粗细变化但不能断
- 沟槽之间是稍亮的树皮脊，脊上有细碎的短横向纹理（1 像素）增加质感
- 不要画树节、不要画藤蔓、不要画苔藓
- 不要有左右方向的整体明暗渐变

### `log-top` 年轮

- 最外圈是 **2–3 像素宽的深色树皮环**，沿着 32×32 的**正方形边缘**走一圈（不是圆形！
  因为方块是立方体，顶面是正方形）
- 环内是同心的年轮：**3–4 圈**，圈与圈之间明暗交替，圈是**圆角矩形**而非正圆
- 中心有一个 2×2 像素的深色髓心
- 年轮不要画得太密，降采样后会糊成一团

## AI 提示词

### `log-side`

```
A seamless tileable pixel art texture of tree bark, flat front view, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: vertical tree bark with 4 to 6 dark vertical grooves of varying width that
run continuously from the very top edge to the very bottom edge without breaking.
Between the grooves are slightly lighter bark ridges with fine short horizontal
grain marks. Natural irregular spacing, no repetition within the image.

Color palette strictly limited to these browns: #3B2C1C, #4E3B27, #634C33, #7A6042,
#8E7350. Warm brown, no red tint, no gray tint.

CRITICAL: The texture must tile seamlessly both horizontally and vertically. Every
vertical groove must exit the bottom edge exactly where it enters the top edge.

No border, no frame, no outline, no knots, no moss, no vines, no mushrooms, no text,
no watermark, no vignette, no drop shadow, no gradient across the image.
```

### `log-top`

```
A pixel art texture of a tree trunk cross-section seen from directly above,
1024x1024, designed to be downscaled to 32x32 pixel art. Square format, the wood
fills the entire square edge to edge.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a dark brown bark ring runs along the four straight edges of the square.
Inside it, 3 to 4 concentric growth rings shaped as rounded squares alternate
between lighter and darker wood tones, ending at a small dark pith at the center.
Rings are clean, well separated, and not too thin.

Color palette strictly limited to: #4E3B27 (bark ring), #8A6A45, #A5814F, #BE9A63.

The bark ring must follow the square border, NOT a circle. The wood must reach all
four edges with no background showing.

No round frame, no circular vignette, no background, no outline glow, no cracks,
no text, no watermark, no drop shadow, no perspective, no 3D render.
```

## 负面提示词（两张通用）

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline, moss,
vines, mushrooms, leaves, snow, text, watermark, signature, blur, depth of field,
3D render, perspective, glossy, photorealistic, circular crop, transparent background
```

## 后处理

1. 两张分别最近邻降采样到 32 × 32
2. 各自量化到对应调色板
3. `log-side` **必做上下偏移半幅自检**，接不上直接重做
4. `log-top` 检查最外圈树皮环在四条边上宽度一致
5. Alpha 全部置为 255

## 跨资源一致性

- `log-top` 最外圈的树皮色必须与 `log-side` 的主色 `#634C33` / 阴影 `#4E3B27` 同系，
  否则从侧面转到顶面会明显变色
- `log-top` 的木心色 `#A5814F` 应比 `planks`（B-11）的主色稍暗，
  这样「原木做成木板」在视觉上说得通

## 验收标准

- [ ] 两张尺寸均恰为 32 × 32
- [ ] `log-side` 上下偏移半幅后，竖直沟槽完全对齐无错位
- [ ] `log-side` 左右偏移半幅后无竖直接缝
- [ ] `log-top` 四条边的树皮环宽度一致，无圆形裁切痕迹
- [ ] 两张并排看颜色属于同一棵树
- [ ] 无任何像素 Alpha < 255
- [ ] 把 `log-side` 竖直叠 5 格预览，看不出格与格之间的横线
