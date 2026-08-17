# 木楼梯（stairs-planks）

## 用途

木质楼梯方块的贴图（第 1 波注册 `stairs-planks`）。与 `planks` 同版式
（4 条水平板、板缝对齐上下边），区别是**板缝更深、板面有磨损亮条**——
被脚步磨过的楼梯踏面。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（上下缝对齐板条边界） |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 板缝（最深） | `#6B4E2E` |
| 阴影木纹 | `#8A6741` |
| 主色 | `#9C7549` |
| 亮部 | `#B98D57` |
| 高光 | `#CFA66B` |

与 `planks` **逐色相同**——同一批木料，楼梯和墙板拼在一起不跳色。

## 视觉描述

- **4 条水平木板**，每条 8 像素高（与 `planks` 同版式）
- 板缝比 `planks` 更重：1 像素 `#6B4E2E` 缝线两侧再各压 1 行
  `#8A6741` 阴影（踏步边缘被踩出凹感）
- 每条板中部一条 1–2 像素的**磨损亮条** `#CFA66B`（被鞋底磨亮的横带），
  长度为板宽的 60%–80%，四条板上的亮条左右位置错开
- 木纹仍为水平 1 像素短线，木节靠边规则同 `planks`
- 顶边第 0 行 / 底边第 31 行是板条边界，上下平铺缝对缝

## AI 提示词

```
A seamless tileable pixel art texture of worn wooden stair planks, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: exactly 4 horizontal wooden planks of equal height stacked from top
to bottom, separated by thin dark seam lines, each seam shadowed by one extra
row of darker wood below and above so the steps read slightly hollowed by
footsteps. In the middle of each plank runs a worn polished band of the
brightest color, 60 to 80 percent of the plank width, horizontally, staggered
left-right between planks. Subtle horizontal wood grain lines and one or two
small knots kept away from the edges. The top edge is the start of the first
plank and the bottom edge the end of the last, so seams line up when tiled
vertically.

Color palette strictly limited to: #6B4E2E, #8A6741, #9C7549, #B98D57,
#CFA66B. Golden brown wood, exactly the same family as plain planks.

CRITICAL: The texture must tile seamlessly on all four edges. Horizontal grain
continues across the left and right edges; plank seams align across the top
and bottom edges.

No border, no frame, no outline, no nails, no screws, no vertical planks, no
diagonal grain, no step geometry, no 3D stairs, no text, no watermark, no
vignette, no drop shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
nails, screws, bolts, metal, vertical planks, diagonal wood, parquet, step
geometry, 3D stairs, text, watermark, blur, 3D render, perspective,
photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查板高恰为 4 × 8 像素，不均手动修正
4. 上下偏移半幅自检板缝对齐
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 恰好 4 条板、每条 8 像素高，上下偏移半幅缝对缝
- [ ] 每板一条错位的磨损亮条（与 `planks` 的区分点）
- [ ] 颜色数 ≤ 5，与 `planks.png` 并排逐色一致
