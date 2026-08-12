# B-20 拉杆

## 用途

红石基础元件——一个石头底座加一根可拨动的木柄。本贴图表示**默认朝向（"关"状态）**：
木柄竖直立在石头底上。后续拉杆被玩家点击时，旋转到水平方向是另一回事，**不在本贴图
表达范围**。

拉杆是本项目里**唯一正面被大量特写**的红石元件，因此细节比红石粉更讲究。

## 平铺要求

| 平铺要求 | 原因 |
| --- | --- |
| **四边无缝** | 红石装置里多个拉杆会并排，竖直接缝不能断 |

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 – 7 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边 / 钉子 | `#1A1A1A` |
| 石头底深 | `#5C5C5C` |
| 石头底主 | `#7E7E7E` |
| 石头底亮 | `#8A8A8A` |
| 木柄主 | `#634C33` |
| 木柄亮 | `#8A6741` |

## 视觉描述

- **下半**（约第 18–31 行）：一个**椭圆形石头底座**，占满下半区域
  - 底座横跨整张图（左右边缘各贴 0 像素，即**第 0 列与第 31 列都必须有底座色**——
    这样平铺时相邻拉杆的底座会无缝连成一片基岩墙）
  - 底座顶部有一道 1 像素的 `#1A1A1A` 暗边，让它有立体感
  - 底座中间有一个 `#5C5C5C` 圆钉孔（深色小圆，直径约 3 像素）
- **中央**：从底座中央长出一根**竖直木柄**（约 2 像素宽，从第 8 行到第 22 行）
  - 木柄必须**严格竖直**穿过画面正中央（第 15–16 列）
  - 木柄顶端（第 8 行附近）有一个**小球头**（直径 3–4 像素，#8A6741），
    代表被拨动的部分
- **上半**（约第 0–7 行）：背景区域，是石头底色 `#7E7E7E` 的延伸 / 木柄背后的墙，
  不要画天空

## AI 提示词

```
A seamless tileable pixel art texture of a Minecraft-style lever in the closed /
default position, flat front view, 1024x1024, designed to be downscaled to
32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: rows 18-31 of the image are a horizontal oval gray stone base that
touches both the left edge and the right edge so that adjacent levers tile into
a continuous gray wall. The base has a 1-pixel dark top edge and a small dark
nail hole in its center.

A thin vertical wooden stick rises straight up from the base center (2 pixels
wide, columns 15-16), reaching from row 22 to row 8. At the top of the stick
sits a small round wooden knob (3-4 pixels, brighter wood tone).

Rows 0-7 background is the same gray stone color as the base, NOT sky blue,
NOT transparent.

Color palette strictly limited to: stone dark #5C5C5C, stone main #7E7E7E,
stone bright #8A8A8A, wood main #634C33, wood bright #8A6741, outlines pure
#1A1A1A.

CRITICAL: tiles seamlessly on all four edges. Stone base touches both
leftmost and rightmost columns. The wooden stick is perfectly vertical,
columns 15-16, never tilted.

No sky, no transparent background, no grass, no redstone wire, no glow, no halo,
no text, no watermark, no vignette, no drop shadow, no gradient, no 3D render,
no perspective, no diagonal stick.
```

## 负面提示词

```
sky, blue background, transparent background, grass, ground, redstone wire, red
dust, glow, halo, lens flare, decorative scrollwork, gradient, vignette, drop
shadow, border, frame, outline around whole image, diagonal stick, bent stick,
broken lever, text, watermark, signature, blur, 3D render, perspective,
photorealistic, chrome, gold, neon, multiple levers, multiple knobs
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 6 个色值
3. **强制检查**：第 15、16 列（中央两列）必须是连续的木柄色（从第 8 行到第 22 行），
   不连贯则手动补线
4. **强制检查**：第 0 列与第 31 列必须有石头底色（不能是黑边 / 紫边），
   否则平铺时会出现黑缝
5. 偏移半幅自检接缝
6. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 石头底座左右两端必须贴到第 0 列 / 第 31 列
- [ ] 木柄严格竖直，第 15–16 列连续不间断
- [ ] 木柄顶端有一颗比柄亮的圆头
- [ ] 颜色数 ≤ 7
- [ ] 偏移半幅后接缝看不出破绽
- [ ] 与 `redstone_dust` 并排看，拉杆的石头底色与红石粉的石头底色一致
- [ ] 无任何像素 Alpha < 255
