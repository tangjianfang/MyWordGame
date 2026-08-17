# E-20 机元守卫（Boss）

## 用途

Boss 图标：机元守卫（machine-guardian），守卫机元矿的巨型机甲 Boss，掉机元
碎片与下界合金线索。Boss 血条头像、战斗登场画面、图鉴卡共用本图。
Boss 图标比普通生物大一档（64×64），细节要求更高。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | **64 × 64 像素**（Boss 专用大图标） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无，完全不透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 紫甲主 | `#6A4A9C` |
| 紫甲暗 | `#463068` |
| 金饰主 | `#DCAE3A` |
| 金饰暗 | `#A87322` |
| 眼蓝光 | `#4CC6C4` |
| 眼核亮 | `#A8F2EF` |

## 视觉描述

紫金机甲巨人的正脸：深紫色金属头盔配暗紫阴影，下颌与眉骨镶金边，头顶一顶
金色冠饰，面甲上一条横置的发光蓝色眼缝——眼缝中心更亮，像扫描光带在扫你。
对称、威压、无多余杂色；蓝眼是唯一光源感元素。

## AI 提示词

```
A single boss monster icon for a boss health bar or codex card, 1024x1024
pixel art designed to be downscaled to 64x64. A menacing machine golem
face viewed from the front: armored helmet in deep purple metal with
darker purple shading, golden trim along the jaw and brow, a heavy
golden crest on top, and one narrow horizontal visor eye slot glowing
bright cyan-blue with a lighter blue-white core. Symmetrical, square
composition with no background — the machine guardian fills the frame.

Color palette strictly: #6A4A9C, #463068, #DCAE3A, #A87322, #4CC6C4,
#A8F2EF only.

No text, no fire, no red, no smoke, no shadow. Hard pixel edges, no
anti-aliasing.
```

## 负面提示词

```
text, watermark, shadow, blur, 3D render, perspective, gradient,
photorealism, red eyes, fire, smoke, body, shoulders, arms, background
scenery, skeleton face
```

## 后处理

降采样到 64×64 + 调色板量化。

## 验收

- [ ] 64×64 PNG，32 位 RGBA
- [ ] 不透明（Alpha 全部 255）
- [ ] 颜色数 ≤ 6
- [ ] 缩到 32×32 预览时蓝眼缝仍是一条清晰亮线（Boss 辨识度）
