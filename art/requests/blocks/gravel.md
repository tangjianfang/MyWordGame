# B-09 沙砾

## 用途

洞穴与水下的散落地层，也是玩家挖掘时的常见障碍。介于 `dirt` 与 `stone` 之间的灰褐色，
必须同时与这两者区分开——沙砾的辨识特征是**明显的小石子颗粒**，颗粒之间明暗对比比
石头强，比泥土碎。

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
| 深阴影 | `#4F4A43` |
| 阴影 | `#6A6258` |
| 主色 | `#857C70` |
| 亮部 | `#9E958A` |
| 高光 | `#B4ABA0` |

色相统一偏暖灰（R > G > B，但差值不超过 20），不要变成纯灰，也不要变成泥土的棕色。

## 视觉描述

密集堆叠的小石子，颗粒直径 2–4 像素，比 `stone` 的颗粒**大一圈且对比更强**。

- 每颗石子是不规则的小色块，边缘硬，不要羽化
- 明暗混杂：亮石子与暗石子随机交错，整体呈现「碎」的观感
- 石子之间不需要画缝隙，靠明度差自然分开
- 不要出现方向性排列（不要看起来像被水冲刷成条纹）
- 不要有任何一颗石子明显大于其它（会在平铺时变成规律标记点）

## AI 提示词

```
A seamless tileable pixel art texture of gravel, top-down flat view, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, completely even
illumination, no global light direction.

Content: densely packed small pebbles of irregular shapes and random sizes, scattered
as ONE continuous field across the entire 1024x1024 canvas with no repeating grid
pattern or tiled preview of a smaller unit. Pebbles are noticeably chunkier and
higher contrast than plain stone grain, giving a coarse crushed-rock look. Light and
dark pebbles are randomly interleaved with no directional alignment and no visible
layering.

Color palette strictly limited to these warm grays: #4F4A43, #6A6258, #857C70,
#9E958A, #B4ABA0. Slightly warm neutral, never pure gray, never brown soil color.

CRITICAL: The texture must tile seamlessly on all four edges.

No border, no frame, no outline, no large focal pebble, no directional streaks,
no text, no watermark, no vignette, no drop shadow, no gradient across the image.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
sand dunes, ripples, directional streaks, one large rock, soil, dirt clumps, grass,
text, watermark, blur, 3D render, perspective, glossy, wet
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 5 个色值（可保留至多 3 个中间色阶）
3. Alpha 全部置为 255
4. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 偏移半幅后画面中央看不出接缝
- [ ] 颜色数 ≤ 10，全部满足 R ≥ G ≥ B 且 R − B ≤ 20
- [ ] 与 `stone.png`、`dirt.png` 三张并排，能分辨出各是哪个
- [ ] 8×8 平铺预览中找不到可用来定位的「标记点」
