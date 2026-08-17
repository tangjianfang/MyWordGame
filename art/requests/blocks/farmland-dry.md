# 干耕地（farmland-dry）

## 用途

锄头开垦后的耕地**干燥态**顶面贴图（milestone-11 农业赛道注册 `farmland`
方块）。四条水平垄沟是「这是耕地」的读法来源；未浇水时是干燥的泥土色。
与 `farmland-wet` 共用同一版式，只差湿度色档。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（上下缝对齐垄沟边界） |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 垄沟底（最深） | `#4E3826` |
| 沟壁阴影 | `#5F4630` |
| 垄面主色 | `#7A5A3C` |
| 垄面亮部 | `#876643` |
| 干土高光 | `#91704B` |

即 `dirt` 的泥土棕全套——耕地是从泥土变来的，色系必须同源。

## 视觉描述

- **4 条水平垄**，每条 8 像素高，从上到下占满 32 像素（与 `planks` 同版式）
- 每条垄：上 5 行是垄面 `#7A5A3C`（散布 1 像素噪点 `#876643` / `#91704B`），
  下 2 行是沟壁阴影 `#5F4630`，最底 1 行是沟底 `#4E3826`
- 垄面上有少量 1 像素的干裂纹短线（不贯穿整条垄）
- 顶边第 0 行是第一条垄面的开始、底边第 31 行是沟底结束，上下平铺时
  沟底正好接上一条垄面
- 噪点颗粒 1–2 像素，与 `dirt` 一致

## AI 提示词

```
A seamless tileable pixel art texture of dry tilled farmland seen from directly
above, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: exactly 4 horizontal furrow ridges of equal height stacked from top to
bottom. Each ridge is a light dry soil crest on its upper part, then a shadowed
furrow wall, then a dark furrow bottom line on its lowest row. The top edge of
the image is the start of the first ridge and the bottom edge is the end of the
last furrow, so ridges line up when tiled vertically. A few tiny dry crack
marks on the ridge crests, 1 or 2 pixels, never crossing a whole ridge. Crisp
1-pixel noise speckles like plain dirt.

Color palette strictly limited to these dry soil browns: #4E3826, #5F4630,
#7A5A3C, #876643, #91704B.

CRITICAL: The texture must tile seamlessly on all four edges. Furrow rows align
across the top and bottom edges; speckles continue across the left and right
edges.

No border, no frame, no stones, no seeds, no sprouts, no water, no crops, no
footprints, no text, no watermark, no vignette, no drop shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
stones, pebbles, seeds, sprouts, water, puddles, crops, footprints, text,
watermark, blur, 3D render, perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查垄高恰为 4 × 8 像素，不均则手动修正
4. 上下偏移半幅自检：沟底正好接垄面
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 恰好 4 条垄，每条 8 像素高
- [ ] 与 `dirt.png` 并排，颗粒风格与色系一致
- [ ] 与 `farmland-wet.png` 并排，版式逐行相同、只是更干更浅
- [ ] 上下偏移半幅后垄沟对齐
