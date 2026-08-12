# E-03 僵尸

## 用途

敌对生物图标：MobTypeId=3（zombie），MobView 已设 `BaseColor = (0.4, 0.7, 0.3)` 草绿。

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
| 皮绿 | `#6E9A4A` |
| 皮暗 | `#3A5A2A` |
| 衣紫 | `#5A4A6A` |
| 眼白 | `#F2F2F2` |
| 眼瞳 | `#1A1A1A` |

## AI 提示词

```
A single zombie icon for an inventory slot or world render, 1024x1024 pixel
art designed to be downscaled to 32x32. A small stylized zombie head viewed
from the front: greenish-gray skin with darker shading on the edges, a few
darker exposed-bone spots, two white eyes with small black pupils, and
ragged purple clothing visible at the bottom. Square composition with no
background — the zombie fills the frame.

Color palette strictly: #6E9A4A, #3A5A2A, #5A4A6A, #F2F2F2, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- 32×32 PNG，32 位 RGBA
- 不透明（Alpha 全部 255）
- 颜色数 ≤ 5
