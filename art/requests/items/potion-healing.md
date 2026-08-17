# 治疗药水

## 用途

热键栏/背包中的治疗药水图标（饮用回血）。瓶型与 `potion-base` 母版一致，**液色换红**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 背景 | 整片纯洋红 `#FF00FF`，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 玻璃沿 | `#A9CBD6` |
| 玻璃高光 | `#E7F4F8` |
| 木塞 | `#9C7549` |
| 液暗（红） | `#8E2430` |
| 液主（红） | `#D63838` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `potion-base` 同一正视圆肚细颈瓶剪影：瓶身下 2/3 红色液体、液面平线、瓶底更深的暗红
- 玻璃高光月牙与木塞照旧；红色 = 生命/治疗语义，与心形 UI 的红同情绪
- 瓶型、玻璃、木塞色逐项照母版，只动液色
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single healing potion icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A small round glass potion flask seen
straight from the front: a round bulb body with a short narrow neck and a
small brown cork stopper on top. Bright red liquid fills the lower two
thirds of the bulb with a flat horizontal surface line, slightly darker red
at the bottom of the bulb. A pale glass highlight crescent sits on the left
of the bulb, and the glass rim at the neck base is slightly darker. Black
1-pixel outline around the flask. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #A9CBD6, #E7F4F8, #9C7549, #8E2430, #D63838, #1A1A1A outline only.

No text, no watermark, no label, no bubbles, no splash, no sparkle, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, label, stickers, bubbles, splash, sparkle, cross symbol, heart,
hands, gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 与 `potion-base` 并排瓶型一致，液体为红色系
