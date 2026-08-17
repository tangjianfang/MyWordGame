# ARCH-11 徽记旗帜方块

## 用途

村庄**主旗**贴图：深蓝布面 + 金色盾徽（盾里一把镐），挂在村口/水井广场，
是「这个村子的领地旗」。与 `banner-plain` 同款布底，加徽记。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 无 |
| 颜色数 | 5 – 7 色 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 布面深蓝 | `#2C5893` |
| 布面蓝 | `#3A6FB5` |
| 徽记金 | `#DCAE3A` |
| 徽记亮金 | `#F7DA7A` |
| 徽记描边 | `#241A11` |

布面沿用游戏水蓝系（`water`），与素色旗的暖红形成主次。

## 视觉描述

一整幅深蓝垂挂布（底子与 `banner-plain` 同画法：织孔点 + 竖褶带），中央一枚**金色盾徽**：

- 盾形上圆下尖，1 像素深描边，盾面金色带一道斜向亮金高光
- 盾中央刻一把**斜置的镐**剪影（深描边色），呼应游戏的挖矿主线
- 徽记每格出现一次、居中、完整落在格内不碰边（每方块显示一次，属预期重复）
- 不画王冠、不画文字、不画流苏

## AI 提示词

```
A seamless tileable pixel art block texture of a heraldic crest banner,
flat front view, 1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a single sheet of deep blue hanging cloth with a subtle woven
stitch dot pattern and broad vertical fold bands alternating slightly
darker and lighter. Centered on the cloth, one gold heraldic shield
emblem: a shield shape rounded on top and pointed at the bottom, outlined
in near-black, filled gold with one diagonal bright gold highlight band,
and a single diagonal pickaxe silhouette engraved in dark outline in the
middle of the shield. The emblem appears exactly once per tile, fully
inside the tile, never touching the edges. Weave and folds run edge to
edge and line up across tile borders.

Palette strictly: cloth #2C5893 #3A6FB5, gold #DCAE3A #F7DA7A, outline
#241A11.

CRITICAL: tiles seamlessly on all four edges, fold bands line up across
tile borders.

No border, no frame, no crown, no text, no letters, no tassels, no
fringe, no pole, no watermark, no gradient, no glow, no 3D render, no
anti-aliasing.
```

## 负面提示词

```
crown, text, letters, tassels, fringe, pole, border, frame, watermark,
gradient, glow, 3D render, anti-aliasing, perspective, realistic cloth
folds
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 按上表 5 个色值量化
3. 偏移半幅自检接缝

## 验收标准

- [ ] 尺寸恰为 32 × 32，四边无缝
- [ ] 盾徽每格恰好一次、居中、不碰边
- [ ] 盾内镐剪影清晰可辨（缩小到 32×32 后仍认得出是把镐）
- [ ] 布面蓝与游戏 `water` 同系，徽记金与金币/金锭同系
- [ ] 图中不含任何文字
