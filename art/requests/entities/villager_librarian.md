# E-07 村民-图书管理员

## 用途

友好生物图标：VillagerProfession.Librarian，VillagerView 已设紫色头巾
`(0.45, 0.35, 0.5)`。

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
| 头巾紫 | `#5A4A8A` |
| 皮肤 | `#D2A48A` |
| 衣紫 | `#3A2A5A` |
| 鼻大 | `#A87322` |
| 眼黑 | `#1A1A1A` |

## AI 提示词

```
A single librarian villager icon for an inventory slot or world render,
1024x1024 pixel art designed to be downscaled to 32x32. A small stylized
villager head viewed from the front: a large round brown nose dominating
the face, a purple wide-brimmed hat with a small feather on top, two small
black dot eyes above the nose, and a hint of darker purple robe at the
bottom. A small book or eyeglasses detail can be added near the face.
Square composition with no background — the villager fills the frame.

Color palette strictly: #5A4A8A, #D2A48A, #3A2A5A, #A87322, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- 32×32 PNG，32 位 RGBA
- 不透明（Alpha 全部 255）
- 颜色数 ≤ 5
