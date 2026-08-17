# FX-14 方块动态·火把火焰（3 帧横排）

## 用途

方块动态帧：火把顶部的燃烧火焰，方块实体动画系统按 8fps 循环播三帧。
一图三帧**横排等宽**，后处理按 1/3 宽拆帧。
**背景必须整片纯洋红键控为透明**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | **1024 × 341 生成 → 后处理拆 3 帧，各 32 × 32** |
| 生成尺寸 | 1024 × 341（宽高比 3:1，横排三格等宽） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |
| 帧序 | 左→右 = 第 1→3 帧（火苗左摆→直立→右摆） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 焰心白 | `#FFF2C8` |
| 焰黄 | `#F7DA7A` |
| 焰橙 | `#F79B22` |
| 焰根红 | `#D64B0A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

同一朵块状火把火焰的三次摆动定格，**火焰底座三帧逐像素对齐**，只有火苗尖端
变化：第 1 帧苗尖左倾、第 2 帧直立、第 3 帧右倾。火焰根部收窄、顶部圆钝，
不画火把杆（杆是方块贴图 `torch` 的事）。

## AI 提示词

```
A three-frame animation strip for a retro voxel game, wide 1024x341
pixel art designed to be split into three square frames after
downscaling. Three equal-width frames arranged side by side in one
strict horizontal row, no border between them, just equal thirds of
the wide canvas. Each frame shows the same blocky torch flame with a
bright cream-yellow flame core, orange body and red-orange base, the
flame base aligned to the bottom edge of its own frame, and only the
flame tip moving: frame 1 tip bends left, frame 2 tip stands straight
up, frame 3 tip bends right. In every frame the entire background is
solid flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing between flame and magenta.

Flame palette strictly: #FFF2C8, #F7DA7A, #F79B22, #D64B0A only.

No torch handle, no wood, no stick, no text, no smoke, no shadow, no
outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, frame borders, grid lines, watermark, torch handle, stick, wood,
smoke, shadow, outline, blur, gradient, 3D render, candle, lantern,
gray background
```

## 后处理

1. 最近邻降采样整幅到 96 × 32（1024×341 → 三等分 32×32）
2. **按 1/3 宽拆为 3 张独立帧图**（`anim-torch-flame-0/1/2`）
3. 每帧键控（洋红 > 50% → Alpha = 0）
4. 去洋红边
5. 量化到上表 4 个色值

## 验收

- [ ] 生成图 1024×341，三帧等宽、无帧间分隔线
- [ ] 拆帧后各为 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 4，无偏紫残留
- [ ] 三帧火焰底部基线逐像素对齐（循环播放不上下跳动）
- [ ] 火焰宽度 ≤ 16 像素，火把方块模型放得下
