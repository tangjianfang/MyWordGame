# 箱子侧面（chest-side）

## 用途

`chest` 方块的**侧面**贴图。与 `chest-front` 共用同一套木色；没有铁搭扣，
但保留同一条箱盖横缝，让箱子四边转角处盖缝连成一体。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单面专用贴图） |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 边框/盖缝（最深） | `#3B2A18` |
| 竖缝阴影木 | `#5A4326` |
| 箱体主色 | `#6F5430` |
| 板条亮部 | `#8A6741` |
| 高光木 | `#9C7549` |

与 `chest-front` 完全同系的木色（去掉铁扣两色），两张是同一只箱子。

## 视觉描述

- 四周 2 像素深木边框 `#3B2A18`
- 内部竖向拼 3 条木板，板间 1 像素 `#5A4326` 竖缝
- 第 12–13 行**盖缝横贯全宽** `#3B2A18`，行号与 `chest-front` 严格一致
- 盖缝上下各 1 行 `#5A4326` 的受缝阴影，做出盖子压住箱身的层次
- 木纹为 1 像素短竖线噪点，无木节、无金属件

## AI 提示词

```
A pixel art texture of the side face of a wooden treasure chest, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a chest side made of vertical wooden planks inside a 2-pixel dark
wooden border around all four sides. One dark horizontal lid seam runs across
the full width at about 40 percent height, with a 1-pixel shadow line just
above and below the seam. Wood grain is subtle short vertical noise lines, no
knots, no metal parts, no latch.

Color palette strictly limited to: #3B2A18, #5A4326, #6F5430, #8A6741, #9C7549.
Reddish dark brown wood, matching the chest front exactly.

This face does not tile; it is a single dedicated face texture. Fill the whole
square canvas edge to edge, no background, no magenta.

No border frame around the canvas, no outline, no latch, no lock, no metal, no
coins, no 3D chest perspective, no text, no watermark, no vignette, no drop
shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border frame, outline,
latch, lock, metal, hinges, coins, 3D chest, perspective, text, watermark,
blur, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 手动核对盖缝行号与 `chest-front` 一致（第 12–13 行）
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 盖缝横贯全宽且行号与 `chest-front` 一致
- [ ] 无任何金属/锁具元素
- [ ] 颜色数 ≤ 5
- [ ] 与 `chest-front.png` 并排，木色逐色一致，拼在一起像同一只箱子
