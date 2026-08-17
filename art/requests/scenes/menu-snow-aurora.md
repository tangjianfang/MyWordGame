# SCENE-02 主菜单背景 · 雪原极光

## 用途

主菜单三选一背景之二：**Snow 群系夜晚极光**。深色夜空与极光是三张背景里最「哇」的一张，
作为默认首选背景。上叠标题与按钮。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 1024 × 1024（**直用，不降采样**） |
| 生成尺寸 | 1024 × 1024 |
| 平铺 | 否 |
| Alpha | 无 |

## 调色板

沿用游戏 Snow 群系 + `sky/sun-moon` 的月亮配色：

| 用途 | HEX |
| --- | --- |
| 雪 | `#F6FAFC` `#E8F0F4` `#D0DEE8` |
| 针叶深绿 | `#1E3B2A` `#2A5038` |
| 月亮 | `#F2F2EC` `#C9C9C0` |
| 极光 | `#4CC6C4` `#66B04A` |
| 夜空 | `#0B1026` `#16224A` |
| 星点 | `#F2F2EC` |

## 视觉描述

- 雪原：白色与淡蓝的方块雪丘，地平线上一排戴雪帽的深绿体素针叶树，一小片冻湖
- 夜空：满月（带灰色环形山的像素圆盘）+ 散布白点星
- 两三条**青绿色极光缎带**在上半空弯曲流动——缎带画成宽幅实色条带、
  下缘阶梯状锯齿，不发光晕、不做半透明
- 底边 1/6 是安静的暗雪坡，留给按钮

## AI 提示词

```
Voxel pixel art scene of a blocky snowfield at night under a bright
aurora for a sandbox game main menu background, 1024x1024, used directly
as final art with no downscaling.

Style: chunky voxel pixel art, terrain built from visible cubic blocks,
flat color faces, hard pixel edges, no realistic perspective. The sky is
flat color areas with dithered edges; auroras are drawn as broad solid
stair-stepped color ribbons, no smooth gradients, no glow.

Content: a snow plain of white and pale blue blocky drifts, dark green
voxel pine trees with snow caps along the horizon, a small frozen lake
patch, one full pixel moon disc with gray craters, scattered white star
dots. Two or three aurora ribbons in cyan and green curving across the
upper half, their lower edges stair-stepped. The bottom sixth is calm
darker snow kept simple.

Palette: snow #F6FAFC #E8F0F4 #D0DEE8, pines #1E3B2A #2A5038, moon
#F2F2EC #C9C9C0, aurora #4CC6C4 #66B04A, night sky #0B1026 #16224A,
stars #F2F2EC.

No text, no letters, no logo, no UI, no buttons, no watermark, no humans,
no photorealism, no 3D render, no blur, no smooth gradient, no glow, no
vignette.
```

## 负面提示词

```
text, letters, logo, UI, buttons, watermark, humans, photorealism, 3D
render, blur, smooth gradient, glow, bloom, vignette, realistic aurora
photography
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 极光是实色条带 + 阶梯边缘，不是模糊光带
- [ ] 月亮配色与 `sky/sun-moon` 的月亮（`#F2F2EC` + 灰环形山）一致
- [ ] 针叶绿是深冷绿（`#1E3B2A` 系），不与草绿混淆
- [ ] 底部 1/6 简洁，叠按钮可读；画面无文字
