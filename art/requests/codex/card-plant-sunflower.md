# M11-C26 收集卡 · 向日葵（card-plant-sunflower）

## 用途

milestone-11 图鉴收集卡：**向日葵**的标本插画，垫在三档卡框挖空区内。
统一构图：深色底 + 居中植株标本（占 70%），不透明、无框。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无，完全不透明 |
| 输出数量 | 1 张：`card-plant-sunflower.png` |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 背景深色 | `#3A342A` |
| 茎叶绿 | `#5D9C3C` |
| 花瓣亮 | `#F7DA7A` |
| 花瓣主色 | `#DCAE3A` |
| 花盘棕 | `#634C33` |
| 标本描边（2px） | `#17140F` |

## 视觉描述

- 整幅 64×64 平铺深色底 `#3A342A`
- 中央一株向日葵（约占 70%）：绿色茎 + 两片对生叶，
  顶部花盘：一圈双层花瓣（外圈 `#DCAE3A`、内圈亮 `#F7DA7A`）围棕色花盘
- 整株标本包 2 像素黑描边

## AI 提示词

```
A pixel art sunflower specimen card illustration for a game codex, 1024x1024,
designed to be downscaled to 64x64 pixel art.

Style: chunky pixel art, flat shading, straight-on front view, no perspective,
no gradient, no glow.

Content: a flat dark backdrop fills the whole square. Centered on it stands one
sunflower filling about 70 percent of the canvas: a green stem #5D9C3C with two
opposite leaves, topped by one round flower head made of a brown seed disc
#634C33 surrounded by two rings of petals, the outer ring golden #DCAE3A and the
inner ring brighter #F7DA7A. The whole plant specimen is wrapped in a two-pixel
near-black outline #17140F. Backdrop: #3A342A.

Color palette strictly limited to: #3A342A, #5D9C3C, #F7DA7A, #DCAE3A, #634C33,
#17140F.

No sun, no sky, no rays of light, no ground, no soil, no second flower, no
text, no numbers, no card frame, no border around the canvas, no glow, no 3D
render, no watermark.
```

## 负面提示词

```
sun, sky, light rays, ground, soil, second flower, text, numbers, card frame,
canvas border, glow, 3D, watermark
```

## 后处理

1. 最近邻降采样到 64 × 64
2. 量化到 6 个色值
3. 核对标本居中、四边留边一致
4. 存为 PNG-32（不透明）

## 验收标准

- [ ] 尺寸恰为 64 × 64，完全不透明
- [ ] 花盘双层花瓣可辨、茎叶分明
- [ ] 叠进卡框挖空区后四边不贴框
- [ ] 与 `card-plant-cherry/fern` 并排时构图一致
