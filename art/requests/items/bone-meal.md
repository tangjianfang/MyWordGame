# 骨粉

## 用途

热键栏/背包中的骨粉物品图标（骨骼磨成的白色粉末，催熟作物用）。

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
| 粉亮 | `#F2F2F2` |
| 粉主 | `#C8C8C8` |
| 粉暗 | `#8A8A8A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视一小丘白色骨粉：圆润丘形，右侧略深灰作体积，几粒 1 像素灰色小点做颗粒感
- 白色与 `bone.md` 骨骼同 `#F2F2F2` 系（同源材料）
- 丘底一条平边，占画面下半

## AI 提示词

```
A single bone meal icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A small mound of fine white bone meal powder seen
straight from the front: a smooth rounded pile of pure white with light gray
shading on the right side and a flat base, plus a few tiny darker gray
speckles for grain texture. The mound fills the lower half of the frame.
Black 1-pixel outline around the mound. The empty area above is one flat
solid dark backdrop color #2A2620.

Color palette strictly: #F2F2F2, #C8C8C8, #8A8A8A, #2A2620, #1A1A1A only.

No text, no watermark, no bone, no sack, no pouch, no dust cloud, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, bone, skeleton, sack, pouch, dust cloud, smoke, sparkle,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 白色丘形轮廓完整，颗粒感不糊成一片
