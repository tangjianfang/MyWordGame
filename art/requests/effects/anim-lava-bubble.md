# FX-16 方块动态·岩浆泡

## 用途

方块动态帧：岩浆面随机鼓起、将破未破的气泡，动画系统随机时长循环
「鼓起→爆开→再鼓起」。单帧图（非帧序列，爆开瞬间由程序缩放本图实现）。
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
| 浆暗 | `#8A2400` |
| 浆主 | `#D64B0A` |
| 泡亮 | `#F79B22` |
| 泡顶金 | `#F7DA7A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一颗鼓起将爆的岩浆泡：圆穹状暗红底托上鼓一颗橙色圆泡，泡顶一圈金黄
月牙高光（泡壁最薄处）。色板取全局调色板岩浆橙系，与 lava 方块无缝衔接。

## AI 提示词

```
A single lava animation sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. One bulging lava bubble about
to pop: a round dark red-orange lava dome with a bright orange surface
and a glowing golden-yellow crescent at the top rim, sitting on a flat
dark red base strip at the bottom. Centered on the canvas. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing between lava and magenta.

Lava palette strictly: #8A2400, #D64B0A, #F79B22, #F7DA7A only.

No splash, no sparks flying off, no stone, no obsidian, no text, no
shadow, no outline, no blur, no semi-transparent pixels. Hard pixel
edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, volcanic
eruption, lava lake, lava fall, stone, obsidian, smoke, gray background
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
- [ ] 底边是平直基线，与 lava 方块表面贴合无缝
