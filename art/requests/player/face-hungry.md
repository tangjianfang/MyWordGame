# P-03 表情·饥饿

## 用途

玩家状态图标：饥饿值见底时的警示状态。脸部色系对齐 `P-01 skin.md`。
**背景必须整片纯洋红键控为透明**。

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

同一颗玩家方头正脸：**双眼半阖下压**（上眼皮横线盖住半只眼），
嘴是下弯的波浪短撇，两颊下侧各压一块皮肤暗色（消瘦感）。
无口水无食物——那是图标层的语义，表情本身要「蔫」。

## AI 提示词

```
A single player face status icon for a retro voxel game HUD, 1024x1024
pixel art designed to be downscaled to 32x32. A square blocky human
head viewed from the front: short dark brown hair covering the top and
sides with a small fringe, light tan skin with slightly darker shading
on the right and bottom edges and extra darker patches on the lower
cheeks, two droopy half-closed tired eyes shown as heavy dark eyelid
lines over small dark pupils, and a small downturned wavy mouth. The
entire background is solid flat pure magenta #FF00FF, fully saturated,
hard edges, no anti-aliasing between head and magenta.

Palette strictly: #E3B08A, #C98F68, #A06F4E (skin), #3B2A1C (hair),
#1A1A1A (lines) only.

No text, no food, no drool, no hat, no body, no shoulders, no shadow,
no outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, food, chicken leg, drool, shadow, outline, blur,
gradient, 3D render, hat, body, shoulders, gray background
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
- [ ] 眼与嘴都在脸的下半区，与 face-happy 并排可分清情绪
