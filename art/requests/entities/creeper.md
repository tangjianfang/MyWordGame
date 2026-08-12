# E-05 苦力怕

## 用途

敌对生物图标：MobTypeId=5（creeper），MobView 已设 `BaseColor = (0.4, 0.85, 0.4)` 草绿。

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
| 皮绿 | `#6ED66A` |
| 皮暗 | `#3A8A3A` |
| 脚黑 | `#1A1A1A` |
| 眼黑 | `#1A1A1A` |

## AI 提示词

```
A single creeper icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized creeper
head viewed from the front: bright green rectangular head with darker
green shading on the bottom and sides, two long black rectangular eye
slits, and a black frowning mouth showing teeth-like pixels. Square
composition with no background — the creeper fills the frame.

Color palette strictly: #6ED66A, #3A8A3A, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- 32×32 PNG，32 位 RGBA
- 不透明（Alpha 全部 255）
- 颜色数 ≤ 3
