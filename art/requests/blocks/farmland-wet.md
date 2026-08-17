# 湿耕地（farmland-wet）

## 用途

浇水/雨后的耕地**湿润态**顶面贴图。与 `farmland-dry` 同一版式（4 条水平垄、
行号逐行相同），整体压暗两档读作「吸饱水的土」。两种湿度态交替出现，
版式必须严格一致，否则浇水瞬间画面会跳。

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
| 沟底（最深） | `#2E2014` |
| 沟壁阴影 | `#3E2E1E` |
| 垄面主色 | `#4E3826` |
| 垄面亮部 | `#5F4630` |
| 湿土反光 | `#6B4E34` |

每一档都比 `farmland-dry` 对应档深一档左右，湿土反光只用在垄脊零星 1 像素点，
不要画成水洼。

## 视觉描述

- 版式与 `farmland-dry` **完全一致**：4 条 8 像素高的水平垄，每条上 5 行垄面、
  下 2 行沟壁、底 1 行沟底
- 垄面 `#4E3826` 为主，噪点 `#5F4630`，垄脊零星 1 像素 `#6B4E34` 湿反光
- 无干裂纹（湿土不裂），颗粒仍保持 1–2 像素与 `dirt` 一致
- 不画水面、不画反光带——湿润靠整体压暗表达，不是靠高光

## AI 提示词

```
A seamless tileable pixel art texture of wet moist tilled farmland seen from
directly above, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: exactly 4 horizontal furrow ridges of equal height stacked from top to
bottom. Each ridge is a dark moist soil crest on its upper part, then a darker
furrow wall, then the darkest furrow bottom line on its lowest row. The top
edge of the image is the start of the first ridge and the bottom edge is the
end of the last furrow, so ridges line up when tiled vertically. Only a few
single-pixel dim sheen dots on ridge crests; no cracks, no puddles, no standing
water. Crisp 1-pixel noise speckles like plain dirt.

Color palette strictly limited to these dark moist soil browns: #2E2014,
#3E2E1E, #4E3826, #5F4630, #6B4E34. Much darker than dry soil, but still brown,
not black, not gray.

CRITICAL: The texture must tile seamlessly on all four edges. Furrow rows align
across the top and bottom edges; speckles continue across the left and right
edges.

No border, no frame, no stones, no seeds, no sprouts, no water, no puddles, no
reflection, no crops, no text, no watermark, no vignette, no drop shadow, no
gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
stones, pebbles, seeds, sprouts, water, puddles, reflection, crops, cracks,
text, watermark, blur, 3D render, perspective, photorealistic
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. 检查垄高恰为 4 × 8 像素
4. 与 `farmland-dry` 逐行叠图比对版式一致（仅色值不同）
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 恰好 4 条垄，每条 8 像素高，版式与 `farmland-dry` 逐行相同
- [ ] 整体明显比 `farmland-dry` 暗，但仍读作棕色土（不发黑不发灰）
- [ ] 无水洼、无反光带、无干裂纹
- [ ] 上下偏移半幅后垄沟对齐
