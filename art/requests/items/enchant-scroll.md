# 附魔卷轴

## 用途

热键栏/背包中的附魔卷轴图标（存储/转移附魔的消耗品）。

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
| 纸主 | `#F2EAD2` |
| 纸侧 | `#D9C89A` |
| 卷边 | `#A8842E` |
| 符文暗 | `#7A5A9A` |
| 符文亮 | `#9B6FD4` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 视角横放卷轴：两端向内卷成小卷筒（卷边暗金阶），中段平铺羊皮纸
- 纸面中央一枚紫色符文印记（两阶紫，与附魔书符文同族）
- 纸色与 `book.md`/`notebook.md` 纸页同源，卷轴上不写字
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single enchanted scroll icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A parchment scroll seen from a slight
three-quarter angle, lying horizontally: a pale cream parchment sheet with
both ends rolled inward into small curls with slightly darker aged tan
edges, and one small purple sigil mark of two purple tones stamped in the
center of the sheet. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #F2EAD2, #D9C89A, #A8842E, #7A5A9A, #9B6FD4, #1A1A1A outline only.

No text, no watermark, no letters, no writing lines, no wax seal, no
ribbons, no sparkles, no gradient, no cast shadow, no anti-aliasing. Hard
pixel edges only.
```

## 负面提示词

```
text, letters, writing lines, wax seal, ribbons, quill, sparkles, glow,
gradient, drop shadow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 两端卷筒与中央紫印记可见，无文字
