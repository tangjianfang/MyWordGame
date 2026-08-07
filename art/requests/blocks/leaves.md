# B-12 树叶

## 用途

树冠方块。是本项目**第一个带 Alpha 镂空**的方块，渲染时走 Alpha Test（cutout）而非
半透明混合，所以每个像素只有「完全不透明」和「完全透明」两种状态，**不允许半透明**。

镂空的作用是让阳光从叶子缝隙漏进树下，也让远处的树看起来蓬松而不是绿色立方体。

## Alpha 的产出方式（重要）

图像模型**无法可靠地输出 Alpha 通道**——第 0 批 10 张图全部产出为 24 位 RGB，
透明区域丢失。因此本需求要求：

> **生成时把镂空区域画成纯洋红 `#FF00FF`**，后处理再把该颜色键控为透明。

洋红是自然界与本项目调色板中都不存在的颜色，不会与叶子误伤。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **四边无缝** |
| Alpha | 有，仅 0 或 255，**不允许中间值** |
| 镂空面积 | 占全图 **15% – 25%** |
| 颜色数 | 5 – 8 色（不含透明） |

镂空面积低于 15% 看不出通透感，高于 25% 树冠会显得稀疏破烂。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 深叶阴影 | `#24491C` |
| 阴影 | `#2F5D24` |
| 主色 | `#3F7A2E` |
| 亮部 | `#52963B` |
| 高光 | `#66B04A` |
| 镂空键控色 | `#FF00FF`（后处理删除） |

叶绿比 `grass-top` 的草绿**更深、更冷**，这样树冠压在草地上时层次分明。

## 视觉描述

密集交叠的小叶片团。

- 叶片为 2–4 像素的不规则小块，**不要画出可辨认的单片树叶形状**（不要有叶脉、叶柄）
- 明暗随机交错，营造「叶子有前有后」的层次
- 镂空点**散布**在整张图上，单个孔洞 1–3 像素，不要集中在中央形成一个大洞
- 孔洞边缘不要加深色描边（会在渲染时形成黑边）
- 不要画树枝、不要画果实、不要画花

## AI 提示词

```
A seamless tileable pixel art texture of dense tree leaves, flat front view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination. Base color is medium bright green filling most of the canvas — NOT
dark, NOT black, NOT navy, NOT teal anywhere; think bright bush green, not shadow.

Content: densely overlapping small leaf clusters, irregular rounded blobs, no
veins, no stems. Light and dark greens interleave randomly for depth.

Mentally divide the canvas into an 8x8 grid of 128x128 cells. Pick roughly 12 of
the 64 cells, scattered evenly (not clustered), and fill each ENTIRELY with one
LARGE solid magenta blob about the size of that whole cell — each magenta gap must
be at least 100 pixels across, big and obvious, never a small speck, never a thin
line, never a tiny dot. The grid is only a size guide; gap edges should look like
organic rounded holes, not perfect squares.

Every gap is pure magenta #FF00FF, flat, fully saturated, hard edges, no
anti-aliasing. Magenta gaps cover roughly 20 percent of the total image area.

Leaf palette strictly limited to: #24491C, #2F5D24, #3F7A2E, #52963B, #66B04A.

CRITICAL: tiles seamlessly on all four edges, including the magenta gaps.

No border, no frame, no branches, no fruit, no flowers, no text, no watermark,
no gradient, no dark background, no navy, no teal, no tiny dots, no thin lines.
```

## 负面提示词

```
gradient, drop shadow, border, frame, dark outline, branches, twigs, fruit,
berries, flowers, snow, autumn colors, yellow leaves, leaf veins, realistic
leaves, single leaf, text, watermark, blur, 3D render, perspective, glossy,
transparent background, checkerboard, night sky, stars, sparkles, dark
background, navy background, teal background, tiny scattered dots, thin lines,
small specks
```

## 后处理

1. 最近邻降采样到 32 × 32（**降采样会在洋红与绿色之间产生过渡色，必须先做第 2 步**）
2. **键控**：把所有「洋红占比 > 50%」的像素设为 Alpha = 0；其余像素 Alpha = 255
3. **去洋红边**：检查剩余不透明像素中是否有偏紫的过渡色（R 与 B 同时偏高），
   有则替换为相邻的叶绿色
4. 量化不透明像素到上表 5 个色值
5. 统计镂空面积，须落在 15% – 25%
6. 偏移半幅自检接缝（包括孔洞的衔接）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 保存为 32 位 PNG，**存在 Alpha 通道**
- [ ] 全图 Alpha 值只有 0 和 255 两种，无中间值
- [ ] Alpha = 0 的像素占比在 15% – 25%
- [ ] 不透明像素中不存在任何偏紫色（不存在 R > G 且 B > G 的像素）
- [ ] 偏移半幅后画面中央看不出接缝
- [ ] 与 `grass-top.png` 并排，树叶明显更深更冷
- [ ] 4×4 平铺预览中孔洞不形成规律阵列
