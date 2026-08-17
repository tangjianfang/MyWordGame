# 石楼梯（stairs-stone）

## 用途

石质楼梯方块的贴图（milestone-11 第 1 波注册 `stairs-stone`，台阶几何由
网格生成，贴图仍是整面）。与 `stone` 同色系但**加工感更强**：更平整的
凿面 + 细凿横纹，并排使用时能分清「原料石」和「切成台阶的石」。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无，完全不透明 |
| 颜色数 | 6 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 凿痕深色 | `#4E4E4E` |
| 石面阴影 | `#5C5C5C` |
| 石面主色 | `#6E6E6E` |
| 石面亮部 | `#8A8A8A` |
| 高光 | `#A3A3A3` |
| 平整反光 | `#B4B4B4` |

前五档即 `stone` 的石灰系，加一档 `#B4B4B4` 专用于凿平后的平整反光条
（台阶踩面比毛石亮）。

## 视觉描述

- 底面是与 `stone` 同款的 1–2 像素噪点颗粒，但**更均匀**（加工过的平整感）
- **2 条水平细凿痕带**（约第 7–8 行、第 23–24 行）：每条是 1 像素
  `#4E4E4E` 的横向断续短线（凿子留下的水平痕），等距分布保证上下平铺
- 每条凿痕带上方 1 行 `#B4B4B4` 平整反光，下方 1 行 `#5C5C5C` 阴影
- 凿痕短线不贯穿整行（留 2–4 像素空隙），且左右边缘处要能接上
  （平铺时断续节奏连续）

## AI 提示词

```
A seamless tileable pixel art texture of chiseled stone stair surface, flat
front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: a dressed stone face — the same speckled gray as plain stone but
smoother and more even, like a cut and chiseled slab. Two horizontal chiseled
bands run across the image at one quarter and three quarters height. Each band
is one row of short dark chisel marks with small 2 to 4 pixel gaps between
them, never one continuous line, with a 1-pixel bright flattened highlight row
just above each band and a 1-pixel shadow row just below. The chisel rhythm
continues across the left and right edges so it lines up when tiled.

Color palette strictly limited to: #4E4E4E, #5C5C5C, #6E6E6E, #8A8A8A,
#A3A3A3, #B4B4B4. Neutral gray, same family as plain stone.

CRITICAL: The texture must tile seamlessly on all four edges. Speckles and
chisel marks continue across the left and right edges; the two bands sit at
equal spacing so the top and bottom edges match.

No border, no frame, no outline, no cracks, no pebbles, no cobblestone, no
bricks, no moss, no text, no watermark, no vignette, no drop shadow, no
gradient.
```

## 负面提示词

```
gradient, vignette, lighting direction, drop shadow, border, frame, outline,
cracks, pebbles, cobblestone, bricks, mortar, moss, text, watermark, blur, 3D
render, perspective, photorealistic, step geometry, 3D stairs
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 量化到上表 6 个色值
3. 检查两条凿痕带等距（第 7–8 行与 23–24 行），偏了手动修正
4. 四边偏移半幅自检接缝
5. Alpha 全部置为 255

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道或全不透明
- [ ] 与 `stone.png` 并排同色系但更平整、有两条水平凿痕带
- [ ] 凿痕是断续短线，不是贯穿实线
- [ ] 颜色数 ≤ 6，偏移半幅看不出接缝
