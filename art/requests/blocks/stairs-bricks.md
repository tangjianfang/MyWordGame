# 砖楼梯（stairs-bricks）

## 用途

砖质楼梯方块的贴图（第 1 波注册 `stairs-bricks`）。与 `bricks` 同色同
砖型，区别是**灰浆缝加宽、砖角磨圆**——旧建筑上被踩磨的砖台阶。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 灰浆缝 | `#9A9086` |
| 砖阴影 | `#6B3226` |
| 砖主色 | `#8B4433` |
| 砖亮部 | `#A05242` |
| 砖高光 | `#B3634C` |

与 `bricks` **逐色相同**，保证砖墙与砖楼梯拼用不跳色。

## 视觉描述

- 砖型与 `bricks` 一致：**4 行砖、错缝砌筑**，砖高 8 像素、宽 16 像素，
  奇偶行错开半砖
- 本贴图灰浆缝**加宽到 2 像素**（`bricks` 为 1 像素），是楼梯的识别点
- 每块砖的四角**磨圆 1 像素**（角上被灰浆替代），砖面有 1–2 个
  1 像素小坑（磨损）
- 上下左右边缘都落在灰浆缝中线上，四边平铺错缝自然衔接
- 不画裂纹、不画苔藓

## AI 提示词

```
A seamless tileable pixel art texture of worn brick stair surface, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: a running-bond brick pattern of 4 rows, each brick 8 final pixels
tall and 16 pixels wide, odd rows offset by half a brick. Mortar lines are 2
pixels wide, slightly wider than normal bricks. Every brick corner is rounded
by 1 pixel, eaten into the mortar, and each brick face carries one or two
1-pixel pits like old wear. No cracks, no moss. All four edges of the image
run along the middle of mortar lines so the offset pattern connects when
tiled.

Color palette strictly limited to: #9A9086 for mortar, #6B3226, #8B4433,
#A05242, #B3634C for bricks. Same brick red family as plain bricks.

CRITICAL: The texture must tile seamlessly on all four edges; the running bond
offset must continue across the seams.

No border, no frame, no outline, no cracks, no moss, no ivy, no stones, no
step geometry, no 3D stairs, no text, no watermark, no vignette, no drop
shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
cracks, moss, ivy, plants, stones, cobblestone, step geometry, 3D stairs,
text, watermark, blur, 3D render, perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查 4 行砖、错缝正确、灰浆缝 2 像素，偏了手动修正
4. 四边偏移半幅自检错缝衔接
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 4 行砖错缝砌筑，灰浆缝 2 像素（宽于 `bricks`）
- [ ] 砖角磨圆、砖面有小磨损坑
- [ ] 颜色数 ≤ 5，与 `bricks.png` 并排逐色一致
- [ ] 偏移半幅后错缝自然衔接
