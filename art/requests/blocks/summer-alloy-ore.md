# M10-A 夏季合金矿石

## 用途

milestone-10 矿物进阶（spec §1）：夏季合金是第三档材料（铁镐可挖、y<24 低稀有度），
装备提供移速加成。贴图名 `summer-alloy-ore`，方块 id `summer_alloy_ore`。

> 当前 `Assets/StreamingAssets/blocks/textures/summer-alloy-ore.png` 是**程序生成的占位图**
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
| 深（暗边） | `#1E6B3A` |
| 主 | `#3FA35B` |
| 亮（高光） | `#8FE39A` |

「夏季」取盛夏草木的**翡翠绿**。必须与既有四矿拉开：
钻石是青（`#4CC6C4` 偏蓝绿）、金是明黄、铁是暖褐、煤是黑。
并排看时不允许被认成钻石。

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

These blobs are vivid summer emerald green: #1E6B3A, #3FA35B, #8FE39A only.
Fresh foliage green, NOT cyan, NOT teal, NOT blue-green, NOT turquoise — think
summer leaves, clearly distinct from any blue-teal shade.

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
checkerboard, border, cyan tint, blue tint, teal, turquoise, tiny dots, small gems
```

## 后处理

同 `ores.md` 的七步（降采样 → 键控 → 去洋红边 → 量化 3 色 → 叠 `stone.png` →
查外圈 → 与既有四矿逐像素差分，差异只在斑块处）。

## 验收标准

- [ ] 尺寸恰为 32 × 32，无 Alpha（全部 255）
- [ ] 非斑块区域与 `stone.png` **逐像素完全一致**
- [ ] 斑块像素占比 12% – 20%，斑块 4–7 个互不相连，不触碰最外 2 圈
- [ ] 无任何偏紫像素
- [ ] 与 `diamond-ore.png` 并排看，1 秒内说出哪张更绿（夏合金）哪张更蓝（钻石）
