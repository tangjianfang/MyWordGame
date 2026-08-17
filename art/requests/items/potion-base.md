# 基底药水（药水系列母版）

## 用途

热键栏/背包中的基底药水（清水瓶）图标。**本文件是全部彩色药水的瓶型母版**：
六个彩色药水（healing/speed/strength/jump/night-vision/water-breathing）
与本品共用同一瓶型与玻璃/木塞色，**只换瓶内液体色**。

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
| 清水液 | `#C8E2EA` |
| 木塞 | `#9C7549` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视圆肚细颈瓶：圆球瓶身 + 短颈 + 顶部木塞；液体占瓶身下 2/3，液面一条平线
- 瓶身左侧一道玻璃高光月牙，颈根一圈略深的玻璃沿色
- 玻璃/木塞色固定不变；本品液体为近无色的清水（`#C8E2EA`），各彩色药水的液色对照表：

| 药水 | 液色系 | 液暗 / 液主 |
| --- | --- | --- |
| potion-healing 治疗 | 红 | `#8E2430` / `#D63838` |
| potion-speed 迅捷 | 蓝 | `#2C5893` / `#4E88CE` |
| potion-strength 力量 | 橙 | `#B23E08` / `#F79B22` |
| potion-jump 跳跃 | 金 | `#A8842E` / `#F7E08A` |
| potion-night-vision 夜视 | 紫 | `#5C3A8E` / `#9B6FD4` |
| potion-water-breathing 水肺 | 青 | `#1E7C7C` / `#4CC6C4` |
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single plain potion bottle icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A small round glass potion flask
seen straight from the front: a round bulb body with a short narrow neck
and a small brown cork stopper on top. Pale clear water fills the lower two
thirds of the bulb with a flat horizontal surface line, slightly denser
pale cyan at the bottom of the bulb. A pale glass highlight crescent sits
on the left of the bulb, and the glass rim at the neck base is slightly
darker. Black 1-pixel outline around the flask. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #A9CBD6, #E7F4F8, #C8E2EA, #9C7549,
#1A1A1A outline only.

No text, no watermark, no label, no bubbles, no splash, no sparkle, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, label, stickers, bubbles, splash, liquid spilling, sparkle, potion
ring, hands, gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 圆肚/细颈/木塞三段分明，液面平线可见；六彩色药水与本品并排**瓶型逐像素同剪影**、只有液体颜色不同
