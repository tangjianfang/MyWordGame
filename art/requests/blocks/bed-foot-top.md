# 床尾俯视（bed-foot-top）

## 用途

`bed` 方块床尾半块的**顶面**贴图。俯视床尾：整幅红被面 + 木床框，与
`bed-head-top` 共用同一套红/木色，两张顶面拼起来是一整张床。

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
| 床框深木 | `#6B4E2E` |
| 床框亮木 | `#8A6741` |
| 被面深红 | `#8E2A24` |
| 被面主红 | `#A03028` |
| 被面亮红 | `#C03A30` |

红系与 `bed-head-top` 逐色相同，全部是 B < G 的暖红（同一条流水线约束）。

## 视觉描述

- 四周 1 像素木床框：左右与底边 `#6B4E2E`，顶边（接床头方向）`#8A6741`
- 内部整幅红被面：主红 `#A03028`
- 3 条横贯全宽的 1 像素 `#8E2A24` 褶线，均匀分布在第 8 / 16 / 24 行附近
- 每两条褶线之间散布 2–3 段 1 像素 `#C03A30` 亮褶短线
- 无枕头、无图案、无花纹

## AI 提示词

```
A pixel art texture of a bed top face seen from directly above, foot end,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a red blanket filling the whole square inside a 1-pixel wooden frame
on all four sides. Three dark red horizontal fold lines run across the full
width, evenly spaced. Between the fold lines sit a few short bright red fold
marks. No pillow, no pattern.

Palette strictly limited to: #6B4E2E and #8A6741 for the wooden frame, #8E2A24,
#A03028 and #C03A30 for the blanket. All reds are warm orange-leaning blood
red, never cool pink, never magenta.

This texture does not tile. Fill the whole square canvas edge to edge with
opaque pixels only, no background, no magenta anywhere.

No pillow, no person, no pattern, no polka dots, no flowers, no border frame,
no text, no watermark, no vignette, no drop shadow, no gradient, no 3D bed
perspective.
```

## 负面提示词

```
gradient, vignette, drop shadow, pillow, person, pattern, polka dots, flowers,
stripes, border frame, outline, text, watermark, blur, 3D render, perspective,
cool pink, magenta
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 核对三条褶线大致等距
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 整幅红被 + 木框，无枕头
- [ ] 颜色数 ≤ 5，全部为暖红（不存在 B > G 的像素）
- [ ] 与 `bed-head-top.png` 横向拼接，红被与木框过渡自然、色值一致
