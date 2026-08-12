# B-17 树苗

## 用途

小树苗方块，可被放置在泥土/草地上、随时间长成大树。是本项目**第一个尺寸细小的方块**，
但贴图仍按 32×32 输出、再在材质上叠 16×16 的视觉密度——在世界里只占 1 格高度，
所以贴图中间区域才有内容，四周大半是透明背景。

## Alpha 的产出方式

同 B-12：生成时把透明区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **不要求无缝** |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | 占全图 **50% – 70%** |
| 颜色数 | 4 – 6 色（不含透明） |

主体只占中央一小块（32×32 里大约 12×16），周围全是洋红键控底，
比 B-12 / B-13 的全屏镂空更稀疏——树苗贴在地面看就是一小撮。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 叶子深 | `#3F7A2E` |
| 木棍（茎） | `#634C33` |
| 泥土底色 | `#5F4630` |
| 叶亮 | `#52963B` |
| 木棍暗 | `#4E3826` |
| 透明键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

中央**一根细木棍**（竖直，约 1–2 像素宽，6–8 像素高），顶端冒出**3–4 片小叶子**（每片约
3×3 像素），底端坐落在**一小块土块**（约 6×3 像素）上。

- 木棍必须**严格竖直**，不能弯不能斜
- 叶子只能长在木棍顶端，不能从两侧或底部冒出来
- 土块只画在木棍正下方一小撮，不要铺满整张图
- 主体的水平中心必须落在 32×32 的**正中央**，垂直中心略偏下（约第 18–20 行）
  ——这样树苗种在地里看起来是"插"在土里的，不是悬空的
- 不要画根、不要画影子、不要画光圈

## AI 提示词

```
A pixel art game icon for a 32x32 tile, 1024x1024 final, downscaled to 32x32
pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a LARGE CHUNKY sapling plant filling 50 PERCENT of the image area.
The plant body (dirt + stem + leaves) must occupy HALF of the total image, with
the magenta background only on the FAR LEFT and FAR RIGHT sides. The plant must
NOT be a tiny speck — it must be a substantial, chunky plant.

The seedling sits slightly below vertical center (around row 18-20 of the 32
pixel grid). It has three parts:

  - a brown dirt mound at the bottom, about 18 pixels wide and 6 pixels tall
  - a thick vertical wooden stem rising from the dirt, 4 pixels wide and 16
    pixels tall, perfectly vertical
  - a big green leaf cluster at the top, total leaf cluster about 16x12 pixels
    with several overlapping leaves

The magenta background should be visible only as margins on the left and right
sides — about 6-8 pixels wide. The plant fills the central column from edge to
edge in vertical direction.

Color palette strictly limited to: dirt #5F4630, stem dark #4E3826, stem
brown #634C33, leaves dark #3F7A2E, leaves bright #52963B.

The plant must be VERY LARGE, filling half the image. No roots, no shadow, no
halo, no glow, no text, no watermark, no gradient, no 3D.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
halo, glow, roots, underground, soil cross-section, shadow, dirt background,
mature tree, branches, fruit, flowers, snow, autumn colors, text, watermark,
signature, blur, 3D render, perspective, glossy, transparent background,
checkerboard, curved stem, diagonal stem, multiple stems, side branches
```

## 后处理

1. 最近邻降采样到 32 × 32
2. **键控**：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. **去洋红边**：不透明像素中若有偏紫过渡色，替换为最接近的调色板色
4. 量化到上表 5 个色值
5. 统计透明占比，须落在 50% – 70%
6. 不透明像素必须**全部连通到中心横向 ±4 像素带**，否则会被渲染成"飞起来的叶子"

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 32 位 PNG，Alpha 只有 0 和 255
- [ ] Alpha = 0 的像素占比在 50% – 70%
- [ ] 不透明像素中不存在任何偏紫像素
- [ ] 木棍严格竖直，从最底下第一片土块连贯到顶端叶子
- [ ] 水平方向上不透明像素的算术中心落在第 14–18 列之间
- [ ] 与 `leaves.png` 并排看，树苗明显是"小植物 + 土块"，而不是单纯的叶子团
