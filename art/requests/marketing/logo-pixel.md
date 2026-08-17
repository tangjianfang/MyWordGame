# MKT-04 极简像素 LOGO

## 用途

**极简像素风变体 logo**：草方块的正面平视「证件照」，颜色压到 6 个以内。
用于 favicon、水印、贴纸等需要极小尺寸的场合——等距徽章缩太小会糊，这张不会。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 256 × 256 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255（洋红键控） |
| 颜色数 | **≤ 6 色**（刻意压到最少） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 草带主色 | `#5D9C3C` |
| 草带亮 | `#74B84E` |
| 泥土主色 | `#7A5A3C` |
| 泥土暗 | `#5F4630` |
| 泥点 | `#6B4F30` |
| 描边 | `#241A11` |

## 视觉描述

- 一个**正面平视**的方形草方块（相当于 `grass-side` 的图标化特写）：
  顶部 1/4 是草绿带，向下咬出三个方齿；下部 3/4 泥土色，散几粒深色泥点
- 全图只允许上表 6 个颜色，色块边界全部硬边
- 外围 2 像素深色描边，四角可轻圆角
- 主体居中占 80%，四周均匀洋红
- 像素要**极大极 Chunky**（最终 256×256 上看起来像 16×16 放大）

## AI 提示词

```
A minimal flat pixel art app icon of a grass block for a sandbox block
game, 1024x1024, designed to be downscaled to 256x256 pixel art.

Style: extremely chunky minimal pixel art, flat front view, no
perspective, no shading gradient, only very large pixels, like a 16x16
sprite scaled up.

Content: a single square grass block portrait facing the viewer: the top
quarter is a green grass band with three square notches hanging down into
the soil, the lower three quarters is brown soil with a few darker
speckle dots. A thick dark outline around the whole square, slightly
rounded corners. Only about six colors total, minimal and iconic.

Palette strictly: grass #5D9C3C #74B84E, soil #7A5A3C #5F4630, speckles
#6B4F30, outline #241A11.

Everything outside the block is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing, with an even magenta margin on
all sides.

No text, no letters, no words, no isometric view, no cube sides, no
shadow, no glow, no watermark, no 3D render, no photorealism, no
gradient.
```

## 负面提示词

```
text, letters, words, isometric, cube sides, 3D, shadow, glow,
watermark, photorealism, gradient, transparent background, checkerboard,
small pixels, fine detail
```

## 后处理

1. 最近邻降采样到 256 × 256
2. 键控洋红为透明，去洋红边
3. 量化到上表 6 个色值（一个多余的都不留）
4. 检查描边闭合

## 验收标准

- [ ] 尺寸恰为 256 × 256，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 6
- [ ] 正面平视，无任何等距/立体侧面
- [ ] 缩到 16×16 预览：草带与泥土仍分得清，认得出是草方块
