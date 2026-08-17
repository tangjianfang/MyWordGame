# E-16 企鹅

## 用途

生物图标：企鹅（penguin），雪原群系被动生物，掉生鱼。热键栏生物蛋、MobView
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
| 背黑 | `#2A2A32` |
| 黑暗 | `#1A1A22` |
| 腹白 | `#F2F2F2` |
| 领橙 | `#E8912E` |
| 喙橙亮 | `#F2B03A` |

## 视觉描述

企鹅的正脸：石板黑圆头，下半脸一片腹白，脸缘一圈橙色颌纹，中央小橙喙，
两颗小黑眼。黑白领橙四段，像穿了一件小礼服。

## AI 提示词

```
A single penguin icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized penguin
face viewed from the front: dark slate-black round head, a white belly
patch on the lower half of the face, an orange band around the face
edges like a chin-strap, a small brighter orange beak in the center, two
small dark eyes. Square composition with no background — the penguin
fills the frame.

Color palette strictly: #2A2A32, #1A1A22, #F2F2F2, #E8912E, #F2B03A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, full body, flippers, ice background, snow scenery
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 橙色颌纹连续不断开，与雪原场景的白色背景对比清晰
