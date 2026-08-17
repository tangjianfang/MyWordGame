# FX-10 魔法阵·传送

## 用途

魔法阵特效：传送指令在脚下展开的俯视魔法阵，2s 展开动画的完整体。
紫色系（与附魔/传送卷轴同族）。**背景必须整片纯洋红键控为透明**。

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
| 外环紫 | `#6A4A9C` |
| 内环紫 | `#9C6AE8` |
| 亮紫 | `#C8A8F2` |
| 中心亮 | `#F4EEFC` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

俯视正圆魔法阵：深紫粗外环、亮紫内环带缺口刻痕（似符文但**不构成文字**）、
近白发光圆心、两条亮紫细十字辐条连内外环。严格同心对称。

## AI 提示词

```
A single magic circle sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A flat top-down teleport magic
circle: a deep purple outer ring, a medium purple inner ring with small
notched rune-like ticks, a bright near-white glowing center disc, and a
thin light purple cross connecting the rings. Perfectly symmetrical and
centered. The entire background is solid flat pure magenta #FF00FF,
fully saturated, hard edges, no anti-aliasing between circle and
magenta.

Circle palette strictly: #6A4A9C, #9C6AE8, #C8A8F2, #F4EEFC only.

No letters, no real words, no text, no readable characters, no portal
scenery, no shadow, no perspective tilt, no blur, no semi-transparent
pixels. Hard pixel edges.
```

## 负面提示词

```
text, letters, readable runes, watermark, shadow, outline, blur,
gradient, 3D render, perspective, isometric, portal, swirl, demon,
gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 4 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 4
- [ ] 无偏紫残留
- [ ] 圆心在画布几何中心 ±1 像素内（贴地展开不偏心）
