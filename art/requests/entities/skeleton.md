# E-04 骷髅

## 用途

敌对生物图标：MobTypeId=4（skeleton），MobView 已设 `BaseColor = (0.93, 0.93, 0.85)` 白骨。

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
| 骨白 | `#EAEAD8` |
| 骨暗 | `#C8C8B8` |
| 弓褐 | `#634C33` |
| 眼黑 | `#1A1A1A` |

## AI 提示词

```
A single skeleton icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized skeleton
head viewed from the front: off-white bone color with darker gray shading
on the edges, two large black eye sockets, a small triangular nose hole,
and a thin brown bow visible behind the head suggesting the archer class.
Square composition with no background — the skeleton fills the frame.

Color palette strictly: #EAEAD8, #C8C8B8, #634C33, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- 32×32 PNG，32 位 RGBA
- 不透明（Alpha 全部 255）
- 颜色数 ≤ 4
