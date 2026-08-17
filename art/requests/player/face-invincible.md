# P-05 表情·无敌

## 用途

玩家状态图标：复活无敌帧/药水无敌期间的状态（与 `RespawnAtSpawn` 的
3 秒无敌帧联动）。脸部色系对齐 `P-01 skin.md`，附加金色光效。
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
| 金光 | `#F7DA7A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

同一颗玩家方头正脸：**双眼睁大有神**（黑瞳居中），
嘴角上扬的自信微笑；头外圈围一圈断续的金色光点（能量光环），
头顶一颗金色四芒星。金色取全局调色板金黄系，与命中火花/成就同记忆色。

## AI 提示词

```
A single player face status icon for a retro voxel game HUD, 1024x1024
pixel art designed to be downscaled to 32x32. A square blocky human
head viewed from the front: short dark brown hair covering the top and
sides with a small fringe, light tan skin with slightly darker shading
on the right and bottom edges, two confident wide-open eyes with dark
pupils, and a small confident smile. Around the head floats a broken
ring of golden glow dots, plus one golden four-point star above the
head. The entire background is solid flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing between head and magenta.

Palette strictly: #E3B08A, #C98F68, #A06F4E (skin), #3B2A1C (hair),
#1A1A1A (lines), #F7DA7A (gold glow) only.

No text, no shield, no armor, no hat, no body, no shoulders, no shadow,
no outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, shield, armor, knight helmet, watermark, shadow, outline, blur,
gradient, 3D render, body, shoulders, lightning, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 6 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 6
- [ ] 无偏紫残留
- [ ] 金色只出现在光环与星点，脸部本体与其它表情图逐色一致
