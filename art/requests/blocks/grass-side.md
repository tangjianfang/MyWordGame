# B-04 草方块侧面

## 用途

草方块的四个侧面。玩家平视地平线时看到的就是它，与 `grass-top` 和 `dirt` 直接相邻，
三者的衔接是否自然决定了画面是否"专业"。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **左右无缝**（上下不要求，见下） |
| Alpha | 无，完全不透明 |
| 颜色数 | 10 – 16 色 |

### 关于上下平铺

本项目的地形生成不会产生"竖直堆叠的草方块"（草只生成在地表最上层），
因此侧面贴图**只需左右无缝**，不必考虑上下衔接。

## 构图（严格遵守）

图像自上而下分为三段：

| 区域 | 像素行 | 内容 |
| --- | --- | --- |
| 草带 | 第 0 – 5 行 | 纯草绿，对应顶面的草色 |
| 过渡带 | 第 6 – 11 行 | 草与土的犬牙交错边界 |
| 土壤 | 第 12 – 31 行 | 与 `dirt.png` 一致的泥土纹理 |

**过渡带是本图的关键**：草不能是一条笔直的水平线切下来，必须是**参差不齐的锯齿状垂落**，
有的地方草垂得深（到第 11 行），有的地方浅（到第 7 行），随机分布。
笔直的分界线会让方块看起来极其廉价。

## 调色板（严格使用）

草带部分：

| 用途 | HEX |
| --- | --- |
| 草阴影 | `#4A7E2F` |
| 草主色 | `#5D9C3C` |
| 草高光 | `#74B84E` |

土壤部分：

| 用途 | HEX |
| --- | --- |
| 土深阴影 | `#4E3826` |
| 土阴影 | `#5F4630` |
| 土主色 | `#7A5A3C` |
| 土高光 | `#91704B` |

## AI 提示词

```
A seamless horizontally-tileable pixel art texture of a grass block side view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat front-facing orthographic view,
flat shading, even illumination, no lighting direction, no shadows.

Composition, strictly from top to bottom:
- The top 19% of the image is solid green grass.
- Below it, a jagged uneven transition where grass hangs down into the soil in
  irregular tongues of varying depth, never a straight horizontal line.
- The remaining bottom 62% is coarse brown dirt soil with chunky grains.

The grass green tones are #4A7E2F, #5D9C3C, #74B84E.
The dirt brown tones are #4E3826, #5F4630, #7A5A3C, #91704B.
Warm natural colors, no neon, no gray tint.

CRITICAL: The texture must tile seamlessly on the LEFT and RIGHT edges, so that
many blocks placed side by side form a continuous grass line with no visible seam.

No border, no frame, no outline, no flowers, no roots, no stones, no text,
no watermark, no vignette, no gradient, no perspective, no 3D shading.
```

## 负面提示词

```
straight horizontal line between grass and dirt, gradient, vignette, lighting
direction, drop shadow, border, frame, outline, flowers, roots, stones, pebbles,
text, watermark, signature, blur, 3D render, perspective, isometric, glossy,
grass on the bottom, dirt on top
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 7 个色值（可保留至多 3 个中间色阶）
3. **核对分界**：确认草带占据顶部约 6 行，且分界为锯齿状而非直线
4. Alpha 全部置为 255
5. 只需左右偏移自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 左右偏移后看不出竖直接缝
- [ ] 顶部 6 行为纯草绿，色值与 `grass-top.png` 一致
- [ ] 草土分界为**参差锯齿**，绝非水平直线
- [ ] 底部 20 行的纹理风格与 `dirt.png` 一致（颗粒粗细相当）
- [ ] 与 `grass-top.png`、`dirt.png` 并排摆放时三者色调连贯
- [ ] 无任何像素的 Alpha < 255
