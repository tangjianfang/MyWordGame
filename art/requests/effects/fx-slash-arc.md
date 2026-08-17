# FX-05 战斗·斩击弧光

## 用途

战斗特效：近战挥击的弧光面片，随攻击方向旋转贴在准星前方一闪即逝。
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
| 弧心白 | `#FFFFFF` |
| 内缘蓝白 | `#E7F4F8` |
| 外缘冰蓝 | `#C8E2EA` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一道新月形斩击弧：从左上扫到右下，弧带中段最厚、两端收成尖角，
白色弧心夹两层蓝白/冰蓝描边。无刀身、无手——纯能量弧。

## AI 提示词

```
A single combat effect sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A single crescent slash arc
sweeping from upper left to lower right: a thick pure white core band
with a pale blue-white inner edge and a light ice-blue outer edge, sharp
pointed tips at both ends. Centered on the canvas. The entire background
is solid flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing between arc and magenta.

Arc palette strictly: #FFFFFF, #E7F4F8, #C8E2EA only.

No text, no sword, no hand, no blood, no shadow, no outline, no blur,
no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, sword
blade, katana, hand, arm, blood, motion lines, gray background
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
- [ ] 弧的两端是尖的，中段最厚——旋转 90° 后仍是「一道弧」
