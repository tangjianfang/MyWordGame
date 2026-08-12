# E-01 猪

## 用途

生物图标：热键栏的生物蛋、MobView 在世界里的占位渲染。MobView.cs 已为
MobTypeId=1（pig）设了 `BaseColor = (0.95, 0.7, 0.7)` 粉色，未来如果
让 MobView 读 PNG 而非纯色，会从这里取。

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
| 皮粉 | `#F2B0B0` |
| 皮暗 | `#C98F68` |
| 鼻土 | `#8E6F4E` |
| 眼黑 | `#1A1A1A` |

## AI 提示词

```
A single pig icon for an inventory slot or world render, 1024x1024 pixel art
designed to be downscaled to 32x32. A small stylized pig face viewed from
the front: round pink head, two small black dot eyes, a darker brown snout
in the center, and two small triangular ears on top. Square composition with
no background — the pig fills the frame.

Color palette strictly: #F2B0B0, #C98F68, #8E6F4E, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- 32×32 PNG，32 位 RGBA
- 不透明（Alpha 全部 255）
- 颜色数 ≤ 4
