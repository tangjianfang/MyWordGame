# FX-07 战斗·爆炸（3 帧横排）

## 用途

战斗特效：苦力怕爆炸、TNT、Boss 落地冲击的三帧动画。一图三帧**横排等宽**，
后处理按 1/3 宽拆帧。**背景必须整片纯洋红键控为透明**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | **1024 × 341 生成 → 后处理拆 3 帧，各 32 × 32** |
| 生成尺寸 | 1024 × 341（宽高比 3:1，横排三格等宽） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |
| 帧序 | 左→右 = 第 1→3 帧（起爆→全爆→余烬） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 焰心白 | `#FFF2C8` |
| 焰黄 | `#F7DA7A` |
| 焰橙 | `#F79B22` |
| 焰红 | `#D64B0A` |
| 烬暗 | `#8A2400` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

同一团卡通爆炸的三次定格，火焰基线对齐每帧底部：

- 第 1 帧：刚起爆——小而亮的黄橙火球，白心
- 第 2 帧：全爆——最大的橙色爆环 + 白热核心
- 第 3 帧：余烬——塌缩成暗红烟团，只剩零星橙色火星

## AI 提示词

```
A three-frame animation strip for a retro voxel game, wide 1024x341
pixel art designed to be split into three square frames after
downscaling. Three equal-width frames arranged side by side in one
strict horizontal row, no border between them, just equal thirds of
the wide canvas. Each frame shows the same cartoon explosion with its
base on the bottom edge: frame 1 a small bright yellow-orange fireball
with a white hot core, frame 2 a huge orange blast ring with a glowing
white center, frame 3 a crumbling dark red-orange smoke cloud with only
a few orange sparks left. In every frame the entire background is solid
flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing between explosion and magenta.

Explosion palette strictly: #FFF2C8, #F7DA7A, #F79B22, #D64B0A,
#8A2400 only.

No text, no numbers, no fire outside its own frame third, no shadow, no
outline, no blur, no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, numbers, frame borders, grid lines, watermark, shadow, outline,
blur, gradient, 3D render, smoke rising like a chimney, crater, debris
chunks, gray background
```

## 后处理

1. 最近邻降采样整幅到 96 × 32（1024×341 → 三等分 32×32）
2. **按 1/3 宽拆为 3 张独立帧图**（`fx-explosion-0/1/2`）
3. 每帧键控（洋红 > 50% → Alpha = 0）
4. 去洋红边
5. 量化到上表 5 个色值

## 验收

- [ ] 生成图 1024×341，三帧等宽、无帧间分隔线
- [ ] 拆帧后各为 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 5，无偏紫残留
- [ ] 三帧爆炸底部基线在同一水平位置（逐帧循环播放不跳动）
