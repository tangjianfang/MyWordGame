# E-12 兔子

## 用途

生物图标：兔子（rabbit），被动生物，掉兔毛与兔肉。热键栏生物蛋、MobView
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
| 毛褐 | `#C8A888` |
| 毛暗 | `#A08060` |
| 耳内粉 | `#E8B8B0` |
| 鼻白 | `#F2F2F2` |
| 眼黑 | `#1A1A1A` |

## 视觉描述

兔子的正脸：浅褐圆头，两只长竖耳内侧粉白，白口鼻配小黑鼻头，两颗黑点眼，
腮帮蓬松。

## AI 提示词

```
A single rabbit icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized rabbit
face viewed from the front: light tan-brown rounded head, two long
upright ears with pale pink inner sides, a small white muzzle with a
tiny dark nose, two black dot eyes, and fluffy cheeks. Square
composition with no background — the rabbit fills the frame.

Color palette strictly: #C8A888, #A08060, #E8B8B0, #F2F2F2, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, drooping ears, carrot, background scenery
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 两只耳朵等高对称，无一只歪斜
