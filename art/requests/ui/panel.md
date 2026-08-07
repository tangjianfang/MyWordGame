# U-06 界面面板底

## 用途

物品栏、暂停菜单、设置界面的背景板。所有弹出式界面共用这一张，
靠九宫格拉伸适配不同尺寸，因此**只需要一张图**。

面板必须**不透明**，把身后的游戏画面完全挡住（半透明会让界面上的文字看不清）；
界面之外的变暗遮罩由引擎画一个半透明黑色全屏 Quad 实现，不需要贴图。

## 九宫格切分（必须严格遵守）

贴图 64 × 64，四边边界均为 **16 像素**。
中间可拉伸区是第 16–47 列、第 16–47 行，**必须是纯色**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 64 × 64 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（走九宫格） |
| Alpha | 有，仅 0 或 255（只有四角的圆角是透明的） |
| 颜色数 | 5 – 7 色 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 | `#17140F` |
| 外框亮边 | `#8B7F6F` |
| 外框主色 | `#6B6155` |
| 外框暗边 | `#443D34` |
| 内面主色 | `#57503F` |
| 内面暗边 | `#3A342A` |

整体是偏暖的深灰褐，与 `button`（U-05）同一色系但**更暗**，
这样按钮放在面板上能浮出来。

## 视觉描述

由外向内四层，每层的宽度是固定的：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 外框立体边 | 2 像素 | 上/左 `#8B7F6F`，下/右 `#443D34` |
| 3. 外框面 | 9 像素 | `#6B6155` |
| 4. 内凹分界 | 1 像素 | 上/左 `#3A342A`，下/右 `#8B7F6F`（内凹方向与外框相反） |
| 5. 内面 | 其余 | `#57503F` 纯色 |

1 + 2 + 9 + 1 = 13，加上过渡余量共 16 像素，正好等于九宫格边界。

- 四角切掉 2 个像素做圆角
- **内面必须是完全的纯色**，不加任何纹理、噪点、渐变——它会被拉伸到几百像素
- 不要画钉子、铆钉、花纹、装饰角

## AI 提示词

这张图的几何全部由固定数值决定，**强烈建议直接用脚本生成而不是 AI**
（`art/tools` 里的后处理工具可以顺带做这件事）。

若仍要用 AI 起稿：

```
A pixel art UI panel background for a retro voxel game, 1024x1024, designed to be
downscaled to 64x64 pixel art. Square, front view, flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient, no texture.

Content: a thick rectangular frame around a large flat inner area. From outside in:
a one-pixel black outline, then a two-pixel bevel that is light on the top and left
and dark on the bottom and right, then a wide flat warm gray-brown frame band, then a
one-pixel inset line that is dark on the top and left and light on the bottom and
right, then a large completely flat darker inner field that fills the rest.

Color palette strictly limited to: #17140F, #8B7F6F, #6B6155, #443D34, #57503F,
#3A342A. Warm dark gray-brown.

The inner field must be one single flat color with absolutely no texture, no noise,
no pattern, and no gradient.

The four corners are clipped by two pixels; the clipped corners and nothing else are
flat pure magenta #FF00FF with hard edges and no anti-aliasing.

No rivets, no nails, no screws, no ornaments, no scrollwork, no wood grain,
no text, no icons, no glow, no drop shadow, no 3D render, no perspective, no
watermark.
```

## 后处理

1. 最近邻降采样到 64 × 64
2. 键控洋红为透明，去洋红边
3. 量化到调色板
4. **逐层校正**：按上表的厚度表把每一层的像素行/列拉直，一层都不能差
5. **纯色化内面**：把第 16–47 列 × 第 16–47 行整块填成 `#57503F`
6. 拉伸测试

## 验收标准

- [ ] 尺寸恰为 64 × 64
- [ ] 中间区（16..47 × 16..47）为**单一颜色**，方差为 0
- [ ] 外描边为闭合的 1 像素黑线，仅四角各 2 像素透明
- [ ] 九宫格拉伸到 400 × 220 后，边框宽度不变、无拉伸变形、无接缝
- [ ] 把 `button-normal`（U-05）叠在面板上，按钮明显比面板亮，能浮出来
- [ ] 面板上放白色文字，对比度足够（面板内面明度 ≤ 90）
