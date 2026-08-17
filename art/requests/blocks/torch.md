# 火把（torch）

## 用途

光源方块（milestone-11 第 1 波注册 `torch` 方块）。十字面片渲染（与花草同款），
不是整方块：贴图主体是一根竖直木杆加顶端火团，其余像素全部键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单体精灵图） |
| Alpha | 有，仅 0 或 255，**不允许中间值** |
| 透明面积 | 占全图 **60% – 75%** |
| 颜色数 | 6 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 木杆阴影 | `#634C33` |
| 木杆主色 | `#7A6042` |
| 木杆亮部 | `#8E7350` |
| 火焰根部（暗橙） | `#D64B0A` |
| 火焰主体（橙） | `#F79B22` |
| 火焰内芯（亮黄） | `#FFD24A` |
| 键控色 | `#FF00FF`（后处理删除） |

木杆三色与 `log-side` 树皮同系，火焰三色与 `lava` 同系，保证与现有贴图摆在一起不跳戏。

## 视觉描述

- 一根竖直木杆，**居中**，宽 2–3 像素，从底边向上延伸到约 65% 高度（约 20 像素）
- 杆身有 1 像素级的竖向明暗变化（受光侧 `#8E7350`、背光侧 `#634C33`），不要画木节
- 顶端火团约 8×8 像素：外圈橙 `#F79B22`、中心亮黄 `#FFD24A`、底部与杆衔接处暗橙 `#D64B0A`
- 火团轮廓圆钝，不要出现分叉、火星、烟
- 杆底可以触到贴图底边（插地方向），**左右与顶部各留 ≥2 像素洋红边**
- 背景整片纯洋红，不含渐变、不含地面、不含墙面支架

## AI 提示词

```
A pixel art sprite of a wooden torch on a solid background, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block sprite, flat shading, no perspective, crisp hard
pixel edges, no anti-aliasing.

Content: one vertical wooden torch stick, about 3 final pixels wide, centered,
rising from the bottom edge to about 65 percent of the canvas height. The stick
uses #634C33 on the dark side, #7A6042 in the middle and #8E7350 on the lit
side, with subtle vertical grain, no knots. On top of the stick sits one round
flame blob about 8x8 final pixels: bright yellow core #FFD24A in the center,
orange #F79B22 around it, dark orange #D64B0A only where flame meets stick.

The entire background is pure magenta #FF00FF, flat, fully saturated, one solid
color, hard edges, covering every pixel that is not the torch, roughly 65
percent of the canvas. No partial transparency.

The sprite does not tile. The flame never touches the top edge; keep at least 2
final pixels of magenta on the left, right and top sides.

No outline, no frame, no drop shadow, no ground, no soil, no pot, no wall
bracket, no multiple flames, no smoke, no sparks, no embers, no text, no
watermark, no gradient, no blur, no 3D render, no glow, no anti-aliasing.
```

## 负面提示词

```
gradient, drop shadow, outline, frame, smoke, sparks, embers, glow, bloom, wall
bracket, multiple torches, ground, soil, pot, candle, lantern, text, watermark,
blur, 3D render, perspective, anti-aliasing, partial transparency
```

## 后处理

1. 最近邻降采样到 32 × 32
2. **键控**：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. **去洋红边**：不透明像素中 R > G 且 B > G 的判为残留，替换为相邻主体色
4. 量化到上表 6 个色值
5. 统计透明面积，须落在 60% – 75%
6. Alpha 归为 0 / 255 两态

## 验收标准

- [ ] 尺寸恰为 32 × 32，32 位 PNG 带 Alpha 通道
- [ ] 全图 Alpha 只有 0 和 255 两种
- [ ] 透明占比 60% – 75%
- [ ] 不透明像素中无残留洋红与偏紫过渡色
- [ ] 木杆居中且触到底边，火团位于杆顶、不触顶边
- [ ] 火焰三色层次清晰（黄芯 / 橙身 / 暗橙根），读作「燃烧中」
- [ ] 与 `lava.png` 并排，火焰色系一致
