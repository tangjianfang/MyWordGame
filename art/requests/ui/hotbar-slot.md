# U-02 热键栏格子

## 用途

屏幕底部物品栏的单个格子边框。**九宫格拉伸**使用，需要 9 个格子横向排列成热键栏。
另需一个高亮版本标示当前选中的格子。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 48 × 48 像素 |
| 生成尺寸 | 512 × 512（再降采样） |
| 平铺 | 否 |
| Alpha | **需要**，内部半透明 |
| 输出数量 | **2 张**：`hotbar-slot.png`、`hotbar-slot-selected.png` |

### 九宫格边距

| 边 | 像素 |
| --- | --- |
| 上 / 下 / 左 / 右 | 各 6 像素 |

即四角 6 × 6 区域不拉伸，中间部分可自由拉伸。**边框粗细必须均匀为 3 像素**，
否则拉伸后四边粗细不一。

## 构图

一个**方形边框**，内部为半透明深色底：

| 区域 | 内容 |
| --- | --- |
| 外描边 | 1 像素 `#1A1A1A`，Alpha 255 |
| 边框主体 | 2 像素 `#8A8A8A`，Alpha 255 |
| 内部填充 | `#000000`，Alpha 110（半透明黑，让底下的世界隐约可见） |

选中版本 `hotbar-slot-selected.png` 的差异：

- 边框主体改为 `#FFFFFF`，Alpha 255
- 边框主体加粗到 3 像素
- 内部填充 Alpha 改为 140

## AI 提示词

### 普通格子

```
A pixel art square inventory slot frame for a game HUD, 512x512, designed to be
downscaled to 48x48 pixel art.

Shape: a simple square border frame centered in the image, occupying nearly the
entire canvas with only a tiny margin. The border is a uniform thickness on all
four sides. The inside of the square is a flat semi-transparent dark fill.

Style: crisp hard-edged pixel art, no anti-aliasing, no blur, no glow, no bevel,
no 3D effect, completely flat.

Colors: the border is medium gray #8A8A8A with a 1-pixel dark #1A1A1A outline on
its outer edge. The interior fill is black at about 43% opacity so the game world
shows through faintly.

The border thickness must be perfectly even on all four sides and the corners must
be clean right angles.

No rounded corners, no bevel, no emboss, no gradient, no glow, no shadow,
no icons inside, no numbers, no text, no watermark, no decoration.
```

### 选中格子

同上，但把这两句替换：

```
Colors: the border is pure white #FFFFFF with a 1-pixel dark #1A1A1A outline on
its outer edge. The border is noticeably thicker than a normal slot. The interior
fill is black at about 55% opacity.
```

## 负面提示词

```
rounded corners, bevel, emboss, 3D, gradient, glow, bloom, drop shadow, inner shadow,
icons, items, numbers, text, watermark, decoration, ornament, uneven border,
anti-aliasing, blur, opaque background, colored border
```

## 后处理

1. 最近邻降采样到 48 × 48
2. 量化到规定色值
3. **逐边核对边框粗细**：普通版 2 像素、选中版 3 像素，四边必须完全一致
4. 内部填充 Alpha 精确设为 110（普通）/ 140（选中）
5. 外部若有留白，Alpha 置 0
6. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 48 × 48
- [ ] 四边边框粗细完全相同（水平、垂直翻转后与原图一致）
- [ ] 四角为直角，无圆角、无斜切
- [ ] 内部填充 Alpha 恰为 110（普通）/ 140（选中）
- [ ] 边框与描边 Alpha 恰为 255
- [ ] 两版并排时，选中版明显更醒目
- [ ] 9 个格子横向排列后，间隔均匀、边框对齐
