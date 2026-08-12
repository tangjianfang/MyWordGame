# B-19 铁门

## 用途

可放置、可开合的铁门方块。**贴图本身只表示"门关着"的形态**——开合姿态由 UV 在格子
里偏转实现，贴图始终是关闭时的正视面。

铁门是本项目里**贴图只占方块一部分**的特殊方块：铁门本身是 1×2 的双格结构，每格 32×32
贴图里只画一扇门，门框周围是透明（被空气方块替代）。这是 brief 规定的 alpha_range=
(0.85, 0.95) 的设计依据，门看起来是"嵌在空气里的铁门"。

## Alpha 的产出方式

同 B-12：把透明区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | 占全图 **85% – 95%**（门只占一小块） |
| 颜色数 | 4 – 6 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 铁暗（铆钉 / 描边） | `#1A1A1A` |
| 铁锈深 | `#5C5C5C` |
| 铁锈主 | `#7A6042` |
| 铁锈亮 | `#8A6A4C` |
| 铁锈高光 | `#B9906B` |
| 透明键控色 | `#FF00FF`（后处理删除） |

整扇门**应读作生锈的铁门**，必须用 RUSTY BROWN 色——不能用紫色、蓝色、灰色。

## 视觉描述

正前方看到的铁门面板——**只占贴图中央偏下**的一小块。

- 门主体只占大约 **第 6–24 列、第 8–30 行**（约 18×22 像素），周围全是洋红键控
- 门本身是**长方形铁门**，由 2 条竖直分缝把门面分成左中右三块板
- 每块板上有 1–2 个**圆形铆钉**（直径 2–3 像素，颜色 `#1A1A1A` 描边 + `#5C5C5C` 中心）
- 板的边缘用 `#1A1A1A` 描 1 像素宽的细边
- 板内**必须用 RUSTY BROWN 颜色**（#7A6042 / #8A6A4C / #B9906B），可以有一些细短横纹
- 门顶（第 8 行）可以有一道横梁（1 像素 `#1A1A1A`）
- 门底（第 30 行）可以有一道门槛（1 像素 `#1A1A1A`）
- 不要画完整的窗户或玻璃区域
- 不要画花纹、不要画 logo

## AI 提示词

```
A pixel art game icon for a 32x32 tile, 1024x1024 final, downscaled to 32x32
pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a small iron door occupying ONLY the central lower portion of the
canvas, roughly columns 6-24 and rows 8-30 (about 18x22 pixels in the 32x32
final). The door is divided into three vertical metal panels by two dark
vertical seams at columns 12 and 18. Each panel has 1-2 small round rivets
near its corners. The right-most panel has a small dark door handle in its
center-left area, about 2x4 pixels. Panel edges have a 1-pixel dark outline.

CRITICAL COLOR: The door body must be RUSTY BROWN (#7A6042, #8A6A4C, #B9906B)
with darker details (#1A1A1A, #5C5C5C). The door must be BROWN/RUST colored,
NOT purple, NOT blue, NOT gray. Brown/rust must dominate.

The ENTIRE remaining canvas area (about 85-90 percent of the image) is filled
with flat pure magenta #FF00FF as a chroma key background. The magenta is fully
saturated, hard edges, no anti-aliasing against the door.

Color palette: #1A1A1A (rivets, handle, outline), #5C5C5C, #7A6042, #8A6A4C,
#B9906B (rusty brown), and #FF00FF for the background. RUSTY BROWN IRON, not
purple, not blue, not stainless, not red.

The door must be RUSTY BROWN, not purple. The rest is magenta.

No window, no glass, no scrollwork, no decoration, no logo, no text, no
watermark, no vignette, no drop shadow, no gradient, no 3D render, no chrome,
no blue, no purple.
```

## 负面提示词

```
window, glass, wrought iron, scrollwork, decoration, floral pattern, logo,
emblem, keyhole, lock, wood, plank, chain, gradient, vignette, lighting
direction, drop shadow, border, frame, chrome, mirror, stainless steel,
copper, brass, gold, magenta background, magenta fill, purple, blue, text,
watermark, blur, 3D render, perspective, photorealistic, glowing, neon
```

## 后处理

1. 最近邻降采样到 32 × 32
2. **键控**：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. **去洋红边**：不透明像素中若有偏紫过渡色，替换为最近调色板色
4. 量化到上表 5 个色值
5. 统计透明占比，须落在 85% – 95%
6. 偏移半幅自检接缝（门缝出现在边缘必须对齐）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 32 位 PNG，Alpha 只有 0 和 255
- [ ] Alpha = 0 的像素占比在 85% – 95%
- [ ] 不透明像素中不存在任何偏紫像素
- [ ] 门主体是 RUSTY BROWN 颜色，不是紫色
- [ ] 右侧板可见一个深色把手
- [ ] 整体读作"生锈的铁门"，不是铜门也不是不锈钢门

## 负面提示词

```
window, glass, wrought iron, scrollwork, decoration, floral pattern, logo,
emblem, keyhole, lock, wood, plank, chain, gradient, vignette, lighting
direction, drop shadow, border, frame, chrome, mirror, stainless steel,
copper, brass, gold, magenta background, magenta fill, purple, blue, text,
watermark, blur, 3D render, perspective, photorealistic, glowing, neon
```

## 后处理

1. 最近邻降采样到 32 × 32
2. **键控**：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. **去洋红边**：不透明像素中若有偏紫过渡色，替换为最近调色板色
4. 量化到上表 5 个色值
5. 统计透明占比，须落在 85% – 95%
6. 偏移半幅自检接缝（门缝出现在边缘必须对齐）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 32 位 PNG，Alpha 只有 0 和 255
- [ ] Alpha = 0 的像素占比在 85% – 95%
- [ ] 不透明像素中不存在任何偏紫像素
- [ ] 门主体是 RUSTY BROWN 颜色，不是紫色
- [ ] 右侧板可见一个深色把手
- [ ] 整体读作"生锈的铁门"，不是铜门也不是不锈钢门
