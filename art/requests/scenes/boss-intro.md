# SCENE-05 章节过场 · 机元守卫登场

## 用途

章节过场图之二：**Boss 登场**。第 3 波的机元守卫（machine-guardian，
紫金机甲巨人、眼发蓝光）开战前的登场定格图，触发 Boss 战时全屏展示。
造型基调与 `entities/machine-guardian.md` 的图标（64×64 正脸）保持一致。

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
| 机甲紫 | `#3A2A5E` `#5C3D8F` `#7B5CB8` |
| 机甲金饰 | `#DCAE3A` `#F7DA7A` |
| 眼部青光 | `#4CC6C4` `#A8F2EF` |
| 石材 | `#4A4A4A` `#6E6E6E` |
| 幽暗底 | `#14142A` `#1E2A44` |
| 描边 | `#241A11` |

## 视觉描述

地下遗迹大厅，Boss 从黑暗中苏醒：

- 巨大的方块机甲巨人占据画面中央偏上：紫色装甲板 + 金色包边，肩宽体沉
- 一只巨拳拄地，头颅低垂——**两只青蓝色方形眼睛亮起**，是全画面唯一强光源
- 胸口几道金色符纹微亮；黑暗石壁上嵌青/金矿脉微光
- 破碎的石柱与裂缝石砖地面；低机位仰视，气势压人
- 气质是「卡通机器人反派」，威严但不血腥吓人（孩子要能看）

## AI 提示词

```
Voxel pixel art scene for a boss introduction interstitial: a huge mecha
guardian awakening in an underground ruin, 1024x1024, used directly as
final art with no downscaling.

Style: chunky voxel pixel art, everything built from visible cubic blocks,
flat color faces, hard pixel edges, low camera looking up, no realistic
perspective, no painterly shading.

Content: a dark underground cavern with broken stone pillars and a floor
of cracked stone bricks. In the center stands a huge blocky mecha giant
with purple armor plates and gold trim, broad shoulders, one massive fist
resting on the ground, head lowered with two glowing cyan-blue square
eyes lighting the dust around them. Faint gold runes on the chest. Cyan
and gold ore veins glimmer in the dark walls. Friendly cartoon robot
villain feel, imposing but not gory, not scary flesh.

Palette: purples #3A2A5E #5C3D8F #7B5CB8, gold #DCAE3A #F7DA7A, eye cyan
#4CC6C4 #A8F2EF, stone #4A4A4A #6E6E6E, dark background #14142A #1E2A44,
outline #241A11.

No text, no letters, no numbers, no watermark, no logo, no UI, no humans,
no blood, no photorealism, no 3D render, no blur, no smooth gradients, no
vignette.
```

## 负面提示词

```
text, letters, numbers, watermark, logo, UI, humans, blood, gore,
skeletons, photorealism, 3D render, blur, smooth gradients, vignette,
horror style
```

## 后处理

1. 不降采样（1024 × 1024 直用）
2. 按上表调色板量化（允许至多 3 个过渡色）
3. 存 32 位 PNG；不做平铺自检

## 验收标准

- [ ] 尺寸恰为 1024 × 1024
- [ ] 紫甲 + 金饰 + 青色方眼三要素齐（与 machine-guardian 图标同一角色）
- [ ] 眼睛是画面最亮的青色，无光晕溢出
- [ ] 无血腥、无骸骨元素（面向儿童）
- [ ] 画面无文字、无人形
