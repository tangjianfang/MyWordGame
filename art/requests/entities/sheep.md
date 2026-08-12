# E-02 羊

## 用途

生物图标：MobTypeId=2（sheep），MobView 已设 `BaseColor = (0.95, 0.95, 0.95)`。

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
| 脸皮 | `#D2A48A` |
| 眼黑 | `#1A1A1A` |

## AI 提示词

```
A single sheep icon for an inventory slot or world render, 1024x1024 pixel
art designed to be downscaled to 32x32. A small stylized sheep face viewed
from the front: round white fluffy head with darker gray shading on the
edges, two small black dot eyes, a small tan-colored face in the center,
and two small floppy ears on the sides. Square composition with no
background — the sheep fills the frame.

Color palette strictly: #F2F2F2, #C8C8C8, #D2A48A, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- 32×32 PNG，32 位 RGBA
- 不透明（Alpha 全部 255）
- 颜色数 ≤ 4
