# B-15 砖块

## 用途

进阶建材，用来和木板拉开风格差距。砖墙是大面积贴图，且**砖缝的错位排列本身就是图案**，
对平铺对齐的要求比木板更高。

## 平铺的特殊性

砖块采用标准的**错缝砌法**（running bond）：偶数行的砖从左边缘开始，
奇数行的砖向右偏移半块。要让贴图上下左右都能对上，必须满足：

- 一张 32×32 里恰好放 **4 行砖**，每行 8 像素高
- 每行恰好 **2 块整砖**（每块 16 像素宽），奇数行偏移 8 像素，
  于是奇数行在左右边缘各露出**半块砖**，正好与相邻贴图的另外半块拼成整块

这套数字（32 / 4 行 / 8 高 / 16 宽 / 偏移 8）是唯一能在 32×32 上闭合的方案，**不要改**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**，砖缝必须闭合 |
| Alpha | 无，完全不透明 |
| 颜色数 | 5 – 8 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 砖缝（灰浆） | `#9A9086` |
| 砖体深色 | `#6B3226` |
| 砖体主色 | `#8B4433` |
| 砖体亮部 | `#A05242` |
| 砖体高光 | `#B3634C` |

灰浆比砖体**亮**（真实砖墙就是这样），不要画成深色缝。
砖体是暖红棕，不要偏橙、不要偏紫。

## 视觉描述

- 4 行砖，每行 8 像素高；砖缝（灰浆）占 1 像素，砖体占 7 像素
- 第 0、2 行：两块整砖，砖缝在第 16 列
- 第 1、3 行：偏移 8 像素，砖缝在第 8 列和第 24 列，左右边缘各是半块砖
- 每块砖内部有**轻微的明暗差异**（不同砖用略不同的红），让墙面不呆板；
  但差异要小，不能出现某块砖特别显眼
- 砖体内部可以有 1–2 个 1 像素的暗点当作气孔，不要多
- 不要画苔藓、不要画裂砖、不要给砖描黑边

## AI 提示词

```
A seamless tileable pixel art texture of a red brick wall, flat front view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: exactly 4 horizontal rows of bricks in a running bond pattern. Each row is
one quarter of the image height. Rows 1 and 3 contain two full bricks with a vertical
mortar joint at the horizontal center. Rows 2 and 4 are offset by half a brick, so a
half brick appears at the left edge and another half brick at the right edge. Mortar
joints are thin and LIGHTER than the bricks, a pale warm gray. Individual bricks vary
very slightly in red tone so the wall does not look mechanical.

Color palette strictly limited to: #9A9086 (mortar), #6B3226, #8B4433, #A05242,
#B3634C (brick). Warm red brown, no orange tint, no purple tint.

CRITICAL: The texture must tile seamlessly on all four edges. The half bricks at the
left and right edges of the offset rows must combine into one full brick when tiled.
Mortar joints must align across the top and bottom edges.

No border, no frame, no black outline, no moss, no cracked bricks, no ivy, no dirt
stains, no text, no watermark, no vignette, no drop shadow, no gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, black outline,
moss, ivy, cracks, broken bricks, weathering, dirt stains, graffiti, dark mortar,
stack bond, herringbone, text, watermark, blur, 3D render, perspective,
photorealistic, glossy
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值
3. **对齐修正**（必做）：降采样后砖缝很可能不在精确的第 8/16/24 列或第 8/16/24 行，
   手动把砖缝挪到精确位置。这一步不能省，差 1 像素平铺就会露馅
4. 上下左右各偏移半幅自检，砖缝必须闭合成完整的错缝网格
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 恰好 4 行，每行 8 像素；砖缝在第 8、16、24 行的位置
- [ ] 偶数行竖缝在第 16 列；奇数行竖缝在第 8、24 列
- [ ] 4×4 平铺预览呈现连续的错缝砖墙，找不到贴图的边界
- [ ] 灰浆明度**高于**所有砖体色
- [ ] 颜色数 ≤ 8
- [ ] 没有任何一块砖比其它砖明显醒目
