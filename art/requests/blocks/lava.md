# B-16 岩浆

## 用途

洞穴深处的危险与光源。岩浆是本项目第一个**自发光**方块（光照等级 15），
它照亮周围的洞壁，也是玩家往下挖时最需要一眼认出的东西。

因为它同时承担「危险信号」和「光源」两个职责，**亮度和饱和度必须明显高于所有其它方块**。

## 静态版本（本批次）

Minecraft 的岩浆是 20 帧动画。本批次先做**单帧静态版**，够用且不阻塞渲染层开发。
将来做流动动画时再单独提「岩浆动画图集」需求（32×32 × N 帧竖排）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 – 8 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 结壳（最深） | `#5C1400` |
| 暗红 | `#8A2400` |
| 主色橙红 | `#D64B0A` |
| 亮橙 | `#F79B22` |
| 炽黄（最亮） | `#FFD24A` |

明度跨度**故意拉大**，这是全项目对比度最高的贴图。炽黄面积要少（占 5%–10%），
多了会变成一块黄板。

## 视觉描述

熔融岩浆表面，暗色结壳裂开露出下面的炽热熔流。

- **结壳**：由 `#5C1400` / `#8A2400` 构成的不规则暗块，占面积 35%–50%
- **熔流**：结壳之间的裂缝网络，由 `#D64B0A` → `#F79B22` → `#FFD24A` 由外向内递亮
- 裂缝宽 2–4 像素，**连通成网**并且贯穿到图片四条边（这是无缝的关键）
- 炽黄只出现在裂缝最宽处的中心，呈短线段而非圆点
- 不要画火焰、不要画烟、不要画气泡冒出
- 不要有整体的方向性流动感（会在平铺时形成条纹）

## AI 提示词

```
A seamless tileable pixel art texture of molten lava seen from above, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, no camera
lighting; the surface is self-illuminated.

Content: dark cooled crust plates broken apart by a connected network of glowing
molten cracks. The crust is dark maroon and covers roughly 40 percent of the image.
The cracks glow brighter toward their center, going from orange red to bright orange
to hot yellow. Hot yellow appears only in the widest parts of the cracks as short
streaks, never as round dots, and stays under 10 percent of the image.

Color palette strictly limited to: #5C1400, #8A2400, #D64B0A, #F79B22, #FFD24A.
No white, no pink, no purple.

CRITICAL: The texture must tile seamlessly on all four edges. Every glowing crack
that reaches an edge must continue on the opposite edge.

No flames, no fire, no smoke, no steam, no bubbles, no sparks, no embers flying,
no border, no frame, no outline, no text, no watermark, no vignette, no drop shadow,
no directional flow, no whirlpool, no bloom, no glow halo.
```

## 负面提示词

```
flames, fire, smoke, steam, bubbles, sparks, embers, glow halo, bloom, lens flare,
whirlpool, directional flow, lava fall, white hot core, pink, purple, gradient,
vignette, drop shadow, border, frame, outline, text, watermark, blur, 3D render,
perspective, photorealistic, reflection
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 统计炽黄 `#FFD24A` 占比，超过 10% 就把外围的炽黄替换为亮橙
4. 检查裂缝在四条边上的位置，偏移半幅自检必须能接上
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后裂缝网络自然连通，无断头
- [ ] 颜色数 ≤ 8，全部在给定调色板内（允许至多 2 个中间色）
- [ ] 炽黄占比 5% – 10%，暗结壳占比 35% – 50%
- [ ] 与 `water.png` 并排，两者的「液体感」风格一致（同样的裂纹/波纹颗粒度）
- [ ] 把整张图整体乘 0.3 模拟暗处，仍然一眼认得出是岩浆
  （因为它自发光，实际渲染不会变暗，这里只是检查形状辨识度）
