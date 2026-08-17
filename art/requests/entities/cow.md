# E-09 牛

## 用途

生物图标：牛（cow），被动生物，掉落牛肉与皮革。热键栏生物蛋、MobView 占位渲染、
图鉴卡共用本图。

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
| 毛白 | `#F2F2F2` |
| 毛暗 | `#C8C8C8` |
| 棕斑 | `#6B4A33` |
| 鼻粉 | `#E8A8A8` |
| 角米 | `#D8CBB0` |
| 眼黑 | `#1A1A1A` |

## 视觉描述

牛的正脸：白底头部，眼周两大片深棕斑，鼻镜粉色带两个鼻孔点，头顶两只小弯角，
两颗黑点眼。构图照 sheep.md——正脸居中撑满画布。

## AI 提示词

```
A single cow icon for an inventory slot or world render, 1024x1024 pixel
art designed to be downscaled to 32x32. A small stylized cow face viewed
from the front: white head with large dark brown patches around the eyes,
a broad pink muzzle at the bottom with two small nostril dots, two tiny
pale horns on top, and two black dot eyes. Square composition with no
background — the cow fills the frame.

Color palette strictly: #F2F2F2, #C8C8C8, #6B4A33, #E8A8A8, #D8CBB0,
#1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, horns too large, extra limbs, background scenery
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 6
- [ ] 与 pig.md / sheep.md 并排，画风与脸部比例一致
