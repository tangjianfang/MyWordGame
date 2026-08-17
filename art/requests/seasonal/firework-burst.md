# SEASON-04 烟花绽放特效

## 用途

节日夜空的**烟花绽放粒子精灵**：春节/庆典时撒在夜空里的那一朵。
走特效粒子渲染，洋红背景键控（同 A3 effects 范式），32×32 单体帧。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵） |
| Alpha | 有，仅 0 或 255（**背景整片纯洋红**） |
| 颜色数 | 4 – 5 色（不含洋红） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 白心 | `#FFF3C4` |
| 金芒 | `#F7DA7A` |
| 橙焰尖 | `#F79B22` |
| 落火星 | `#D64B0A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

- 一朵居中占约 70% 画幅的放射状烟花：
  - 中心一小簇白心
  - **8-12 根粗壮的金色放射芒**，从白心向外张开，末端各一颗橙色焰尖点
  - 芒间散几粒橙色落火星（坠落感）
- 芒是粗锥形像素条，直而对称，像纸风车/星花，不是细线
- 背景整片纯洋红，主体四周留均匀洋红边距
- 无烟、无地面、无建筑

## AI 提示词

```
A single pixel art firework burst sprite for festival night sky
particles, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, symmetric radial composition,
hard pixel edges, no anti-aliasing.

Content: one firework burst centered filling about 70 percent of the
frame: a small warm white core, eight to twelve chunky golden rays
spreading outward, each ray a thick tapered pixel spear ending in one
orange tip dot, plus a few loose orange ember dots falling between the
rays. The rays are straight and symmetric like a stylized star flower or
pinwheel.

Palette strictly: core #FFF3C4, rays #F7DA7A, tips #F79B22, embers
#D64B0A.

Everything outside the burst is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing, with an even magenta margin on
all sides.

No text, no letters, no smoke, no ground, no buildings, no watermark,
no glow halo, no gradient, no 3D render, no blur, no thin hair lines.
```

## 负面提示词

```
smoke, ground, buildings, text, letters, watermark, glow halo,
gradient, 3D render, blur, thin lines, tiny dots only, off-center
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明，去洋红边
3. 量化到上表 4 个色值
4. 校验放射芒根根连通到白心（无断芒）

## 验收标准

- [ ] 尺寸恰为 32 × 32，32 位 PNG，Alpha 只有 0/255
- [ ] 主体居中、左右大致对称
- [ ] 深色夜空背景（`#0B1026`）上预览无紫边、无白边
- [ ] 颜色数 ≤ 5，图中无文字无烟雾
