# 机元靴子

## 用途

热键栏/背包/盔甲栏中的机元靴子图标。机元系盔甲第四件（护足）。

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
| 机元暗 | `#5E4478` |
| 机元主 | `#7A5A9A` |
| 机元亮 | `#9C82BC` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `boots-iron` 同一侧视单靴剪影（鞋头朝右），材质换 m10 机元紫灰系
- 紫灰与机元剑/镐同源（`#5E4478`/`#7A5A9A`/`#9C82BC`）
- 鞋底为暗紫阶，踝筒前面一道亮紫高光
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single machine essence boots icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A single heavy armored boot seen
from the side, toe pointing right: an L-shaped purple-gray metal boot with
a tall ankle shaft, a flat slightly darker purple sole plate under the toe,
and one pale violet highlight streak on the front of the shaft. The boot
fills the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #5E4478, #7A5A9A, #9C82BC, #1A1A1A outline only.

No text, no watermark, no pair of boots, no foot inside, no laces, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, two boots, pair, foot, toes, laces, sock, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 与 `boots-iron` 并排剪影一致，颜色为紫灰系
