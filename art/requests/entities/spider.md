# E-11 蜘蛛

## 用途

生物图标：蜘蛛（spider），敌对生物，夜间出没、掉线。热键栏生物蛋、MobView
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
| 体棕 | `#4A362A` |
| 体暗 | `#2A1E16` |
| 毛亮 | `#6A5648` |
| 眼红 | `#C42222` |

## 视觉描述

蜘蛛的正脸：深棕圆头配更暗的边缘，眼上方一条浅棕绒毛带，中央横排四只小红眼
（两大两小），底部两颗短小螯牙。敌对感全靠红眼，不需要獠牙滴液等血腥元素。

## AI 提示词

```
A single spider icon for an inventory slot or world render, 1024x1024
pixel art designed to be downscaled to 32x32. A small stylized spider
face viewed from the front: round dark brown furry head with darker
shading on the edges, a lighter brown fur band above the eyes, a row of
four small bright red eyes in the center (two larger, two smaller), and
two short fangs at the bottom. Square composition with no background —
the spider fills the frame.

Color palette strictly: #4A362A, #2A1E16, #6A5648, #C42222 only.

No text, no shadow, no glow. Hard pixel edges, no anti-aliasing, no
legs, no web.
```

## 负面提示词

```
text, watermark, shadow, glow, blur, 3D render, perspective, gradient,
photorealism, spider legs, spider web, venom drops, blood, background
scenery, realistic spider
```

## 后处理

降采样到 32×32 + 调色板量化。

## 验收

- [ ] 32×32 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 4
- [ ] 四只红眼在 32×32 下仍可辨认为「一排红点」
