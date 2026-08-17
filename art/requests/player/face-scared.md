# P-07 表情·害怕

## 用途

玩家状态图标：夜间怪物逼近/Boss 战低血量时的惊恐状态。脸部色系对齐
`P-01 skin.md`，附加一滴冰蓝冷汗。**背景必须整片纯洋红键控为透明**。

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
| 眼白 | `#F2F2F2` |
| 线黑 | `#1A1A1A` |
| 冷汗蓝 | `#A9CBD6` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

同一颗玩家方头正脸：**双眼瞪大到极限**——大片眼白配中央一粒小黑瞳，
嘴张成竖长小椭圆，右上额角一滴冰蓝冷汗（水滴形）。
冷汗蓝取玻璃青白系，唯一的外加色。

## AI 提示词

```
A single player face status icon for a retro voxel game HUD, 1024x1024
pixel art designed to be downscaled to 32x32. A square blocky human
head viewed from the front: short dark brown hair covering the top and
sides with a small fringe, light tan skin with slightly darker shading
on the right and bottom edges, two huge wide-open shocked eyes with
large white areas and tiny dark pupils in the center, a small vertical
oval open mouth, and one pale blue teardrop of cold sweat on the upper
right forehead. The entire background is solid flat pure magenta
#FF00FF, fully saturated, hard edges, no anti-aliasing between head
and magenta.

Palette strictly: #E3B08A, #C98F68, #A06F4E (skin), #3B2A1C (hair),
#F2F2F2 (eye white), #1A1A1A (lines), #A9CBD6 (sweat) only.

No text, no monster, no ghost, no hat, no body, no shoulders, no
shadow, no outline, no blur, no semi-transparent pixels. Hard pixel
edges.
```

## 负面提示词

```
text, monster, ghost, skeleton, watermark, shadow, outline, blur,
gradient, 3D render, hat, body, shoulders, multiple sweat drops, gray
background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 7 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 7
- [ ] 无偏紫残留
- [ ] 冷汗恰好一滴且在右上额角；瞪大眼的眼白面积明显大于其它表情
