# U-07 标题 LOGO

## 用途

主菜单顶部的游戏标题。

## 先说清楚：文字不要交给 AI

图像模型**画不对文字**。哪怕提示词里写死了字母，产出也常常是拼错的、字形不一致的、
带随机装饰笔画的。标题是玩家看到的第一样东西，拼错字就前功尽弃。

所以本资源拆成两部分：

| 部分 | 产出方式 |
| --- | --- |
| **标题文字** | 用像素字体在图像编辑器里排版，或直接在引擎里用 TextMeshPro + 像素字体渲染 |
| **装饰元素** | 由 AI 生成（见下方提示词），单独一张带 Alpha 的图 |

装饰元素与文字在引擎里叠加，不合并成一张图——这样以后改名字不用重做美术。

## 交付物

| 文件 | 尺寸 | 说明 |
| --- | --- | --- |
| `title-decor-block.png` | 96 × 96 | 一个 45° 等距视角的草方块，放在标题左右两侧当装饰 |

标题文字本身不产出图片文件。

## 规格（`title-decor-block.png`）

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 96 × 96 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 颜色数 | 8 – 12 色（不含透明） |

## 调色板（严格使用）

必须与游戏内的草方块贴图同色系，否则主菜单和游戏画面像两个游戏：

| 面 | 用途 | HEX |
| --- | --- | --- |
| 顶面（最亮） | 草绿高光 | `#74B84E` |
| 顶面 | 草绿主色 | `#5D9C3C` |
| 左侧面（中） | 草边 | `#4A7E2F` |
| 左侧面 | 泥土主色 | `#7A5A3C` |
| 右侧面（最暗） | 草边暗 | `#3B6626` |
| 右侧面 | 泥土暗 | `#5F4630` |
| 描边 | 深棕黑 | `#241A11` |

三个面的明度关系固定为 **顶面 > 左侧面 > 右侧面**，模拟从左上打来的光。

## 视觉描述

一个等距（isometric）草方块：

- 顶面是菱形，占上半部分，画草绿色的顶面纹理（简化版 `grass-top`）
- 左侧面和右侧面各是一个平行四边形，**上部 1/4 是草绿边，下部 3/4 是泥土**
  （对应 `grass-side` 的结构）
- 整个方块外围一圈 **2 像素的深棕黑描边** `#241A11`
- 三个面的交界处也画 1 像素描边，让体积清晰
- 方块居中，四周留出均匀空白（洋红）
- 不要画阴影、不要画倒影、不要画高光星芒

## AI 提示词

```
An isometric pixel art grass block for a retro voxel game title screen, 1024x1024,
designed to be downscaled to 96x96 pixel art.

Style: chunky pixel art, flat shading, isometric 2:1 projection, no perspective
distortion, no realistic lighting.

Content: a single cube seen from a 45 degree isometric angle showing exactly three
faces. The top face is a green diamond with simple grass texture. The left and right
faces are parallelograms whose upper quarter is green grass edge and whose lower
three quarters is brown soil with small speckles. The top face is the brightest, the
left face is mid tone, the right face is the darkest. The whole cube has a thick dark
brown outline, and the three face boundaries also have thin dark lines.

Color palette strictly limited to: #74B84E, #5D9C3C, #4A7E2F, #3B6626 (greens),
#7A5A3C, #5F4630 (soil browns), #241A11 (outline).

The cube is centered with an even margin on all sides. Everything outside the cube
outline is flat pure magenta #FF00FF, fully saturated, hard edges, no anti-aliasing.

No ground shadow, no cast shadow, no reflection, no glow, no sparkle, no star burst,
no floating particles, no grass blades sticking up, no flowers, no text, no letters,
no logo, no watermark, no 3D render, no photorealism.
```

## 负面提示词

```
text, letters, words, logo, title, watermark, signature, cast shadow, ground shadow,
reflection, glow, bloom, sparkle, star burst, particles, flowers, grass blades,
perspective distortion, vanishing point, 3D render, photorealistic, blur,
depth of field, gradient background, checkerboard, transparent background
```

## 标题文字的排版要求（引擎侧，不产出图片）

- 使用等宽像素字体，字号取 32 或 48 的整数倍，**禁止非整数缩放**（会糊）
- 文字颜色 `#F2E6C8`，加 2 像素 `#241A11` 描边和 2 像素向下的 `#241A11` 投影
- 装饰方块左右各一个，与文字基线对齐，间距为文字高度的 0.5 倍

## 后处理

1. 最近邻降采样到 96 × 96
2. 键控洋红为透明，去洋红边
3. 量化到上表 7 个色值
4. 检查外描边闭合、宽度处处 ≥ 2 像素

## 验收标准

- [ ] 尺寸恰为 96 × 96
- [ ] 32 位 PNG，Alpha 只有 0 和 255
- [ ] 恰好显示三个面，顶面最亮、右侧面最暗
- [ ] 外描边闭合无缺口
- [ ] 侧面的「上部草绿、下部泥土」比例与 `grass-side.png` 一致
- [ ] 颜色全部落在上表（允许至多 3 个中间色）
- [ ] 图中**不含任何文字**
- [ ] 与游戏内的草方块并排截图对比，认得出是同一种方块
