# 箱子正面（chest-front）

## 用途

存储方块（milestone-11 第 1 波注册 `chest` 方块实体）的**正面**贴图。与
`chest-side` / `chest-top` 组成同一箱子的六个面，三张必须共用同一套木色，
正面独有的是中央铁搭扣与横贯的箱盖缝。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单面专用贴图） |
| Alpha | 无，完全不透明 |
| 颜色数 | 7 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 边框深木（最深） | `#3B2A18` |
| 箱盖/箱身阴影木 | `#5A4326` |
| 箱体主色 | `#6F5430` |
| 板条亮部 | `#8A6741` |
| 高光木 | `#9C7549` |
| 铁扣暗部 | `#5C5C5C` |
| 铁扣高光 | `#8A8A8A` |

箱木比 `planks` 更红更暗（读作「老木箱」），铁扣用 `stone` 的石灰系，不引入新色相。

## 视觉描述

- 四周 2 像素深木边框 `#3B2A18`
- 内部竖向拼 3 条木板，板与板之间 1 像素 `#5A4326` 竖缝
- 第 12–13 行一条**横贯全宽的深色盖缝** `#3B2A18`（上盖与箱身的分界），
  与 `chest-side` 的盖缝行号一致
- 盖缝中央压一枚竖长方形铁搭扣：宽约 6 像素、高约 8 像素，主体 `#5C5C5C`、
  左上角 1 像素高光 `#8A8A8A`、外圈 1 像素 `#3B2A18` 包边
- 木纹是 1 像素级的短竖线噪点，不要画木节

## AI 提示词

```
A pixel art texture of the front face of a wooden treasure chest, flat front
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a chest front made of vertical wooden planks inside a 2-pixel dark
wooden border around all four sides. A dark horizontal lid seam runs across the
full width at about 40 percent height. In the center of that seam sits one
small vertical rectangular iron latch, about 6x8 final pixels, with a 1-pixel
highlight on its upper left edge. Wood grain is subtle short vertical noise
lines, no knots.

Color palette strictly limited to: #3B2A18, #5A4326, #6F5430, #8A6741, #9C7549
for wood, #5C5C5C and #8A8A8A for the iron latch. Reddish dark brown wood,
darker and redder than plain planks.

This face does not tile; it is a single dedicated face texture. Fill the whole
square canvas edge to edge, no background, no magenta.

No border frame around the canvas, no outline, no keyhole, no lock digits, no
coins, no treasure, no 3D chest perspective, no text, no watermark, no
vignette, no drop shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border frame, outline,
keyhole, padlock, coins, gold, gems, 3D chest, open lid, perspective, text,
watermark, blur, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 7 个色值
3. 手动核对盖缝落在第 12–13 行、搭扣居中（跨缝），偏了手动平移修正
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 盖缝横贯全宽且位于第 12–13 行
- [ ] 铁搭扣跨在盖缝中央，读得出「锁」
- [ ] 颜色数 ≤ 7，无调色板外杂色
- [ ] 与 `chest-side.png` 并排，木色完全一致、盖缝行号一致
- [ ] 与 `planks.png` 并排，箱木明显更暗更红
