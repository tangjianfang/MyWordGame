# MKT-07 预告卡

## 用途

**上线前的悬念预告卡**（社交平台发布「要做这个游戏了」）：
近全黑画面里只有一颗被点亮的草方块和高处一双苏醒的青色机甲眼。
发布文案由社媒平台叠加，图内不画字。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 背景 | `#0D0D1A` `#14142A` |
| 地面 | `#4A4A4A` `#6E6E6E` |
| 草方块 | `#74B84E` `#5D9C3C` `#7A5A3C` |
| 机甲眼青光 | `#4CC6C4` `#A8F2EF` |
| 机甲轮廓紫 | `#3A2A5E` `#5C3D8F` |
| 点缀金 | `#F7DA7A` |

## 视觉描述

一张几乎全黑的悬念图：

- 画面下方中央：一颗小草方块被一小圈暖金光照亮，站在裂开的深灰石地上
- 高处黑暗中：**两只发光的青蓝色方形眼睛**（机元守卫，与
  `scenes/boss-intro.md` 同一角色），周围隐约可辨紫色的巨型机甲头肩轮廓
  （只比背景亮一点，似有似无）
- 两侧黑暗石壁嵌两道细青色矿脉微光
- 大部分画面是空黑——留白即悬念
- 无文字、无 UI、无光晕泛滥

## AI 提示词

```
Voxel pixel art teaser card for announcing a sandbox block game,
1024x1024, used directly as final art with no downscaling.

Style: chunky voxel pixel art, flat color faces, hard pixel edges, very
dark moody scene, no realistic perspective, no painterly shading, no
glow bloom.

Content: an almost black scene. In the lower center, one small grass
block cube lit by a narrow warm pool of gold light, standing on cracked
dark stone ground. High above it in the darkness, one pair of two
glowing cyan-blue square robot eyes with faint gold rims, around them
the barely visible outline of a huge blocky mecha head and shoulders
traced in slightly lighter purple. Two thin cyan ore veins glimmer
faintly in the dark walls at the sides. Most of the image stays empty
darkness.

Palette: background #0D0D1A #14142A, ground #4A4A4A #6E6E6E, grass block
#74B84E #5D9C3C #7A5A3C, eyes #4CC6C4 #A8F2EF, purple trace #3A2A5E
#5C3D8F, warm light #F7DA7A.

No text, no letters, no numbers, no logo, no watermark, no UI, no
humans, no photorealism, no 3D render, no blur, no smooth gradients, no
vignette.
```

## 负面提示词

```
text, letters, numbers, logo, watermark, UI, humans, photorealism, 3D
render, blur, smooth gradients, vignette, horror style, blood
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 双眼是全图最亮的青色，草方块次之，其余压暗
- [ ] 机甲轮廓「隐约可辨」而非清晰——保留悬念
- [ ] 无文字、无 UI、无血腥
