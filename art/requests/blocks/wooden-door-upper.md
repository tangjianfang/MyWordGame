# 木门上半（wooden-door-upper）

## 用途

`wooden-door` 方块上半部分的正面贴图（milestone-11 第 1 波注册）。门的上下
两半各一张竖向贴图，拼起来是一扇 1×2 的门。上半带两格透光窗洞，窗洞用
洋红键控为透明（Alpha Test 渲染，与树叶同管线）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（单面专用贴图） |
| Alpha | 有，仅 0 或 255，**不允许中间值** |
| 透明面积（窗洞） | 占全图 **10% – 20%** |
| 颜色数 | 5 色（不含透明） |

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 门框/窗框（最深） | `#4E3826` |
| 板缝阴影木 | `#6B4E2E` |
| 门板主色 | `#8A6741` |
| 门板亮部 | `#9C7549` |
| 门板高光 | `#B98D57` |
| 键控色 | `#FF00FF`（后处理删除） |

门板木色即 `planks` 的木板黄系（去掉了最深一档板缝色之外的杂色），
「木板做的门」一目了然。

## 视觉描述

- 四周 2 像素门框 `#4E3826`
- 内部竖向拼 2 条门板，中间 1 像素竖缝 `#6B4E2E`
- 上部（第 4–16 行）左右各一个 **4×4 像素窗洞**，窗洞画纯洋红 `#FF00FF`
  （键控为透明），四周 1 像素 `#4E3826` 窗框包边
- 下半是实心门板，带 1–2 道 1 像素竖向木纹
- 不画门把手（把手在下半）

## AI 提示词

```
A pixel art texture of the upper half of a wooden door, flat front view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination.

Content: a door panel made of 2 vertical wooden boards inside a 2-pixel dark
wooden frame around all four sides. In the upper part sit two square windows,
each about 4x4 final pixels, side by side. Each window is filled entirely with
flat pure magenta #FF00FF, fully saturated, hard edges, surrounded by a
1-pixel dark wooden window frame. The rest of the door is solid wood with
subtle vertical grain. No handle, no hinges, no carved pattern.

Wood palette strictly limited to: #4E3826, #6B4E2E, #8A6741, #9C7549, #B98D57.

This texture does not tile. Fill the whole square canvas edge to edge. Only the
two window squares are magenta; everything else is opaque wood, no background.

No outer frame around the canvas, no handle, no knob, no hinges, no metal, no
glass reflection, no text, no watermark, no vignette, no drop shadow, no
gradient, no anti-aliasing.
```

## 负面提示词

```
gradient, vignette, drop shadow, handle, knob, hinges, metal, glass reflection,
highlight, border frame, outline, text, watermark, blur, 3D render, perspective,
anti-aliasing, more than two windows
```

## 后处理

1. 最近邻降采样到 32 × 32
2. **键控**：洋红占比 > 50% 的像素 Alpha = 0，其余 Alpha = 255
3. **去洋红边**：不透明像素中 R > G 且 B > G 的判为残留，替换为相邻木色
4. 量化到上表 5 个色值
5. 核对窗洞恰为两个 4×4、位置左右对称，透明占比 10% – 20%

## 验收标准

- [ ] 尺寸恰为 32 × 32，32 位 PNG 带 Alpha 通道
- [ ] Alpha 只有 0 和 255 两种，透明占比 10% – 20%
- [ ] 恰好两个 4×4 窗洞，各带 1 像素深色窗框
- [ ] 无残留洋红与偏紫边
- [ ] 与 `planks.png` 并排，木色一致，读作「同一批木料」
- [ ] 与 `wooden-door-lower.png` 竖向拼合，门框宽度、竖缝位置对齐
