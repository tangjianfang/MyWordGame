# U-05 按钮三态（常态 / 悬停 / 按下）

一次需求包含**三张贴图**：`button-normal`、`button-hover`、`button-pressed`。

## 用途

主菜单、暂停菜单、设置界面的所有按钮。

按钮宽度不固定（「开始游戏」和「退出」长度不同），所以做成**九宫格（9-slice）拉伸**：
四个角不拉伸，上下边横向拉伸，左右边纵向拉伸，中间双向拉伸。

## 九宫格切分（必须严格遵守）

贴图 64 × 24，边界为：

| 方向 | 切分 |
| --- | --- |
| 左边界 | 8 像素 |
| 右边界 | 8 像素 |
| 上边界 | 8 像素 |
| 下边界 | 8 像素 |

即中间可拉伸区是第 8–55 列、第 8–15 行。
**中间区必须是纯色或水平/垂直方向可无限重复的图案**，否则拉伸后会变形。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 各 64 × 24 像素 |
| 生成尺寸 | 各 1024 × 384（再降采样） |
| 平铺 | 否（走九宫格） |
| Alpha | 有，仅 0 或 255（只有四个圆角是透明的） |
| 颜色数 | 各 5 – 7 色 |

## 调色板（严格使用）

| 用途 | 常态 | 悬停 | 按下 |
| --- | --- | --- | --- |
| 外描边 | `#1E1A16` | `#1E1A16` | `#1E1A16` |
| 上高光边 | `#9E9184` | `#BFB3A4` | `#6E655C` |
| 面主色 | `#7A6E62` | `#96897A` | `#5C5349` |
| 下阴影边 | `#4E463D` | `#6A6055` | `#3E372F` |
| 内阴影 | — | — | `#2E2924` |

三态的关系：**悬停 = 常态整体提亮；按下 = 常态整体压暗 + 高光与阴影上下对调**
（做出「凹下去」的错觉）。

## 视觉描述

### 共同结构（三态完全一致的几何）

- 1 像素黑色外描边，四角各切掉 1 个像素做圆角
- 描边内是 1 像素的立体边：常态与悬停时**上边和左边亮、下边和右边暗**
- 中央是纯色面
- **不要在贴图里画文字**，文字由引擎的 TextMeshPro 叠加

### `button-pressed` 的差异

立体边上下对调：**上边和左边暗、下边和右边亮**，
并在上边内侧多加 1 像素的 `#2E2924` 内阴影。

整体色比常态暗一档，让「按下去了」一眼可见。

## AI 提示词

三态之间必须逐像素对齐，**推荐手绘或脚本生成**。若用 AI，只生成 `button-normal`，
另外两态由它派生（整体调亮 / 整体调暗并翻转立体边）。

`button-normal` 的提示词：

```
A pixel art UI button plate for a retro voxel game, 1024x384, designed to be
downscaled to 64x24 pixel art. Wide horizontal rectangle, front view, flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient.

Content: a rectangular stone-gray button plate with a one-pixel black outline and
slightly clipped corners. Inside the outline there is a one-pixel bevel: the top and
left inner edges are light, the bottom and right inner edges are dark. The large
center area is a single flat mid-gray-brown color with no texture and no gradient.

Color palette strictly limited to: #1E1A16 (outline), #9E9184 (top bevel),
#7A6E62 (face), #4E463D (bottom bevel).

The area outside the button, including the four clipped corners, is flat pure magenta
#FF00FF, fully saturated, hard edges, no anti-aliasing.

The button must fill the image edge to edge apart from the clipped corners, with no
margin.

No text, no letters, no numbers, no icon, no glow, no bloom, no drop shadow,
no rounded gradient, no glossy plastic, no 3D render, no perspective, no watermark.
```

## 后处理

1. 最近邻降采样到 64 × 24
2. 键控洋红为透明，去洋红边
3. 量化到调色板
4. **拉直边界**：确认外描边是精确的 1 像素直线，立体边是精确的 1 像素
5. **验证中间区可拉伸**：把第 8–55 列、第 8–15 行的区域取出，
   确认它是纯色（或至少每一行、每一列都完全一致）
6. 派生 `button-hover`：把面主色与两条立体边替换为悬停列的色值
7. 派生 `button-pressed`：替换为按下列的色值，并把上下两条立体边的颜色互换，
   在上边内侧补一行 `#2E2924`

## 验收标准

- [ ] 三张尺寸均恰为 64 × 24
- [ ] 三张的 Alpha 通道逐像素一致（只有四角 8 个像素透明）
- [ ] 九宫格中间区（8..55 列 × 8..15 行）为纯色
- [ ] 外描边为闭合的 1 像素黑线
- [ ] 把 `button-normal` 用九宫格拉伸到 200 × 24 和 64 × 48，边角不变形
- [ ] 三态快速切换预览时，只有明暗变化，**几何不抖动**
- [ ] `button-pressed` 与 `button-normal` 并排，能直观看出「凹进去了」
