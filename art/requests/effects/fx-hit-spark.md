# FX-09 战斗·命中火花

## 用途

战斗特效：攻击命中生物/方块瞬间的四芒火花，与闪红反馈（MobHitFeedback）
同帧出现，0.1s 消散。**背景必须整片纯洋红键控为透明**。

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
| 芯白 | `#FFFFFF` |
| 芒黄 | `#F7DA7A` |
| 芒金 | `#DCAE3A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一枚四芒星火花：中心小的纯白四角星，四条主芒向外伸展为金黄/金色两层，
芒尖锐利。金白取全局调色板的金黄系，与经验/成就的视觉记忆一致。

## AI 提示词

```
A single combat effect sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A star-shaped hit spark burst: a
small pure white four-point star core with golden yellow outer points
and medium gold accents between the points, sharp pointed tips.
Centered on the canvas. The entire background is solid flat pure
magenta #FF00FF, fully saturated, hard edges, no anti-aliasing between
spark and magenta.

Spark palette strictly: #FFFFFF, #F7DA7A, #DCAE3A only.

No text, no blood, no sword, no hand, no shadow, no outline, no blur,
no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, blood,
sword, impact crater, lightning bolt, gray background
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
- [ ] 旋转 90° 后与原图基本重合（四芒对称）
