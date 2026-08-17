# M11-C25 收集卡 · 樱花（card-plant-cherry）

## 用途

milestone-11 图鉴收集卡：**樱花**的标本插画，垫在三档卡框挖空区内。
植物卡统一构图：深色底 + 居中植株标本（占 70%），不透明、无框——
与矿石卡的「方块正面」区分，植物是「整株标本」。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无，完全不透明 |
| 输出数量 | 1 张：`card-plant-cherry.png` |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 背景深色 | `#3A342A` |
| 枝干棕 | `#634C33` |
| 花瓣粉主色 | `#F2C4CE` |
| 花芯深粉 | `#E8A0B4` |
| 标本描边（2px） | `#17140F` |

## 视觉描述

- 整幅 64×64 平铺深色底 `#3A342A`
- 中央一枝斜上的樱花枝（约占 70%）：棕色枝干，
  三四簇粉白花团（每簇 6~10 朵五瓣小花，花芯点深粉）
- 整株标本包 2 像素黑描边

## AI 提示词

```
A pixel art cherry blossom specimen card illustration for a game codex,
1024x1024, designed to be downscaled to 64x64 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no gradient,
no glow.

Content: a flat dark backdrop fills the whole square. Centered on it stands one
cherry blossom branch filling about 70 percent of the canvas: a brown branch
#634C33 reaching diagonally upward, carrying three or four clusters of small
five-petal blossoms in soft pink #F2C4CE, each blossom center marked with a
darker pink dot #E8A0B4. The whole branch specimen is wrapped in a two-pixel
near-black outline #17140F. Backdrop: #3A342A.

Color palette strictly limited to: #3A342A, #634C33, #F2C4CE, #E8A0B4, #17140F.

No falling petals, no ground, no soil, no leaves, no text, no numbers, no card
frame, no border around the canvas, no glow, no 3D render, no watermark.
```

## 负面提示词

```
falling petals, ground, soil, leaves, text, numbers, card frame, canvas border,
glow, 3D, watermark
```

## 后处理

1. 最近邻降采样到 64 × 64
2. 量化到 5 个色值
3. 核对标本居中、四边留边一致
4. 存为 PNG-32（不透明）

## 验收标准

- [ ] 尺寸恰为 64 × 64，完全不透明
- [ ] 花簇可辨（能看到花芯深粉点）
- [ ] 叠进卡框挖空区后四边不贴框
- [ ] 与其余两张植物卡并排时构图一致
