# 附魔书·效率

## 用途

热键栏/背包中的附魔书（效率 I–III）图标。书本底与三变体共用，**封面符文发光色换金**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 皮暗 | `#634C33` |
| 皮主 | `#8A6741` |
| 纸页 | `#F2EAD2` |
| 符文暗（金） | `#A8842E` |
| 符文亮（金） | `#F7E08A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 三变体（sharpness/efficiency/unbreaking）同一书本底：3/4 俯视合拢书，棕皮封面 + 奶白纸页侧边 + 中央小铜扣（皮暗阶）
- 差异点只在封面中央一枚发光符文：本册为**金色小闪电形符文**（两阶金）
- 书皮色与 `book.md` 同源；符文 2–3 像素见方，不画外发光晕

## AI 提示词

```
A single enchanted book icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A closed book seen from a slight
three-quarter angle above: a brown leather cover with a small darker clasp
in the center and pale cream pages visible along the right edge. On the
front cover burns one small glowing bright gold rune shaped like a tiny
lightning bolt glyph, two tones of gold. Black 1-pixel outline. The empty
corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #634C33, #8A6741, #F2EAD2, #A8842E, #F7E08A,
#2A2620, #1A1A1A only.

No text, no watermark, no letters, no bookmark, no ribbons, no sparkles, no
large glow halo, no gradient, no cast shadow, no anti-aliasing. Hard pixel
edges only.
```

## 负面提示词

```
text, letters, runes forming words, bookmark, ribbons, sparkles, glow halo,
open pages, quill, gradient, drop shadow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 7
- 与另两册附魔书并排**书底一致**，仅封面符文为金色闪电形
