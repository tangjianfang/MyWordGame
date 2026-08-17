# M11-C27 收集卡 · 蕨（card-plant-fern）

## 用途

milestone-11 图鉴收集卡：**蕨**的标本插画，垫在三档卡框挖空区内。
统一构图：深色底 + 居中植株标本（占 70%），不透明、无框。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无，完全不透明 |
| 输出数量 | 1 张：`card-plant-fern.png` |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 背景深色 | `#3A342A` |
| 羽叶暗 | `#2F5D24` |
| 羽叶主色 | `#3F7A2E` |
| 羽叶亮 | `#52963B` |
| 标本描边（2px） | `#17140F` |

前三色即全局调色板叶绿（深冷）——蕨必须比草更暗更冷，
与方块 `fern`（A1 花草批次）同一套绿。

## 视觉描述

- 整幅 64×64 平铺深色底 `#3A342A`
- 中央一丛蕨（约占 70%）：三根从底部同点散开的弧形羽叶，
  每根主脉两侧排短羽片，根部深 `#2F5D24`、梢部亮 `#52963B`
- 整株标本包 2 像素黑描边

## AI 提示词

```
A pixel art fern specimen card illustration for a game codex, 1024x1024,
designed to be downscaled to 64x64 pixel art.

Style: chunky pixel art, flat shading, straight-on front view, no perspective,
no gradient, no glow.

Content: a flat dark backdrop fills the whole square. Centered on it stands one
fern plant filling about 70 percent of the canvas: three arching fronds fanning
out from a single base point at the bottom, each frond a curved stem with rows
of small paired leaflets along both sides. The fronds are darkest green #2F5D24
near the base, mid green #3F7A2E through the middle, and brighter green #52963B
toward the tips. The whole fern specimen is wrapped in a two-pixel near-black
outline #17140F. Backdrop: #3A342A.

Color palette strictly limited to: #3A342A, #2F5D24, #3F7A2E, #52963B, #17140F.

No flowers, no spores, no mushrooms, no ground, no soil, no rocks, no text, no
numbers, no card frame, no border around the canvas, no glow, no 3D render,
no watermark.
```

## 负面提示词

```
flowers, spores, mushrooms, ground, soil, rocks, text, numbers, card frame,
canvas border, glow, 3D, watermark
```

## 后处理

1. 最近邻降采样到 64 × 64
2. 量化到 5 个色值
3. 核对标本居中、四边留边一致
4. 存为 PNG-32（不透明）

## 验收标准

- [ ] 尺寸恰为 64 × 64，完全不透明
- [ ] 三根羽叶呈扇形、根部汇聚一点
- [ ] 绿色三阶（暗→主→亮）从根到梢渐次出现（仍为硬边分色，无渐变）
- [ ] 叠进卡框挖空区后四边不贴框
