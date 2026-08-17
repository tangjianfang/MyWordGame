# M11-U13 地图边框（ui-map-frame）

## 用途

milestone-11 图鉴/地图界面：地图画幅的边框，九宫格拉伸适配不同窗口尺寸。
**内部完全挖空**（键控为透明），地形图内容在框底下直接透出——
这点与 `panel`（U-06，不透明内面）相反。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素（与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（走九宫格拉伸） |
| Alpha | **有**，仅 0 或 255（框外与框内全部键控透明） |
| 输出数量 | 1 张：`ui-map-frame.png` |

### 九宫格切分

四边边界均为 **6 像素**。四角 6 × 6 区域不拉伸；
四条边的中段只沿边方向拉伸；中间 20 × 20 挖空区必须是纯洋红（键控后全透明）。

## Alpha 的产出方式

边框内部挖空区与画布外全部画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 | `#17140F` |
| 边框亮边（上/左 1px） | `#D9C89A` |
| 边框主色（羊皮纸木框） | `#B98D57` |
| 边框暗边（下/右 1px） | `#8A6741` |
| 四角铜钉 | `#8B7355` |
| 背景键控色 | `#FF00FF` |

羊皮纸地图的暖木色系，铜钉与图鉴徽章金边（`#8B7355`）同色呼应。

## 视觉描述

由外向内：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 亮边 | 1 像素 | 上/左 `#D9C89A` |
| 3. 边框主体 | 2 像素 | `#B98D57` |
| 4. 暗边 | 1 像素 | 下/右 `#8A6741` |
| 5. 内描边 | 1 像素 | `#17140F` |
| 6. 挖空区 | 中间 20×20 | 洋红（键控为透明） |

四角 6 × 6 区域内各钉一枚 2 × 2 铜钉 `#8B7355`（落在边框主体上）。

## AI 提示词

```
A pixel art treasure map frame border for a retro game UI, 1024x1024, designed
to be downscaled to 32x32 pixel art. Square, front view, flat.

Style: chunky pixel art UI, flat shading, no perspective, no gradient, no texture.

Content: a plain square border frame around a completely hollow center. From
outside in: a one-pixel near-black outline #17140F, a one-pixel light parchment
line #D9C89A along the top and left, a two-pixel parchment-wood border band
#B98D57, a one-pixel darker wood line #8A6741 along the bottom and right, and a
one-pixel near-black inner rim #17140F. On each of the four corners of the border
band sits one single two-by-two brass pin #8B7355. The border thickness is
perfectly uniform on all four sides.

Both the hollow center of the frame and everything outside the frame are flat
pure magenta #FF00FF, fully saturated, hard edges, no anti-aliasing.

Color palette strictly limited to: #17140F, #D9C89A, #B98D57, #8A6741, #8B7355.

No compass, no map drawings, no terrain, no text, no symbols, no ornaments,
no glow, no bevel, no 3D render, no watermark.
```

## 负面提示词

```
compass, terrain, map drawings, text, symbols, ornaments, scrollwork, glow,
bevel, 3D, gradient, texture, watermark
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：全部洋红（含中心挖空区）Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 5 个色值
4. **纯色化边框中段**：四条边 6..25 的中段拉直为纯色带（可被拉伸）
5. 确认中间 20 × 20（第 6..25 列 × 行）为完全透明
6. 九宫格拉伸测试：拉到 256 × 192 后边框宽度不变

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 中间 20 × 20 区域 Alpha 全为 0
- [ ] 边框四边总厚度一致（6 像素），四角铜钉四角对称
- [ ] 九宫格拉伸到 256 × 192 后边框不变形、无接缝
- [ ] 透明区叠在地图渲染上，地图内容完整透出
