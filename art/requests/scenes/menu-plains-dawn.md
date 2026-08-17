# SCENE-01 主菜单背景 · 平原黎明

## 用途

主菜单三选一背景之一：**Plains 群系黎明**。整幅 1024 大图直用作主菜单背景，
上叠引擎侧像素字体标题与按钮。底边六分之一保持简洁偏暗，给菜单按钮留对比度。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无 |

## 调色板

沿用游戏现有 Plains 群系配色（biomes.json）+ 全局调色板：

| 用途 | HEX |
| --- | --- |
| 草地绿 | `#4A7E2F` `#5D9C3C` `#74B84E` |
| 河水蓝 | `#3A6FB5` `#4E88CE` |
| 太阳 | `#FFD84A` `#F2A81E` |
| 天空色带（下→上） | `#F2B8C6` `#FFF3C4` `#79A6FF` |
| 云 | `#E7F4F8` |

## 视觉描述

- 平缓起伏的方块草原，几棵圆滚滚的体素橡树，一条小河蜿蜒到地平线并映着天色
- 远处两只方块小羊在吃草
- 低垂的黎明太阳是**方形淡金圆盘**（贴近日平线，不发光晕）
- 天空画成**横向平涂色带**：下暖粉 → 米白 → 上淡蓝，带与带之间抖动过渡，不做平滑渐变
- 几朵扁平方块云；底边 1/6 是略暗的纯草坡，留给按钮

## AI 提示词

```
Voxel pixel art scene of a blocky plains landscape at dawn for a sandbox
game main menu background, 1024x1024, used directly as final art with no
downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, straight-on distant view with only
mild depth, no realistic perspective, no painterly shading. The sky is
drawn as flat horizontal color bands with dithered band edges, not smooth
gradients.

Content: a wide grass plain of rolling blocky hills in fresh green, a few
round voxel oak trees, a small river winding to the horizon reflecting
the sky, two blocky sheep grazing far away. A low dawn sun as a pale gold
square disc near the horizon. Sky bands from bottom to top: warm rose,
pale cream, soft blue. A few flat blocky clouds. The bottom sixth of the
image is slightly darker calm grass kept simple.

Palette: greens #4A7E2F #5D9C3C #74B84E, water #3A6FB5 #4E88CE, sun
#FFD84A #F2A81E, sky bands #F2B8C6 #FFF3C4 #79A6FF, clouds #E7F4F8.

No text, no letters, no logo, no UI, no buttons, no watermark, no humans,
no photorealism, no 3D render, no blur, no smooth gradient, no vignette.
```

## 负面提示词

```
text, letters, logo, UI, buttons, watermark, humans, photorealism, 3D
render, blur, smooth gradient, vignette, realistic perspective, lens
flare
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 草绿主色与游戏 `grass-top`（`#5D9C3C`）同系，一眼认出是本游戏的平原
- [ ] 天空是色带不是平滑渐变；太阳无光晕
- [ ] 底部 1/6 无复杂细节，叠按钮仍可读
- [ ] 画面无文字、无 UI 元素
