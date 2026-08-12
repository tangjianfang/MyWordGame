# B-21 红石粉

## 用途

红石粉是红石电路的载体——撒在地面上、附着在方块侧面、互相连接形成电路。本贴图就是
"红石粉颗粒散落在石头底面上"的俯视图。

平铺**频繁且大面积**：一长条红石电路里会有几十格红石粉并排、上下层叠。
因此这块贴图对**平铺无缝**的要求是所有方块里最高的之一。

## 平铺要求

| 平铺要求 | 原因 |
| --- | --- |
| **四边无缝**（严苛） | 红石电路常常一条直线铺十几格，任何接缝都会露馅 |

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 – 7 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边 / 暗钉 | `#1A1A1A` |
| 石头底 | `#7E7E7E` |
| 红石暗 | `#A02020` |
| 红石主 | `#D03030` |
| 红石亮 | `#E84040` |

整张图读作"灰色石板 + 红色粉粒"，红色必须是**红石红**——比砖红稍冷、稍深，不要像
砖块那样偏橙。

## 视觉描述

- **底层**：与 `lever` 共用的石头底色 `#7E7E7E`（带 `#1A1A1A` 描边和 `#5C5C5C` 暗角），
  占满整张图
- **上层**：8–14 个**红色小颗粒**，散布在石头底上
  - 单个颗粒直径 **2–3 像素**，形状不规则（接近椭圆或圆角矩形）
  - 颗粒之间**互相不接触**（间距至少 1 像素）
  - 颗粒**不能触碰第 0 行/第 31 行/第 0 列/第 31 列**——边缘 2 像素内不允许有红石粉，
    否则平铺时会接成连续的红色块，看不出颗粒感
  - 颗粒位置要**错落**，不能落在网格交点上形成规律阵列
  - 每个颗粒是 `#D03030` 主色 + `#A02020` 描边 + 中心偶尔 `#E84040` 高光
- 不要画线条、不要画箭头、不要画电路连接痕迹

## AI 提示词

```
A seamless tileable pixel art texture of redstone dust scattered on a gray stone
surface, top-down flat view, 1024x1024, designed to be downscaled to 32x32 pixel
art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: a gray stone background fills the entire canvas. On top of the stone
there are about 10 small irregular red dust particles, each 2-3 pixels in
diameter, scattered randomly across the surface. Particles do NOT touch each
other (gap of at least 1 pixel between them) and do NOT touch the outermost
2 pixels of any edge — particles keep a clear margin from all four edges so
that when the texture is tiled the particles do not visually merge across
seams. Particle positions are staggered, not on a grid. Each particle is dark
red outlined, medium red filled, with an occasional single bright red highlight
pixel at its center.

Color palette strictly limited to: stone background #7E7E7E (with #5C5C5C and
#1A1A1A for stone detail), and red dust: #A02020 (dark), #D03030 (main),
#E84040 (highlight).

CRITICAL: tiles seamlessly on all four edges with no visible seam. The 2-pixel
border on every edge must be PURE stone background — no red particles in this
margin zone.

No lines, no arrows, no circuit traces, no wire connections, no glow, no spark,
no animation, no text, no watermark, no vignette, no drop shadow, no gradient,
no 3D render, no perspective.
```

## 负面提示词

```
lines, arrows, circuit, wire connection, glowing, spark, animation, frames,
gradient, vignette, drop shadow, border, frame, outline around whole image,
pink tint, orange tint, brick red, brown red, dark red background, magenta,
text, watermark, signature, blur, 3D render, perspective, photorealistic,
grid pattern, particles on grid intersections, particles touching edges
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. **强制清边**：第 0–1 行 / 第 30–31 行 / 第 0–1 列 / 第 30–31 列必须全部是
   石头底色（不允许有任何红石颗粒），否则手动涂掉
4. **强制检查颗粒间距**：相邻两颗红石颗粒的最小曼哈顿距离 ≥ 2 像素，
   否则手动调整位置
5. 偏移半幅自检接缝（要求严苛，比值阈值 ≤ 1.4 而不是默认 1.6）
6. Alpha 全部置为 255

## 跨资源一致性

- 石头底色 `#7E7E7E` 与 `lever` 的石头底色一致，使红石粉铺在拉杆旁边时不会有"两种石头"

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 8–14 个红石颗粒散布在石头底上
- [ ] 边缘 2 像素带内 100% 是石头底色，**无任何红石颗粒**
- [ ] 相邻颗粒间距 ≥ 2 像素
- [ ] 颗粒位置错落，不形成网格阵列
- [ ] 颜色数 ≤ 7
- [ ] 偏移半幅后接缝比值 ≤ 1.4（红石粉平铺要求最严）
- [ ] 与 `lever` 并排看，石头底色完全一致
- [ ] 无任何像素 Alpha < 255
