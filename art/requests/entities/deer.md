# E-14 鹿

## 用途

生物图标：鹿（deer），被动生物，掉鹿皮与鹿肉。热键栏生物蛋、MobView
占位渲染、图鉴卡共用本图。

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
| 毛棕 | `#A5754A` |
| 毛暗 | `#7A5433` |
| 鼻腹白 | `#F2E8D8` |
| 角米 | `#C8A878` |
| 眼黑 | `#1A1A1A` |

## 视觉描述

鹿的正脸：暖棕头部配暗棕边缘，奶白口鼻，头顶一对小米色分叉角，
两颗大黑点眼配小黑鼻。

## AI 提示词

```
A single deer icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized deer face
viewed from the front: warm brown head with darker shading, pale cream
muzzle, two small branching antlers in light tan on top, two large black
dot eyes and a dark nose. Square composition with no background — the
deer fills the frame.

Color palette strictly: #A5754A, #7A5433, #F2E8D8, #C8A878, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, huge antlers, full body, forest background, snow
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 角只占头顶窄条，不挤掉眼睛的位置
