# 铁靴子

## 用途

热键栏/背包/盔甲栏中的铁靴子图标。铁系盔甲第四件（护足）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 铁暗 | `#5F5F5F` |
| 铁主 | `#8A8A8A` |
| 铁亮 | `#C8C8C8` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 侧视单只靴（鞋头朝右）：高踝筒 + 前伸鞋头，L 形剪影
- 鞋底一层略深铁色，踝筒前面一道亮色高光
- 四材料靴子共用同一剪影，只换材质三阶色；与 `legs-iron` 并排能区分（靴更矮胖）

## AI 提示词

```
A single iron boots icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A single heavy armored boot seen from the side,
toe pointing right: an L-shaped silver-gray metal boot with a tall ankle
shaft, a flat slightly darker metal sole plate under the toe, and one light
highlight streak on the front of the shaft. The boot fills the frame. Black
1-pixel outline. The empty corners are one flat solid dark backdrop color
#2A2620.

Color palette strictly: #5F5F5F, #8A8A8A, #C8C8C8, #2A2620, #1A1A1A only.

No text, no watermark, no pair of boots, no foot inside, no laces, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, two boots, pair, foot, toes, laces, sock, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 单只靴侧视，鞋底板与踝筒分明
