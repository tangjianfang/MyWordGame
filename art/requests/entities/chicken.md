# E-10 鸡

## 用途

生物图标：鸡（chicken），被动生物，掉落生鸡肉与羽毛。热键栏生物蛋、MobView
占位渲染、图鉴卡共用本图。

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
| 羽白 | `#F2F2F2` |
| 羽暗 | `#C8C8C8` |
| 喙黄 | `#E8B830` |
| 肉垂红 | `#C43A2E` |
| 眼黑 | `#1A1A1A` |

## 视觉描述

鸡的正脸：蓬松白头配浅灰阴影，中央小黄喙，头顶红冠、喙下垂红肉垂，两颗黑点眼。

## AI 提示词

```
A single chicken icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized chicken
face viewed from the front: fluffy white head with light gray shading, a
small yellow beak in the center, a red comb on top of the head and a red
wattle under the beak, two small black dot eyes. Square composition with
no background — the chicken fills the frame.

Color palette strictly: #F2F2F2, #C8C8C8, #E8B830, #C43A2E, #1A1A1A only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, full body, wings, background scenery
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 5
- [ ] 红冠与肉垂的红和 heart 的红同档饱和度，不刺眼
