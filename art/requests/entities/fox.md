# E-13 狐狸

## 用途

生物图标：狐狸（fox），被动生物，掉狐皮。热键栏生物蛋、MobView 占位渲染、
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
| 橙主 | `#E07B28` |
| 橙暗 | `#B85A18` |
| 颊白 | `#F2E8DC` |
| 耳尖黑 | `#2A2A2A` |
| 眼黑 | `#1A1A1A` |

## 视觉描述

狐狸的正脸：亮橙头部、边缘压橙暗，两颊与口鼻奶白，两只尖耳带黑耳尖，
黑点眼配小黑鼻。橙白黑三段配色一眼可辨。

## AI 提示词

```
A single fox icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized fox face
viewed from the front: bright orange head with darker orange shading on
the edges, white cheeks and muzzle, two pointed ears with dark tips on
top, two black dot eyes and a small dark nose. Square composition with
no background — the fox fills the frame.

Color palette strictly: #E07B28, #B85A18, #F2E8DC, #2A2A2A, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, snout in profile, full body, background scenery, snow
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 橙主色与 fox 掉落物「狐皮」图标的底色一致
