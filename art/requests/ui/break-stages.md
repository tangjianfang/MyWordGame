# U-03 挖掘裂纹（5 阶）

## 用途

叠加在正在挖掘的方块表面，表示破坏进度。**叠加渲染在方块贴图之上**，
所以除裂纹本身外必须完全透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素（每张） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **需要**，非裂纹区域全透明 |
| 输出数量 | **5 张**：`break-0.png` ～ `break-4.png` |

## 核心要求：递进关系

五张图必须是**同一组裂纹的逐步生长**，不是五组各自独立的裂纹。

具体做法：**先画好第 5 阶（最密集）的完整裂纹图，再逐级删减分支得到前四阶**。
这样播放时裂纹是"长出来"的，而不是每一帧跳变成另一副样子。

| 文件 | 阶段 | 覆盖程度 |
| --- | --- | --- |
| `break-0.png` | 刚开始 | 1 条短裂纹，位于中心附近，长度约 8 像素 |
| `break-1.png` | 早期 | 该裂纹延长，并分出 1 条支线 |
| `break-2.png` | 中期 | 3 – 4 条裂纹，开始向四角延伸 |
| `break-3.png` | 后期 | 6 – 8 条裂纹，覆盖大半个面 |
| `break-4.png` | 即将破碎 | 裂纹密布全图，边缘出现细小碎块 |

## 调色板

| 用途 | HEX | Alpha |
| --- | --- | --- |
| 裂纹主体 | `#000000` | 200 |
| 裂纹边缘 | `#000000` | 90 |
| 其余区域 | — | 0 |

用纯黑半透明而非实色，这样叠加在任何颜色的方块上都自然。

## 视觉描述

不规则的**尖锐折线**裂纹，像玻璃或岩石开裂：

- 线条宽度 1 – 2 像素，**必须是折线，不能是平滑曲线**
- 从中心区域向外发散，分叉角度随机
- 不要画圆形、不要画星形、不要画规则的对称图案
- 不要有阴影、不要有高光、不要有颜色

## AI 提示词

对每一阶单独生成，或先生成第 5 阶再手工删减（推荐后者，更容易保证递进关系）。

```
Pixel art crack overlay texture for a breaking block in a voxel game, 1024x1024,
on a fully transparent background, designed to be downscaled to 32x32 pixel art.

Content: irregular jagged black cracks radiating outward from near the center,
like fractured stone. The cracks are thin sharp angular lines that branch at random
angles. Lines are straight segments meeting at sharp angles, never smooth curves.

Stage: [替换为下列之一]
  - "a single short crack, very sparse, covering only a small part of the center"
  - "one crack extended with a single branch, still sparse"
  - "three or four cracks beginning to reach toward the corners"
  - "six to eight cracks covering most of the surface"
  - "a dense network of cracks covering the entire surface with small chips at the edges"

Style: crisp hard-edged pixel art, no anti-aliasing, no blur, no glow, no shading.
The cracks are pure black. Everything that is not a crack is fully transparent.

No background, no fill, no color, no shadow, no highlight, no circle, no star shape,
no symmetrical pattern, no spider web, no text, no watermark, no block texture
underneath — only the cracks on transparency.
```

## 负面提示词

```
background, opaque background, block texture, stone texture, color, colored cracks,
shadow, highlight, glow, bloom, circle, star, spider web, symmetrical pattern,
smooth curves, rounded lines, text, watermark, signature, blur, anti-aliasing,
3D render, bevel, emboss
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 裂纹主体像素设为 `#000000` / Alpha 200，边缘过渡像素设为 Alpha 90
3. **非裂纹区域 Alpha 严格置 0**
4. **逐张核对递进**：把 5 张叠在一起看，后一阶必须完整包含前一阶的所有裂纹
5. 存为 PNG-32

## 验收标准

- [ ] 5 张尺寸均为 32 × 32
- [ ] **后一阶包含前一阶的全部裂纹**（叠图检查，这是最关键的一条）
- [ ] 非裂纹区域 Alpha 全为 0
- [ ] 裂纹为折线，无平滑曲线
- [ ] 裂纹颜色为纯黑，无任何色相
- [ ] 无圆形、星形等规则对称图案
- [ ] 依次叠加在 `stone.png` 上播放，观感是裂纹逐渐"长大"而非跳变
