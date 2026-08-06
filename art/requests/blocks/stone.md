# B-01 石头

## 用途

地表以下的主体填充方块，玩家在洞穴与矿洞中大面积看到它。出现频率最高，
因此**必须耐看、不能有醒目的特征点**——任何显眼的斑块在大面积重复时都会变成刺眼的规律图案。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 6 – 10 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 深阴影 | `#5C5C5C` |
| 阴影 | `#6E6E6E` |
| 主色 | `#8A8A8A` |
| 亮部 | `#9B9B9B` |
| 高光 | `#A3A3A3` |

允许在上述色值之间插入至多 2 个过渡灰阶，但不得引入任何带色相的颜色（不要偏蓝、偏棕）。

## 视觉描述

均匀的灰色岩石表面，由**大小不一的不规则颗粒**构成，颗粒直径 1–3 像素。
整体明度分布要平均，不能出现某一角明显更亮或更暗。

具体要求：

- 颗粒随机散布，**不能形成可辨认的形状**（不要有看起来像脸、箭头、字母的图案）
- 明暗对比要弱：最深与最亮之间的视觉跨度控制在中等，避免产生"噪点雪花屏"的观感
- 不要画裂缝、不要画矿脉、不要画方向性的纹理（比如平行斜线）
- 不要有边框、不要有描边

## AI 提示词

```
A seamless tileable pixel art texture of plain gray stone, top-down flat view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, no lighting
direction, completely even illumination across the entire image.

Content: uniform rough stone surface made of small irregular speckles and grain.
Grain particles are tiny and evenly scattered. The overall value is mid-gray with
subtle darker and lighter speckles. Low contrast, calm and non-distracting.

Color palette strictly limited to these grays: #5C5C5C, #6E6E6E, #8A8A8A, #9B9B9B,
#A3A3A3. No blue tint, no brown tint, purely neutral gray.

CRITICAL: The texture must tile seamlessly on all four edges. The left edge must
continue perfectly into the right edge, and the top edge into the bottom edge.

No border, no frame, no outline, no cracks, no ore veins, no directional streaks,
no recognizable shapes or patterns, no text, no watermark, no vignette,
no drop shadow, no gradient across the image.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
cracks, ore, gems, crystals, moss, text, watermark, signature, blur, depth of field,
3D render, perspective, glossy, metallic, recognizable shapes, faces, symbols
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值（可保留至多 2 个中间灰阶）
3. 清除所有半透明像素，Alpha 全部置为 255
4. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后画面中央看不出接缝
- [ ] 颜色数 ≤ 10，且全部为中性灰（R、G、B 三通道差值 ≤ 4）
- [ ] 无任何像素的 Alpha < 255
- [ ] 在 8×8 平铺预览中看不出规律性重复图案
- [ ] 盯着看 10 秒不会觉得某个点特别跳眼
