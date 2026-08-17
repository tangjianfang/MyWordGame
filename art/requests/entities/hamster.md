# E-19 仓鼠

## 用途

生物图标：仓鼠（hamster），家园系统的小宠物，不掉战斗材料。热键栏生物蛋、
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
| 金棕 | `#E8B878` |
| 金暗 | `#C89050` |
| 颊奶白 | `#F2E0C8` |
| 耳内粉 | `#E8A898` |
| 眼黑 | `#1A1A1A` |

## 视觉描述

仓鼠的正脸：金棕胖圆脸配边缘压暗，两腮与口鼻奶白鼓起，两只小圆耳内侧粉白，
两颗亮黑点眼配小粉鼻。圆滚感是重点，脸部要占满画布。

## AI 提示词

```
A single hamster icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized hamster
face viewed from the front: chubby golden-brown round head with darker
shading on the edges, cream-white puffy cheeks and muzzle, two tiny
round ears with pale pink inner sides, two shiny black dot eyes and a
tiny pink nose. Square composition with no background — the hamster
fills the frame.

Color palette strictly: #E8B878, #C89050, #F2E0C8, #E8A898, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, full body, seed, cage, sunflower seed, background scenery
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 腮帮饱满外凸，与 rabbit 的尖脸有明显差异
