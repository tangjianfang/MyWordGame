# M11-C20 收集卡 · 金矿（card-ore-gold）

## 用途

milestone-11 图鉴收集卡：**金矿**的标本插画，垫在三档卡框（`card-frame-*`）
的挖空区内。8 张收集卡统一构图：深色底 + 居中标本（占 70%），
不透明、无框——框由卡框另叠。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无，完全不透明 |
| 输出数量 | 1 张：`card-ore-gold.png` |

矿石卡画的是**方块正面**，非矿脉、非矿锭——与世界里挖到的方块同观感，
孩子一眼对上「我挖到过它」。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 背景深色 | `#3A342A` |
| 石底主色 | `#8A8A8A` |
| 石底暗斑 | `#6E6E6E` |
| 石底亮斑 | `#A3A3A3` |
| 金斑暗 | `#A87322` |
| 金斑主色 | `#DCAE3A` |
| 金斑高光 | `#F7DA7A` |
| 标本描边（2px） | `#17140F` |

金斑三色与 `gold-ore`（B-14）逐色一致。

## 视觉描述

- 整幅 64×64 平铺深色底 `#3A342A`
- 中央一块约 45×45 的方形金矿石正面：石底 + 稀疏噪点，
  4~5 团大颗不规则金斑（每团 6~10 像素，暗边亮心）
- 方块外圈 2 像素黑描边

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

The mineral blobs are rich metallic gold yellow: #A87322, #DCAE3A, #F7DA7A
only. Warm bright yellow-gold, not pale yellow, not brown, not orange.

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
4. 存为 PNG-32（不透明，Alpha 全 255）

## 验收标准

- [ ] 尺寸恰为 64 × 64，完全不透明
- [ ] 金斑三色与 `gold-ore.png` 同观感
- [ ] 叠进 `card-frame-common` 挖空区后四边不贴框
- [ ] 与其余 4 张矿卡并排时构图一致
