# 床头俯视（bed-head-top）

## 用途

`bed` 方块床头半块的**顶面**贴图（milestone-11 第 1 波注册）。俯视床头：
白枕头 + 红被面 + 木床框，是玩家认出「这是床」的关键一张。

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
| 床框深木 | `#6B4E2E` |
| 床框亮木 | `#8A6741` |
| 枕头阴影 | `#C9C4BA` |
| 枕头主色 | `#E8E4DC` |
| 被面深红 | `#8E2A24` |
| 被面主红 | `#A03028` |
| 被面亮红 | `#C03A30` |

红系全部是 B < G 的**暖红**（偏橙的血红），这样不会被后处理「去洋红边」
步骤当成洋红残留误删——冷粉/品红调的红在这条流水线上必死。

## 视觉描述

- 四周 1 像素木床框：左右与顶边 `#6B4E2E`，底边（接床尾方向）`#8A6741`
- 上半约 45% 是白枕区：主色 `#E8E4DC`，枕面 2 道 1 像素 `#C9C4BA` 横向折痕，
  枕区左右各留 2 像素红被边
- 下半约 55% 是红被面：主红 `#A03028`，靠枕处 2 行 `#8E2A24` 压痕，
  被面散布 3–4 段 1 像素 `#C03A30` 亮褶
- 枕与被的分界是一条 1 像素 `#8E2A24` 横线

## AI 提示词

```
A pixel art texture of a bed top face seen from directly above, head end,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a bed seen from above inside a 1-pixel wooden frame on all sides. The
upper 45 percent is a white pillow area with two subtle horizontal crease
lines, leaving a 2-pixel strip of red blanket on the left and right of the
pillow. The lower 55 percent is a warm red blanket with one dark red line
separating pillow from blanket, a 2-row dark red pressure mark just below the
pillow, and a few short bright red fold lines scattered on the blanket.

Palette strictly limited to: #6B4E2E and #8A6741 for the wooden frame, #C9C4BA
and #E8E4DC for the pillow, #8E2A24, #A03028 and #C03A30 for the blanket. All
reds are warm orange-leaning blood red, never cool pink, never magenta.

This texture does not tile. Fill the whole square canvas edge to edge with
opaque pixels only, no background, no magenta anywhere.

No person, no sheets pattern, no flowers pattern, no border frame, no text, no
watermark, no vignette, no drop shadow, no gradient, no 3D bed perspective.
```

## 负面提示词

```
gradient, vignette, drop shadow, person, sleeping figure, pattern, polka dots,
flowers, stripes, border frame, outline, text, watermark, blur, 3D render,
perspective, cool pink, magenta
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 7 个色值
3. 核对枕区占上 45%、居中
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 白枕在上、红被在下，一眼读出「床头」
- [ ] 颜色数 ≤ 7，全部为暖红（不存在 B > G 的像素）
- [ ] 与 `bed-foot-top.png` 并排，红被与木框色逐色一致
- [ ] 与 `bed-side.png` 并排，床框木色一致
