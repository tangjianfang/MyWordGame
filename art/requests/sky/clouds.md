# S-02 云层

## 用途

天空中缓慢横向漂移的云层。云是判断「世界在动」的最低成本手段——
只要云在飘，即使玩家站着不动，画面也是活的。

实现方式：一张**黑白遮罩图**平铺在玩家头顶固定高度的一个大平面上，
UV 随时间横向偏移。云的颜色由着色器给（白天纯白、黄昏偏橙、夜里偏灰蓝），
**贴图本身只提供形状**。

## 只需要形状，所以是遮罩图

贴图的每个像素只有两种状态：**有云**（不透明白）和**没云**（透明）。
不要在贴图里画云的明暗、体积、投影——那些由着色器和光照处理。

这条约束让贴图极其简单，也让「换个时间段云自动变色」成为可能。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 128 × 128 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝**（这是硬要求，云会一直在头顶重复） |
| Alpha | 有，仅 0 或 255 |
| 云覆盖率 | **30% – 40%** |
| 颜色数 | 1 色（纯白 `#FFFFFF`）+ 透明 |

覆盖率低于 30% 天空太空旷，高于 40% 会变成阴天顶棚、把太阳挡没。

## 视觉描述

俯视看到的云层遮罩。

- 由 **6–10 团**独立的云构成，每团是**圆角的、由若干重叠圆块拼成的**不规则形状
- 单团云的长度约 25–45 像素、高度约 12–25 像素，**横向明显长于纵向**
  （云被风拉长，也让横向漂移看起来自然）
- 云团之间留出足够的空隙，不要连成一整片
- 云的边缘必须是**硬边阶梯像素**，不要羽化、不要绒毛感
- 不要有任何一团云明显大于其它（平铺时会变成醒目的标记）
- 不要画雨、不要画闪电、不要画云的阴影面

## AI 提示词

```
A seamless tileable black and white cloud mask for a retro voxel game sky, 1024x1024,
designed to be downscaled to 128x128 pixel art.

Style: flat 1-bit stencil mask like a Photoshop layer mask, top-down view, ONLY two
flat colors visible in the whole image: pure white and pure magenta. Absolutely no
pink, no lavender, no purple, no gray, no shading tone of any kind.

Content: 6 to 10 separate rounded cloud blobs scattered across the image. Each cloud
is built from several overlapping rounded lumps, clearly wider than it is tall. The
clouds are flat pure white with crisp stair-stepped pixel edges, no soft feathering,
no drop shadow, no shaded underside. Clouds are well separated and never merge into
one continuous layer. No single cloud is much larger than the others. Clouds cover
roughly 35 percent of the image.

Everything that is not cloud is flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing.

CRITICAL: The mask must tile seamlessly on all four edges. Any cloud touching an edge
must continue exactly on the opposite edge.

No shading, no shadow, no volume, no fluffy illustration style, no gradient, no rain,
no sun, no sky background, no birds, no text, no watermark, no logo, no signature,
no stamp, no icon, no 3D render, no perspective, no cute cartoon style.
```

## 负面提示词

```
shading, shadows, volumetric, fluffy soft edges, feathered edges, gradient, blur,
pink, lavender, purple, gray tone, rain, lightning, storm, sun, moon, sky background,
blue background, birds, airplane, landscape, horizon, text, watermark, logo,
signature, stamp, icon, 3D render, perspective, depth of field, photorealistic,
cute illustration, overcast, one big cloud, transparent background, checkerboard
```

## 后处理

1. 最近邻降采样到 128 × 128
2. **二值化**：洋红占比 > 50% 的像素 Alpha = 0；其余像素设为 `#FFFFFF` 且 Alpha = 255
   （不保留任何灰度——这是遮罩图，只要形状）
3. 统计云覆盖率，须落在 30% – 40%；不够就补一团，超了就删一团
4. **偏移半幅自检**：跨边缘的云必须完整衔接，接不上就手工修补
5. **横向滚动自检**（关键）：把图横向平铺 3 份，逐像素向右滚动播放，
   看是否出现「某团云每隔 128 像素出现一次」的规律感

## 验收标准

- [ ] 尺寸恰为 128 × 128
- [ ] 32 位 PNG，全图只有两种像素：`#FFFFFF` + Alpha 255，或 Alpha 0
- [ ] Alpha 255 的像素占比在 30% – 40%
- [ ] 偏移半幅后无接缝，跨边缘云团完整
- [ ] 横向滚动预览时看不出明显的循环节
- [ ] 没有任何云团的面积超过所有云团平均面积的 2 倍
- [ ] 所有云团都是横向长于纵向
- [ ] 把遮罩涂成纯白叠在天蓝 `#79A6FF` 上，读起来像晴天的云而不是雾
