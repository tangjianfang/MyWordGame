# M10-B 机元矿石

## 用途

milestone-10 矿物进阶（spec §1）：机元是第四档顶材料（钻石镐才挖得动、y<16 极低稀有度），
装备提供生命上限加成。贴图名 `machine-essence-ore`，方块 id `machine_essence_ore`。

> 当前 `Assets/StreamingAssets/blocks/textures/machine-essence-ore.png` 是**程序生成的占位图**
> （`art/scripts/gen_m10_ore_placeholders.py`：stone.png 逐像素打底 + 确定性哈希布斑块，
> 仅为让材质库与贴图存在性测试通过）。正式美术入库后按下面规格替换。

## 生产方式与 B-14 矿石相同（务必先读 `ores.md`）

矿石**不要整张交给 AI 生成**，两层合成：

1. **底层**：逐像素复用已验收的 `stone.png`
2. **上层**：只让 AI 生成**矿物斑块层**（洋红背景 + 斑块），键控后叠到底层上

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 斑块层生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 底层继承 `stone.png` 的四边无缝；斑块层**不跨越边缘** |
| Alpha | 无，完全不透明 |
| 斑块面积 | 占全图 **12% – 20%**，4–7 个斑块，单个 3–6 像素 |
| 斑块约束 | 不触碰最外 2 圈像素；斑块间隔 ≥2 像素石头 |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 深（暗边） | `#5B2A8C` |
| 主 | `#8C4FD4` |
| 亮（高光） | `#C9A6F5` |

「机元」取科技感的**紫罗兰**——现役全部贴图的调色板里没有紫色，
一眼即「这矿和别的不一样」。不能偏粉也不能偏蓝。

## AI 提示词（斑块层）

```
A pixel art layer of scattered ore mineral deposits on a flat magenta background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game texture, flat shading, no perspective, even illumination.

Content: 4 to 7 irregular rounded mineral blobs, LARGE and prominent, each spanning
roughly 150 to 250 pixels across (about one sixth to one quarter of the image
width) — never small dots, never tiny gems. Blobs are evenly scattered with clear
separation between them. Each blob has a darker rim, a solid main color, and a
single small highlight dot in its upper-left area. Blobs are organic rounded
shapes, never diamond icons, never faceted gems, never crystals.

These blobs are mysterious machine violet purple: #5B2A8C, #8C4FD4, #C9A6F5 only.
Deep violet with a faint technological feel, NOT pink, NOT magenta, NOT blue,
NOT red-purple — must read as clearly violet.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing between the magenta and the blobs. Magenta covers about 65 percent of
the image, blobs cover about 35 percent.

The blobs must stay away from the outer border; leave a magenta margin of at least
8 percent of the image width on all four sides.

No stone texture, no rock background, no gradient, no vignette, no glow, no sparkle,
no lens flare, no faceted gem, no diamond shape, no metallic shine, no text,
no watermark, no 3D render, no perspective.
```

## 负面提示词

```
stone background, rock texture, gradient, vignette, drop shadow, glow, sparkle,
lens flare, shine, faceted gem, diamond shape, crystal cluster, jewel icon,
minecraft logo, text, watermark, signature, blur, depth of field, 3D render,
perspective, metallic reflection, transparent background, alpha channel,
checkerboard, border, pink tint, magenta blobs, blue tint, red tint, tiny dots,
small gems
```

## 后处理

同 `ores.md` 的七步（降采样 → 键控 → 去洋红边 → 量化 3 色 → 叠 `stone.png` →
查外圈 → 与既有四矿逐像素差分，差异只在斑块处）。

注意：去洋红边判据（`R > G` 且 `B > G`）对紫色主体天然敏感——**斑块主体色
`#8C4FD4` 恰好同时满足 R>G、B>G**。后处理必须先键控再按「与三个目标色最近邻」
量化斑块像素，不能把量化与去洋红边的顺序颠倒，否则紫色主体会被误伤成主色以外的颜色。

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha（全部 255）
- [ ] 非斑块区域与 `stone.png` **逐像素完全一致**
- [ ] 斑块像素占比 12% – 20%，斑块 4–7 个互不相连，不触碰最外 2 圈
- [ ] 斑块像素全部量化到上表 3 个紫罗兰色值，无偏粉/偏蓝
- [ ] 全图找不到洋红 `#FF00FF` 残留
