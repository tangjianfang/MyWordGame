# FX-01 环境·烟团

## 用途

环境粒子：篝火/熔炉排烟、火把熄灭余烟。粒子系统按生命期缩放旋转本图，
**背景必须整片纯洋红键控为透明**（AI 无法输出 Alpha，见 art/README.md 硬性约束 4）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 烟亮 | `#A8A8A8` |
| 烟主 | `#8A8A8A` |
| 烟暗 | `#5A5A5A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一团由 3–4 个圆泡叠成的烟团：中心偏亮、下缘偏暗，轮廓圆润无棱角。
32×32 下读作「一缕灰烟」即可，不要画成云朵或蘑菇云。

## AI 提示词

```
A single particle sprite for a retro voxel game, 1024x1024 pixel art
designed to be downscaled to 32x32. A soft rounded cluster of three to
four overlapping light gray smoke puffs, brighter gray in the center and
darker gray on the lower edges, centered on the canvas. The entire
background around the smoke is solid flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing between smoke and magenta.

Smoke palette strictly: #A8A8A8, #8A8A8A, #5A5A5A only.

No text, no shadow, no gradient, no outline, no blur, no semi-transparent
smoke. Hard pixel edges.
```

## 负面提示词

```
text, watermark, shadow, outline, blur, gradient, 3D render, smoke with
alpha look, clouds, factory chimney, fire, dark background, gray
background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. 去洋红边：不透明像素中 R 与 B 同时高于 G 的偏紫过渡色替换为相邻烟灰色
4. 量化到上表 3 个灰色

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 3，全部为灰色系
- [ ] 不存在任何偏紫残留（无 R > G 且 B > G 的不透明像素）
- [ ] 烟团占画布 40% – 70%，四周留洋红转透明的空隙
