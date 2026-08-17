# E-17 山羊

## 用途

生物图标：山羊（goat），山地群系被动生物，掉羊奶与山羊皮。热键栏生物蛋、
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
| 灰白 | `#DCD6CC` |
| 灰暗 | `#B0AAA0` |
| 角灰 | `#8A8378` |
| 耳内粉 | `#D8B0A8` |
| 眼黑 | `#1A1A1A` |

## 视觉描述

山羊的正脸：羊毛灰白长脸配暗灰边缘，头顶两只后弯石灰色角，两侧长垂耳
内侧粉白，横向细缝黑眼配小黑口鼻。

## AI 提示词

```
A single goat icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized goat face
viewed from the front: pale wool-gray elongated head with darker gray
shading, two backward-curving horns in stone gray on top, two long
drooping ears at the sides with pale pink inner sides, two black
horizontal-slit eyes and a small dark muzzle. Square composition with
no background — the goat fills the frame.

Color palette strictly: #DCD6CC, #B0AAA0, #8A8378, #D8B0A8, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, huge spiraled horns, full body, mountain background,
rectangular pupils
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 与 sheep 并排能一眼分清（羊无角、圆脸；山羊有角、长脸）
