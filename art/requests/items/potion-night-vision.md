# 夜视药水

## 用途

热键栏/背包中的夜视药水图标（饮用暗处可视）。瓶型与 `potion-base` 母版一致，**液色换紫**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 玻璃沿 | `#A9CBD6` |
| 玻璃高光 | `#E7F4F8` |
| 木塞 | `#9C7549` |
| 液暗（紫） | `#5C3A8E` |
| 液主（紫） | `#9B6FD4` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `potion-base` 同一正视圆肚细颈瓶剪影：瓶身下 2/3 紫色液体、液面平线、瓶底更深的暗紫
- 玻璃高光月牙与木塞照旧；紫色为魔法/夜晚语义，与附魔系紫（`#9B6FD4`）同族
- 瓶型、玻璃、木塞色逐项照母版，只动液色

## AI 提示词

```
A single night vision potion icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A small round glass potion flask
seen straight from the front: a round bulb body with a short narrow neck
and a small brown cork stopper on top. Bright purple liquid fills the lower
two thirds of the bulb with a flat horizontal surface line, slightly darker
purple at the bottom of the bulb. A pale glass highlight crescent sits on
the left of the bulb, and the glass rim at the neck base is slightly
darker. Black 1-pixel outline around the flask. The empty corners are one
flat solid dark backdrop color #2A2620.

Color palette strictly: #A9CBD6, #E7F4F8, #9C7549, #5C3A8E, #9B6FD4,
#2A2620, #1A1A1A only.

No text, no watermark, no label, no bubbles, no splash, no sparkle, no
eyes, no stars, no moon, no gradient, no cast shadow, no glow, no
anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, label, stickers, bubbles, splash, sparkle, eyeball, stars, moon,
night sky, hands, gradient, drop shadow, glow, blur, 3D render,
anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 7
- 与 `potion-base` 并排瓶型一致，液体为紫色系
