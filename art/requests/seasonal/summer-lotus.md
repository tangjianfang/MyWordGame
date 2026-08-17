# SEASON-08 夏日荷塘方块（顶面）

## 用途

夏季变体的**水面顶面**贴图：荷塘——水上浮几片荷叶、开一朵小荷花。
结构与 `blocks/water.md` 同构，夏天活动期间替换池塘/湖面的水面材质。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无 |
| 颜色数 | 6 – 7 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 水深/主/亮（与 water 同） | `#2C5893` `#3A6FB5` `#4E88CE` |
| 荷叶绿 | `#3F7A2E` `#52963B` |
| 荷叶缘暗 | `#2F5D24` |
| 荷花粉 | `#F2C4D0` `#E8A8BC` |

## 视觉描述

俯视的夏日荷塘水面：

- 底子是三档蓝的塘水，散布略亮的短横波纹点（同 `water` 的画法）
- 水面浮 **5 片荷叶**：平面圆盘绿块，每片一侧带个楔形小缺口，外缘一圈暗绿
- 荷叶散布均匀，其中一两片跨过格边（跨边对齐）
- 其中一片上开一朵小粉荷花（几粒粉像素簇）
- 不画鱼、青蛙、涟漪圈、天空倒影

## AI 提示词

```
A seamless tileable pixel art block texture of a summer lotus pond
surface, flat top-down view, 1024x1024, designed to be downscaled to
32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective,
even illumination. This is the top face texture of a water block in
summer.

Content: calm pond water in layered blue tones with a subtle scatter of
slightly lighter short horizontal wave dashes. Floating on the water
five round lotus pads as flat green discs, each with a small wedge
notch cut from one side and a thin darker green rim, scattered evenly,
one or two pads crossing the tile borders and lining up. On one pad, a
single small pink lotus flower of a few clustered pink pixels.

Palette strictly: water #2C5893 #3A6FB5 #4E88CE, pads #3F7A2E #52963B,
pad rim #2F5D24, lotus #F2C4D0 #E8A8BC.

CRITICAL: tiles seamlessly on all four edges, wave dashes and pads
crossing borders must line up.

No border, no frame, no fish, no frogs, no ripple rings, no sky
reflection, no clouds, no text, no letters, no watermark, no gradient,
no glow, no 3D render, no anti-aliasing.
```

## 负面提示词

```
fish, frogs, ripples, reflections, clouds, border, frame, text,
letters, watermark, gradient, glow, 3D render, anti-aliasing,
perspective
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 7 个色值量化
3. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝
- [ ] 水色与 `water.png` 逐色一致（三蓝不许换）
- [ ] 荷叶恰好约 5 片、每片带缺口与暗缘
- [ ] 4×4 平铺荷叶不成规整网格；无鱼无青蛙
