# B-13 玻璃

## 用途

透明建材。小孩造房子第一个想要的就是窗户。

玻璃与其它方块最大的不同：它的贴图**绝大部分是透明的**，只有一圈边框和几笔反光是实的。

## 为什么玻璃的边框不会导致平铺问题

`GreedyMesher` 输出的 UV 是「每格重复一次」，所以一面 5×3 的玻璃墙上，
贴图会完整重复 15 次，**每一格各自显示一圈边框**——这正是我们要的效果，
不是需要消除的接缝。因此玻璃**不要求四边无缝**，反而要求边框正好贴着 32×32 的边界。

## Alpha 的产出方式

同 B-12：生成时把透明区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | **不要求无缝**，但边框必须严格对齐图片边界 |
| Alpha | 有，仅 0 或 255 |
| 透明面积 | 占全图 **70% – 85%** |
| 颜色数 | 3 – 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 边框主色 | `#A9CBD6` |
| 边框亮部 | `#C8E2EA` |
| 反光高光 | `#E7F4F8` |
| 边框暗角 | `#8FB4C0` |
| 透明键控色 | `#FF00FF`（后处理删除） |

冷青白色，不要偏绿（会像脏玻璃），不要偏蓝到发紫。

## 视觉描述

- **边框**：沿 32×32 的四条边各画 **2 像素宽**的浅青色框，框要闭合、四角相接
- 四个角各加 1 像素的 `#8FB4C0` 暗角，让框看起来有厚度
- **反光**：在框内的透明区域里，画 **2 条斜向的细反光**（1–2 像素宽），
  从左下往右上方向，长度约 10–14 像素，位置不对称
- 反光之外的框内区域**全部是洋红**
- 不要画裂纹、不要画十字窗格、不要画水滴

## AI 提示词

```
A pixel art texture of a glass block face, flat front view, 1024x1024, designed to be
downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a thin pale cyan-white frame runs along all four edges of the square,
about 2 pixels wide at 32x32 scale, closed at the corners with slightly darker
corner pixels. Inside the frame there are two thin diagonal specular highlight
streaks running from lower-left to upper-right, of different lengths, placed
asymmetrically.

The ENTIRE remaining interior area is filled with flat pure magenta #FF00FF as a
chroma key background. The magenta is fully saturated with hard edges and no
anti-aliasing against the frame or the highlights. Magenta covers about 80 percent
of the image.

Glass color palette strictly limited to: #8FB4C0, #A9CBD6, #C8E2EA, #E7F4F8.
Cool pale cyan white, no green tint, no purple tint.

The frame must touch the outermost pixels of all four edges exactly, with no padding
and no margin.

No cracks, no window mullions, no cross bars, no water drops, no reflections of
scenery, no text, no watermark, no vignette, no drop shadow, no gradient,
no 3D render, no perspective.
```

## 负面提示词

```
cracks, broken glass, window frame with mullions, cross bars, curtains, water drops,
rain, scenery reflection, sky reflection, gradient, vignette, drop shadow, blur,
depth of field, 3D render, perspective, photorealistic, green tint, purple tint,
text, watermark, transparent background, alpha channel, checkerboard, margin, padding
```

## 后处理

1. 最近邻降采样到 32 × 32
2. **键控**：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. **去洋红边**：不透明像素中若有偏紫过渡色，替换为 `#A9CBD6`
4. **修边框**：手动确认第 0 行、第 31 行、第 0 列、第 31 列全部是不透明边框色，
   一个透明像素都不能有；框宽统一为 2 像素
5. 量化到上表 4 个色值

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 32 位 PNG，Alpha 只有 0 和 255
- [ ] 最外一圈（4 条边）全部 Alpha = 255，宽度统一 2 像素
- [ ] Alpha = 0 的像素占比在 70% – 85%
- [ ] 无任何偏紫像素
- [ ] 3×3 平铺预览中，九个格子的边框拼成整齐的网格（这是预期效果）
- [ ] 把贴图叠在深色背景上，反光能看清；叠在浅色背景上，边框不消失
