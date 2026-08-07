# B-14 矿石四件套（煤 / 铁 / 金 / 钻石）

一次需求包含**四张贴图**：`coal-ore`、`iron-ore`、`gold-ore`、`diamond-ore`。
它们共用同一张石头底图，只有矿物斑块的颜色与形状不同。

## 用途

洞穴探索的核心奖励。小孩挖到第一颗钻石的那一刻，就是这个项目值不值得做的答案。

## 生产方式与其它贴图不同（务必先读）

矿石**不要整张交给 AI 生成**。原因：AI 每次生成的石头底纹都不一样，四张矿石放在
同一片石壁上会出现四种不同的灰底，一眼露馅。

正确做法是**两层合成**：

1. **底层**：直接复用已验收的 `Assets/StreamingAssets/blocks/textures/stone.png`，
   一个像素都不改
2. **上层**：只让 AI 生成**矿物斑块层**（洋红背景 + 矿物斑块），键控后叠到底层上

这样四张矿石的石头部分**逐像素完全相同**，只有斑块不同。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 各 32 × 32 像素 |
| 斑块层生成尺寸 | 各 1024 × 1024（再降采样） |
| 平铺 | 底层继承 `stone.png` 的四边无缝；斑块层**不跨越边缘** |
| Alpha | 最终成品无 Alpha，完全不透明 |
| 斑块面积 | 占全图 **12% – 20%** |

**斑块不得触碰图片的最外 2 圈像素**。原因：矿石在世界里通常单独嵌在石头中，
斑块贴边会导致相邻的两块矿石斑块粘连成一片，看起来像一大块矿脉。

## 各矿物调色板（严格使用）

| 矿物 | 深 | 主 | 亮 |
| --- | --- | --- | --- |
| 煤 `coal-ore` | `#0F0F0F` | `#1F1F1F` | `#3A3A3A` |
| 铁 `iron-ore` | `#8A6A4C` | `#B9906B` | `#D8B896` |
| 金 `gold-ore` | `#A87322` | `#DCAE3A` | `#F7DA7A` |
| 钻石 `diamond-ore` | `#1E7C7C` | `#4CC6C4` | `#A8F2EF` |

煤要黑得下去但不能纯黑（纯黑在暗处会糊成洞）；
铁是暖褐不是橙；金要黄得亮但不刺眼；钻石是青绿不是天蓝。

## 视觉描述（四张通用）

每张图有 **4–7 个矿物斑块**，散布在石头底上。

- 单个斑块 3–6 像素，形状是不规则的圆角块，**不要画成菱形宝石图标**
- 每个斑块内部有 2–3 级明暗：暗边 + 主色 + 1 像素高光点，高光点位置在斑块左上
- 斑块大小要有差异，位置随机但**分布均匀**，不要挤在一角
- 斑块之间保持至少 2 像素的石头间隔，不要连成片
- 斑块不得触碰最外 2 圈像素
- 钻石可以比其它三种少 1–2 个斑块（稀有感），但不能只有 2 个

## AI 提示词（斑块层，四张分别替换颜色）

以煤为例，其余三种把 `COLORS` 一行换成对应调色板即可：

```
A pixel art layer of scattered ore mineral deposits on a flat magenta background,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game texture, flat shading, no perspective, even illumination.

Content: 4 to 7 irregular rounded mineral blobs, LARGE and prominent, each spanning
roughly 150 to 250 pixels across (about one sixth to one quarter of the image
width) — never small dots, never tiny gems. Blobs are evenly scattered with clear
separation between them. Each blob has a darker rim, a solid main color, and a
single small highlight dot in its upper-left area. Blobs are organic rounded
shapes, never diamond icons, never faceted gems, never crystals, never metallic.

COLOR_DESC

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing between the magenta and the blobs. Magenta covers about 65 percent of
the image, blobs cover about 35 percent.

The blobs must stay away from the outer border; leave a magenta margin of at least
8 percent of the image width on all four sides.

No stone texture, no rock background, no gradient, no vignette, no glow, no sparkle,
no lens flare, no faceted gem, no diamond shape, no metallic shine, no text,
no watermark, no 3D render, no perspective.
```

四张的 `COLOR_DESC` 行分别为：

- 煤：`These blobs are pure dark charcoal black: #0F0F0F, #1F1F1F, #3A3A3A only. They must NOT be red, NOT brown, NOT warm-toned — think black coal lumps, absolutely no reddish or orange tint anywhere.`
- 铁：`These blobs are warm rusty iron brown: #8A6A4C, #B9906B, #D8B896 only. Muted earthy brown, not orange, not red, not gray.`
- 金：`These blobs are rich metallic gold yellow: #A87322, #DCAE3A, #F7DA7A only. Warm bright yellow-gold, not pale yellow, not brown.`
- 钻石：`These blobs are cyan teal diamond: #1E7C7C, #4CC6C4, #A8F2EF only. Cool cyan-green, not blue, not purple.`

## 负面提示词

```
stone background, rock texture, gradient, vignette, drop shadow, glow, sparkle,
lens flare, shine, faceted gem, diamond shape, crystal cluster, jewel icon, minecraft
logo, text, watermark, signature, blur, depth of field, 3D render, perspective,
metallic reflection, transparent background, alpha channel, checkerboard, border,
red tint, orange tint, reddish brown, tiny dots, small gems
```

## 后处理

1. 斑块层最近邻降采样到 32 × 32
2. **键控**：洋红占比 > 50% 的像素设为透明
3. **去洋红边**：剩余像素中偏紫的过渡色替换为该矿物的主色
4. 量化斑块像素到该矿物的 3 个色值
5. **合成**：把斑块层叠到 `stone.png` 之上，输出为不透明 PNG
6. 检查斑块未触碰最外 2 圈像素，触碰了就把该斑块整体内移
7. 四张成品互相做**逐像素差分**，差异区域应当只出现在斑块处

## 验收标准（四张各自检查）

- [ ] 尺寸恰为 32 × 32，无 Alpha 通道（全部 255）
- [ ] 四张的非斑块区域与 `stone.png` **逐像素完全一致**
- [ ] 斑块像素占比在 12% – 20%
- [ ] 最外 2 圈像素中没有任何斑块像素
- [ ] 斑块数量 4–7 个，无两个斑块相连
- [ ] 无任何偏紫像素
- [ ] 四张并排，能在 1 秒内说出哪个是铁哪个是金（这两个最容易混）
- [ ] 在模拟的低亮度（乘 0.3）下预览，煤矿石仍能与纯石头区分开
