# M11-U14 附魔台面板底（ui-enchant-panel）

## 用途

milestone-11 第 2 波「附魔生效」：附魔台界面的背景板。
结构逐层照抄 `panel`（U-06）——**必须与其同厚度同色系**，
唯一差异是四角各加一枚紫色符文点，把「这是附魔界面」标出来。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（走九宫格拉伸） |
| Alpha | 有，仅 0 或 255（只有四角的圆角是透明的） |
| 输出数量 | 1 张：`ui-enchant-panel.png` |

## 九宫格切分（与 panel 相同）

贴图 64 × 64，四边边界均为 **16 像素**；中间 32 × 32 必须是纯色。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 | `#17140F` |
| 外框亮边 | `#8B7F6F` |
| 外框主色 | `#6B6155` |
| 外框暗边 | `#443D34` |
| 内面主色 | `#57503F` |
| 内面暗边 | `#3A342A` |
| 四角符文点（紫） | `#8C4FD4` |

紫色取自 m10 机元矿（`machine-essence-ore`）的紫罗兰系，
「神秘科技」的语义全游戏一致。

## 视觉描述

由外向内四层，厚度与 `panel` 完全相同：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 外框立体边 | 2 像素 | 上/左 `#8B7F6F`，下/右 `#443D34` |
| 3. 外框面 | 9 像素 | `#6B6155` |
| 4. 内凹分界 | 1 像素 | 上/左 `#3A342A`，下/右 `#8B7F6F` |
| 5. 内面 | 其余 | `#57503F` 纯色 |

差异点：**四角** 16 × 16 的外框面内各加一枚 2 × 2 紫色符文点 `#8C4FD4`。
四角不参与拉伸，符文点不会变形。除此之外不加任何纹理。

## AI 提示词

```
A pixel art enchanting table UI panel background for a retro voxel game,
1024x1024, designed to be downscaled to 64x64 pixel art. Square, front view,
flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient, no texture.

Content: a thick rectangular frame around a large flat inner area. From outside
in: a one-pixel black outline, a two-pixel bevel that is light on the top and left
and dark on the bottom and right, a wide flat warm gray-brown frame band, a
one-pixel inset line that is dark on the top and left and light on the bottom and
right, then a large completely flat darker inner field. On each of the four
corners of the wide frame band sits one single small two-by-two purple rune dot.

Color palette strictly limited to: #17140F, #8B7F6F, #6B6155, #443D34, #57503F,
#3A342A, #8C4FD4.

The inner field must be one single flat color with absolutely no texture, no
noise, no pattern, and no gradient.

The four corners of the panel are clipped by two pixels; the clipped corners and
nothing else are flat pure magenta #FF00FF with hard edges and no anti-aliasing.

No other runes, no glyphs, no magic circles, no scrollwork, no ornaments, no
text, no icons, no glow, no drop shadow, no 3D render, no watermark.
```

## 负面提示词

```
runes beyond four dots, glyphs, magic circles, books, orbs, scrollwork, ornaments,
text, icons, glow, drop shadow, 3D, gradient, texture, watermark
```

## 后处理

1. 最近邻降采样到 64 × 64
2. 键控：四角圆角洋红 Alpha = 0，其余 255；去洋红边
3. 量化到 7 个色值
4. **纯色化内面**：把第 16..47 列 × 行整块填成 `#57503F`
5. 校正四角符文点为 2 × 2 `#8C4FD4`、四角对称
6. 九宫格拉伸测试

## 验收标准

- [ ] 尺寸恰为 64 × 64
- [ ] 中间区（16..47 × 16..47）为单一颜色，方差为 0
- [ ] 与 `panel.png` 叠放对比：除四角符文点外逐像素一致
- [ ] 四枚符文点均为 2 × 2、同色、四角对称
- [ ] 面板上放白色文字对比度足够（内面明度 ≤ 90）
