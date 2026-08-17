# 箱子顶面（chest-top）

## 用途

`chest` 方块的**顶面**贴图。俯视箱盖：同款深木边框 + 横纹盖板，与正/侧面
组成完整箱子。顶面没有搭扣，只有盖板的拼板横纹。

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
| 边框/板缝（最深） | `#3B2A18` |
| 拼板阴影木 | `#5A4326` |
| 盖板主色 | `#6F5430` |
| 板条亮部 | `#8A6741` |
| 高光木 | `#9C7549` |

与 `chest-front` / `chest-side` 同一套木色。

## 视觉描述

- 四周 2 像素深木边框 `#3B2A18`
- 内部**横向**拼 3 条盖板（每条约 9 像素高），板间 1 像素 `#3B2A18` 横缝
- 每条盖板有 1–2 道 1 像素高的浅木纹 `#8A6741` / `#9C7549`，长短不一
- 无锁、无金属、无木节
- 四角 2×2 像素是边框的角榫（全部 `#3B2A18`），暗示箱角包边

## AI 提示词

```
A pixel art texture of the top face of a wooden treasure chest seen from
directly above, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a chest lid made of horizontal wooden boards inside a 2-pixel dark
wooden border around all four sides. Three horizontal boards of equal height
fill the inside, separated by thin dark seam lines. Each board carries one or
two subtle horizontal grain lines of varying length. The four corner squares of
the border are solid dark wood, like corner joints. No metal, no latch, no
knots.

Color palette strictly limited to: #3B2A18, #5A4326, #6F5430, #8A6741, #9C7549.
Reddish dark brown wood, matching the chest front and side exactly.

This face does not tile; it is a single dedicated face texture. Fill the whole
square canvas edge to edge, no background, no magenta.

No border frame around the canvas, no outline, no latch, no lock, no hinges, no
3D chest perspective, no text, no watermark, no vignette, no drop shadow, no
gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border frame, outline,
latch, lock, hinges, metal, 3D chest, perspective, open lid, text, watermark,
blur, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 核对三条盖板高度均匀（约 9 像素），不均手动修正
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 恰好 3 条横向盖板，高度均匀
- [ ] 四角有实心深色角榫
- [ ] 颜色数 ≤ 5
- [ ] 与 `chest-front.png` / `chest-side.png` 并排，木色逐色一致
