# P-02 表情·睡觉

## 用途

玩家状态图标：睡觉状态（床/挂机睡眠）HUD 左上角的状态标记之一。
六个表情图标共用玩家皮肤（`P-01 skin.md`）的同一套脸部色系，
保证头像与第三人称模型逐色一致。**背景必须整片纯洋红键控为透明**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 皮肤亮 | `#E3B08A` |
| 皮肤主 | `#C98F68` |
| 皮肤暗 | `#A06F4E` |
| 头发 | `#3B2A1C` |
| 线黑 | `#1A1A1A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

玩家的方形头正脸（与皮肤 8×8 脸同构放大）：深棕头发盖顶与两侧、
额前一排短刘海，**双眼闭合成两条水平黑短线**，嘴微微张开成小圆口
（打鼾）。不画「Z z z」字样——README 禁止 AI 画字。

## AI 提示词

```
A single player face status icon for a retro voxel game HUD, 1024x1024
pixel art designed to be downscaled to 32x32. A square blocky human
head viewed from the front: short dark brown hair covering the top and
sides with a small fringe, light tan skin with slightly darker shading
on the right and bottom edges, both eyes closed as flat dark horizontal
lines, and a small round open snoring mouth. The entire background is
solid flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing between head and magenta.

Palette strictly: #E3B08A, #C98F68, #A06F4E (skin), #3B2A1C (hair),
#1A1A1A (lines) only.

No text, no letters, no hat, no helmet, no body, no shoulders, no
shadow, no outline, no blur, no semi-transparent pixels. Hard pixel
edges.
```

## 负面提示词

```
text, letters, zzz, watermark, shadow, outline, blur, gradient, 3D
render, hat, body, shoulders, pillow, bed, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 5 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 5
- [ ] 无偏紫残留
- [ ] 头部色块与 skin.md 脸部区域逐色一致（同一张脸）
