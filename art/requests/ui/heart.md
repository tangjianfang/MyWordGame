# U-04 生命值心形（满 / 半 / 空）

一次需求包含**三张贴图**：`heart-full`、`heart-half`、`heart-empty`。
三者的外轮廓必须**逐像素完全一致**，只有内部填充不同——否则血量变化时心形会「跳动」。

## 用途

屏幕左下角、热键栏正上方的血量条，10 颗心 = 20 点血。
这是小孩最直观理解「我快死了」的信号，所以**红色必须够刺眼**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 各 24 × 24 像素 |
| 生成尺寸 | 各 768 × 768（再降采样，24 的 32 倍） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 颜色数 | 4 – 6 色（不含透明） |

## Alpha 的产出方式

同方块的镂空规则：生成时背景画成纯洋红 `#FF00FF`，后处理键控为透明。
**心形之外的所有区域都必须是洋红**。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边（黑） | `#1A0A0A` |
| 红色阴影 | `#8C1B1B` |
| 红色主色 | `#D42B2B` |
| 红色高光 | `#F45C5C` |
| 空心底色 | `#3A2020` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

### 共同轮廓

经典的像素心形：顶部两个圆弧凸起，底部收成尖角。
外面包一圈 **2 像素的黑色描边** `#1A0A0A`——描边是必须的，
因为血量条会叠在任意场景上，没有描边在浅色天空前会看不清。

心形占满 24×24 的中央区域，四周留 2 像素空白（洋红）。

### `heart-full`

内部完全填充：主色 `#D42B2B`，下缘用 `#8C1B1B` 压暗，
左上方一个 3×2 像素的 `#F45C5C` 高光块。

### `heart-half`

**左半边**（第 0–11 列）与 `heart-full` 完全相同；
**右半边**（第 12–23 列）填充 `#3A2020` 暗底。
分界是一条垂直的硬边，不做过渡。

### `heart-empty`

内部全部填充 `#3A2020`，保留黑色描边，**不保留高光**。

## AI 提示词

由于三张要求逐像素对齐，**推荐直接手绘或由脚本生成**，AI 只用来产出 `heart-full`，
另外两张由 `heart-full` 派生（复制后替换内部像素），这样对齐天然成立。

`heart-full` 的提示词：

```
A pixel art heart icon for a retro video game health bar, 768x768, designed to be
downscaled to 24x24 pixel art.

Style: flat shading, chunky pixel art, front view, symmetrical, no perspective.

Content: a classic pixel heart shape with two rounded lobes on top and a pointed
bottom. The heart is filled with solid red, darker red along the lower edge, and one
small bright red highlight block in the upper-left lobe. The heart is surrounded by a
thick solid black outline of even width on all sides.

Color palette strictly limited to: #1A0A0A (outline), #8C1B1B, #D42B2B, #F45C5C.

The background outside the black outline is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing. The heart is centered with a small even
magenta margin on all four sides.

No gradient, no glow, no bloom, no drop shadow, no sparkle, no 3D render, no bevel,
no glossy plastic look, no text, no watermark.
```

## 后处理

1. `heart-full` 最近邻降采样到 24 × 24
2. 键控：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. 去洋红边，量化到 4 个色值
4. **手工整形**：确认心形左右完全对称（第 N 列与第 23−N 列镜像相等），
   描边宽度处处为 2 像素
5. **派生**：复制 `heart-full`，把第 12–23 列的非描边像素替换为 `#3A2020` → `heart-half`
6. **派生**：复制 `heart-full`，把所有非描边像素替换为 `#3A2020` → `heart-empty`

## 验收标准

- [ ] 三张尺寸均恰为 24 × 24
- [ ] 三张的 Alpha 通道**逐像素完全一致**
- [ ] 三张的描边像素位置**逐像素完全一致**
- [ ] `heart-full` 左右镜像对称
- [ ] 描边宽度处处 ≥ 2 像素，无一处露白
- [ ] 把三张分别叠在纯白、纯黑、天蓝三种背景上，轮廓都清晰可辨
- [ ] 10 颗 `heart-full` 横排后，整条血量条读起来是「满血」而不是一排红点
