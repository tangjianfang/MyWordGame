# M11-C23 收集卡 · 机元矿（card-ore-essence）

## 用途

milestone-11 图鉴收集卡：**机元矿**的标本插画，垫在三档卡框挖空区内。
统一构图：深色底 + 居中矿石方块正面（占 70%），不透明、无框。
机元是 Boss 链的关键矿，这张卡预计配 `card-frame-epic`。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无，完全不透明 |
| 输出数量 | 1 张：`card-ore-essence.png` |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 背景深色 | `#3A342A` |
| 石底主色 | `#8A8A8A` |
| 石底暗斑 | `#6E6E6E` |
| 石底亮斑 | `#A3A3A3` |
| 机元斑暗 | `#5B2A8C` |
| 机元斑主色 | `#8C4FD4` |
| 机元斑高光 | `#C9A6F5` |
| 标本描边（2px） | `#17140F` |

机元斑三色与 `machine-essence-ore`（M10-B）逐色一致——紫罗兰，
不能偏粉也不能偏蓝。

## 视觉描述

同 `card-ore-gold` 构图，矿物斑换成紫罗兰机元斑（暗边亮心）。

## AI 提示词

```
A pixel art mineral specimen card illustration for a game codex, 1024x1024,
designed to be downscaled to 64x64 pixel art.

Style: chunky pixel art, flat shading, straight-on front view like a block
texture, no perspective, no gradient, no glow.

Content: a flat dark backdrop fills the whole square. Centered on it sits one
square ore block face taking about 70 percent of the canvas: a gray stone base
with faint darker and lighter speckles, and four to five large irregular mineral
blobs of the featured mineral spread over the face. The block face is wrapped in
a two-pixel near-black outline #17140F.

Stone palette strictly: #8A8A8A base, #6E6E6E speckles, #A3A3A3 speckles.
Backdrop: #3A342A.

The mineral blobs are violet purple machine energy: #5B2A8C, #8C4FD4, #C9A6F5
only. True violet purple, not pink, not blue, not magenta.

No text, no numbers, no card frame, no border around the canvas, no shine
marks, no glow, no 3D render, no watermark.
```

## 负面提示词

```
text, numbers, card frame, canvas border, shine marks, glow, 3D, watermark
```

## 后处理

1. 最近邻降采样到 64 × 64
2. 量化到 8 个色值
3. 核对方块居中、四边留边一致
4. 存为 PNG-32（不透明）

## 验收标准

- [ ] 尺寸恰为 64 × 64，完全不透明
- [ ] 机元斑三色与 `machine-essence-ore.png` 同观感
- [ ] 紫色不偏粉、不偏蓝、不含洋红
- [ ] 叠进 `card-frame-epic` 挖空区后紫卡紫框仍有描边分隔
