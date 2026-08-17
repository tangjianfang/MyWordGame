# 附魔书·耐久

## 用途

热键栏/背包中的附魔书（耐久 I–III）图标。书本底与三变体共用，**封面符文发光色换蓝**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 背景 | 整片纯洋红 `#FF00FF`，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 皮暗 | `#634C33` |
| 皮主 | `#8A6741` |
| 纸页 | `#F2EAD2` |
| 符文暗（蓝） | `#2C5893` |
| 符文亮（蓝） | `#4E88CE` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 三变体（sharpness/efficiency/unbreaking）同一书本底：3/4 俯视合拢书，棕皮封面 + 奶白纸页侧边 + 中央小铜扣（皮暗阶）
- 差异点只在封面中央一枚发光符文：本册为**蓝色小盾形符文**（两阶蓝）
- 书皮色与 `book.md` 同源；符文 2–3 像素见方，不画外发光晕
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single enchanted book icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A closed book seen from a slight
three-quarter angle above: a brown leather cover with a small darker clasp
in the center and pale cream pages visible along the right edge. On the
front cover burns one small glowing bright blue rune shaped like a tiny
shield glyph, two tones of blue. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #634C33, #8A6741, #F2EAD2, #2C5893, #4E88CE, #1A1A1A outline only.

No text, no watermark, no letters, no bookmark, no ribbons, no sparkles, no
large glow halo, no gradient, no cast shadow, no anti-aliasing. Hard pixel
edges only.
```

## 负面提示词

```
text, letters, runes forming words, bookmark, ribbons, sparkles, glow halo,
open pages, quill, gradient, drop shadow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 与另两册附魔书并排**书底一致**，仅封面符文为蓝色盾形
