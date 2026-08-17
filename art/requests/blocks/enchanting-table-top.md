# 附魔台顶面（enchanting-table-top）

## 用途

附魔台方块（milestone-11 第 2 波注册 `enchanting_table` 方块，numericId=1062）的
**顶面**贴图。与 `enchanting-table-side` 组成同一附魔台的六个面，两张必须共用
同一套深紫黑台面色；顶面独有的是中央菱形宝石与四角符文点。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 四边无缝（贪心网格会合并整片台面，接缝必须对上） |
| Alpha | 无，完全不透明 |
| 颜色数 | ≤ 8 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 台面暗部 | `#1F1430` |
| 台面主色 | `#32224D` |
| 台面亮部 | `#4A3568` |
| 宝石暗边 | `#2C4E8C` |
| 宝石主色 | `#3E6FBF` |
| 宝石高光 | `#7FA8E8` |
| 符文金 | `#DCAE3A` |

## 视觉描述

- 全幅深紫黑噪点底（三阶 1-2 像素级颗粒，密度与 `stone.png` 一致）
- 中央一枚**菱形宝石**（约 11×11 像素钻石轮廓）：主体 `#3E6FBF`、
  外圈 1 像素 `#2C4E8C` 暗边、正中心 1 像素 `#7FA8E8` 高光——读作「青金石台上发光」
- 四角各一枚 2×2 金色符文点 `#DCAE3A`（离边 ≥4 像素，保证四边无缝）
- 除宝石与符文外不做其它装饰，噪点颗粒不跨边

## AI 提示词

```
A pixel art texture of the top face of an enchanting table block, flat top
view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, crisp pixel edges.

Content: a dark purple-black stone surface with subtle 1-2 pixel noise grain
filling the whole square. In the exact center sits one diamond-shaped
sapphire gem about 11x11 final pixels, with a 1-pixel dark blue outline and
a single bright highlight pixel in its middle. Near each of the four corners
is one small 2x2 gold rune dot, each kept at least 4 pixels away from the
canvas edges so the texture tiles seamlessly on all four sides.

Color palette strictly limited to #1F1430, #32224D, #4A3568 for the dark
stone surface, #2C4E8C, #3E6FBF, #7FA8E8 for the gem, and #DCAE3A for the
gold rune dots.

Fill the whole square canvas edge to edge, no background, no magenta.

No text, no watermark, no vignette, no drop shadow, no gradient, no glow
bloom, no books, no candles, no extra decorations.
```

## 负面提示词

```
gradient, vignette, bloom, drop shadow, text, watermark, blur, photorealistic,
3D perspective, books, candles, skulls, animation frames
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 7 色
3. 平铺自检：位移半宽半高，中央不得出现接缝线或色块断裂
4. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 菱形宝石居中、读得出「发光的青金石」
- [ ] 四角符文点不触边（位移自检无缝）
- [ ] ≤ 7 色，无调色板外杂色（尤其不许出现 `#FF00FF`）
- [ ] 与 `enchanting-table-side.png` 并排，深紫黑三阶完全一致
