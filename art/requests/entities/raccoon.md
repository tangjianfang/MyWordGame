# E-18 浣熊

## 用途

生物图标：浣熊（raccoon），夜行被动生物，掉浣熊皮。热键栏生物蛋、MobView
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
| 灰 | `#8A8580` |
| 灰暗 | `#5A5550` |
| 眼罩黑 | `#262626` |
| 颊白 | `#E8E4DC` |
| 鼻黑 | `#1A1A1A` |

## 视觉描述

浣熊的正脸：灰圆头配暗灰边缘，黑色眼罩横带连两眼，两颊与眉间白斑，
两只小尖灰耳带黑耳尖，白口鼻上一颗黑鼻。标志性「蒙面」要一眼可辨。

## AI 提示词

```
A single raccoon icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized raccoon
face viewed from the front: gray rounded head with darker gray shading,
a black band across both eyes like a mask, white cheeks and white brow
spots, two small pointed gray ears with dark tips, a dark nose on a
pale muzzle. Square composition with no background — the raccoon fills
the frame.

Color palette strictly: #8A8580, #5A5550, #262626, #E8E4DC, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, full body, striped tail, trash can, night background
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 黑眼罩与 panda 的眼罩并排时不混淆（浣熊灰底、熊猫白底）
