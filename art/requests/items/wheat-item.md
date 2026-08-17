# 小麦

## 用途

热键栏/背包中的小麦物品图标（成熟小麦收获物）。三支麦秆捆成一束。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 背景 | 整片纯洋红 `#FF00FF`，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 麦秆 | `#D9C89A` |
| 麦穗主 | `#E8C04A` |
| 麦穗亮 | `#F7E08A` |
| 麦穗暗 | `#A8842E` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视三支麦束：中间一支略高，穗头在上、秆在下，底部一小段深色束带
- 穗头两侧小穗粒刻痕（1–2 像素锯齿），秆为浅麦秆色
- 金色与 m10 金系同源，与 `seeds-wheat` 并排能区分（整株 vs 散粒）
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single wheat bundle icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A small bundle of three golden wheat
stalks held together, seen straight from the front: pale straw stems at the
bottom tied by one small dark band, and plump golden-yellow grain heads with
tiny spikelet notches on both sides at the top; the middle stalk stands
slightly taller than the two side stalks. The bundle fills the frame, taller
than wide. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #D9C89A, #E8C04A, #F7E08A, #A8842E, #1A1A1A outline
only.

No text, no watermark, no bread, no flour, no field, no gradient, no cast
shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, bread, flour, field, haystack, sickle, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing, extra objects, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 三支穗头分明，底部束带可见
