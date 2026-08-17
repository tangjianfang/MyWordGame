# E-15 熊猫

## 用途

生物图标：熊猫（panda），稀有被动生物（竹林群系彩蛋），掉竹叶。热键栏生物蛋、
MobView 占位渲染、图鉴卡共用本图。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无，完全不透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 白 | `#F2F2F2` |
| 白暗 | `#C8C8C8` |
| 黑 | `#2A2A2A` |
| 黑灰 | `#4A4A4A` |
| 瞳 | `#0F0F0F` |

## 视觉描述

熊猫的正脸：白圆头配浅灰阴影，黑色眼罩横带连两眼，头顶两只黑圆耳，
白口鼻上一颗黑鼻，眼罩内各有小黑瞳。全图只有黑白灰三档。

## AI 提示词

```
A single panda icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized panda
face viewed from the front: round white head with light gray shading, a
black band across both eyes like a mask, two black rounded ears on top,
a black nose on a white muzzle, and small darker pupils inside the black
eye patches. Square composition with no background — the panda fills
the frame.

Color palette strictly: #F2F2F2, #C8C8C8, #2A2A2A, #4A4A4A, #0F0F0F only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, bamboo, full body, background scenery, any color other
than black white and gray
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5，且只有黑白灰
- [ ] 黑眼罩在 32×32 下仍是一条连贯横带
