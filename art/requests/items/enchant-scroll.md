# 附魔卷轴

## 用途

热键栏/背包中的附魔卷轴图标（存储/转移附魔的消耗品）。

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
| 纸主 | `#F2EAD2` |
| 纸侧 | `#D9C89A` |
| 卷边 | `#A8842E` |
| 符文暗 | `#7A5A9A` |
| 符文亮 | `#9B6FD4` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 视角横放卷轴：两端向内卷成小卷筒（卷边暗金阶），中段平铺羊皮纸
- 纸面中央一枚紫色符文印记（两阶紫，与附魔书符文同族）
- 纸色与 `book.md`/`notebook.md` 纸页同源，卷轴上不写字

## AI 提示词

```
A single enchanted scroll icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A parchment scroll seen from a slight
three-quarter angle, lying horizontally: a pale cream parchment sheet with
both ends rolled inward into small curls with slightly darker aged tan
edges, and one small purple sigil mark of two purple tones stamped in the
center of the sheet. Black 1-pixel outline. The empty corners are one flat
solid dark backdrop color #2A2620.

Color palette strictly: #F2EAD2, #D9C89A, #A8842E, #7A5A9A, #9B6FD4,
#2A2620, #1A1A1A only.

No text, no watermark, no letters, no writing lines, no wax seal, no
ribbons, no sparkles, no gradient, no cast shadow, no anti-aliasing. Hard
pixel edges only.
```

## 负面提示词

```
text, letters, writing lines, wax seal, ribbons, quill, sparkles, glow,
gradient, drop shadow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 7
- 两端卷筒与中央紫印记可见，无文字
