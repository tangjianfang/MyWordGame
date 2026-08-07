# B-08 圆石

## 用途

石头被挖掘后掉落并可再放置的建材，玩家最早能大量获得的方块。会被用来盖房子、搭桥、
封洞口，**大面积出现**，因此同样要耐看；但它必须与 `stone` 一眼就能区分开，
否则玩家分不清哪里是自己挖过的。

区分手段是**结构**而不是颜色：石头是均匀颗粒，圆石是可辨认的鹅卵石块 + 深色缝隙。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 6 – 10 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 缝隙（最深） | `#4A4A4A` |
| 石块阴影 | `#5C5C5C` |
| 石块主色 | `#7E7E7E` |
| 石块亮部 | `#8F8F8F` |
| 石块高光 | `#A3A3A3` |

整体平均明度要比 `stone` **略低一点**（缝隙拉低了平均值），这是正常的，不要刻意提亮。
不得引入任何带色相的颜色。

## 视觉描述

由 **6–9 块大小不等的圆润石块**拼成，石块之间是 1–2 像素宽的深色缝隙。

- 石块轮廓是圆角多边形，不要画成正圆，也不要画成规整的矩形
- 每块石头内部有轻微的明暗变化（亮部偏左上、暗部偏右下），但**幅度要小**，
  不能让整张图看起来有统一光源方向
- 石块大小要有差异：最大的约 12×12 像素，最小的约 5×5 像素，随机分布
- 缝隙必须连通成网，不要出现孤立的短线段
- 不要画苔藓、不要画裂纹、不要描黑边

## AI 提示词

```
A seamless tileable pixel art texture of gray cobblestone, top-down flat view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination across the entire image, no global light direction.

Content: 6 to 9 rounded cobblestones of varying sizes packed together, separated by
thin dark mortar gaps that form a connected network. Each stone has a very subtle
internal shading variation. Stone outlines are rounded irregular polygons, not
perfect circles and not rectangles.

Color palette strictly limited to these neutral grays: #4A4A4A, #5C5C5C, #7E7E7E,
#8F8F8F, #A3A3A3. No blue tint, no brown tint.

CRITICAL: The texture must tile seamlessly on all four edges. Stones that touch the
left edge must continue on the right edge, and stones touching the top edge must
continue on the bottom edge.

No border, no frame, no black outline, no moss, no cracks, no text, no watermark,
no vignette, no drop shadow, no gradient across the image.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, black outline,
moss, cracks, ore, text, watermark, signature, blur, 3D render, perspective, glossy,
wet, uniform grid, perfectly circular stones, brick pattern
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值（可保留至多 3 个中间灰阶）
3. 检查缝隙宽度：降采样后仍应有 1 像素以上的深色缝，糊成一片就要重做
4. Alpha 全部置为 255
5. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后画面中央看不出接缝，跨接缝的石块轮廓自然衔接
- [ ] 颜色数 ≤ 10，全部为中性灰（R、G、B 三通道差值 ≤ 4）
- [ ] 与 `stone.png` 并排放大对比，**在 2 秒内能分辨出哪个是圆石**
- [ ] 4×4 平铺预览中看不出「每格一个相同石块」的规律感
