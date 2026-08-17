# FX-13 魔法阵·护盾

## 用途

魔法阵特效：无敌帧/药水减伤期间罩住玩家的球形护盾泡，受击时闪亮一帧。
青白系（玻璃青白同族，读作「透明的能量」）。**背景必须整片纯洋红键控为透明**。

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
| 泡内淡 | `#A9CBD6` |
| 泡体 | `#C8E2EA` |
| 边亮 | `#E7F4F8` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一个正面圆球形护盾泡：清晰亮白的圆形轮廓线（球缘），球内大部分区域**留空**
（键控透明，渲染时叠加半透明），仅左上一道月牙形浅蓝反光弧和一颗小亮点。
内空外实——护盾是「罩」不是「饼」。

## AI 提示词

```
A single shield effect sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A round protective force bubble
seen from the front: a clean bright white-blue circular rim, a pale
blue crescent sheen arc on the upper left inside the rim, and one small
bright sparkle dot, with the middle of the sphere left as background.
Centered on the canvas. The entire background including the middle of
the bubble is solid flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing between rim and magenta.

Shield palette strictly: #A9CBD6, #C8E2EA, #E7F4F8 only.

No text, no knight, no crest, no hexagon, no shadow, no outline outside
the bubble, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, hexagon
shield, knight, sword, armor, solid filled sphere, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 3 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 3
- [ ] 无偏紫残留
- [ ] 球心大片透明（不透明像素围成环带），叠在玩家身上不糊脸
