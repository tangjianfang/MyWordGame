# FX-11 魔法阵·治疗环

## 用途

魔法阵特效：食物/药水回血、床睡觉回血时在脚下展开的绿色治疗环。
绿色系（与生命/自然魔法同族）。**背景必须整片纯洋红键控为透明**。

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
| 环绿 | `#52963B` |
| 环亮 | `#A8E89A` |
| 心亮 | `#F2F8E8` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

俯视正圆治疗环：外环四段断裂的鲜绿弧（留缺口），内一圈薄白绿细环，
中心近白柔光圆，外环四段弧的中点各缀一颗小绿点（叶芽感）。
不画十字、不画心形——那是 UI 层的事。

## AI 提示词

```
A single magic circle sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A flat top-down healing ring: a
fresh green outer ring broken into four arc segments with even gaps, a
pale mint-white thin inner ring, a soft near-white glowing center disc,
and one tiny green dot at the middle of each outer arc segment.
Perfectly symmetrical and centered. The entire background is solid flat
pure magenta #FF00FF, fully saturated, hard edges, no anti-aliasing
between ring and magenta.

Ring palette strictly: #52963B, #A8E89A, #F2F8E8 only.

No letters, no text, no cross symbol, no heart shape, no shadow, no
perspective tilt, no blur, no semi-transparent pixels. Hard pixel
edges.
```

## 负面提示词

```
text, letters, cross, heart, watermark, shadow, outline, blur,
gradient, 3D render, perspective, isometric, flowers, leaves, grass,
gray background
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
- [ ] 四段弧等长、缺口等宽（旋转 90° 基本重合）
