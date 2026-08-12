# E-08 村民-铁匠

## 用途

友好生物图标：VillagerProfession.Blacksmith，VillagerView 已设灰黑头巾
`(0.3, 0.3, 0.35)`。

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
| 头巾黑 | `#4A4A52` |
| 皮肤 | `#D2A48A` |
| 衣灰 | `#3A3A3A` |
| 鼻大 | `#A87322` |
| 眼黑 | `#1A1A1A` |

## AI 提示词

```
A single blacksmith villager icon for an inventory slot or world render,
1024x1024 pixel art designed to be downscaled to 32x32. A small stylized
villager head viewed from the front: a large round brown nose dominating
the face, a black wide-brimmed hat with a small metal pin on top, two small
black dot eyes above the nose, and a hint of dark gray apron at the bottom.
Square composition with no background — the villager fills the frame.

Color palette strictly: #4A4A52, #D2A48A, #3A3A3A, #A87322, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- 32×32 PNG，32 位 RGBA
- 不透明（Alpha 全部 255）
- 颜色数 ≤ 5
