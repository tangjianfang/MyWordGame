# I-45 工作台（物品图标）

## 用途

热键栏中的工作台物品图标（物品 id 为 `crafting_table`，texture 字段为
`crafting_table-top`——所以最终 PNG 文件名是 `crafting_table-top.png`）。
整体呈现工作台的俯视缩略。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 16 × 16 像素 |
| 生成尺寸 | 512 × 512（再降采样） |
| 平铺 | 否 |
| Alpha | **有**（仅 0 / 255） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 板缝 | `#6B4E2E` |
| 板主 | `#8A6741` |
| 板亮 | `#9C7549` |
| 工具格 | `#1A1A1A` |

## AI 提示词

```
A single crafting table icon for an inventory slot, 512x512 pixel art
designed to be downscaled to 16x16. A small top-down view of a wooden
crafting table showing a 3x3 grid of dark-outlined square slots in the
center. The slots are filled with medium wood tone, the outer 2-pixel
border is a darker wood. Black 1-pixel outline around the whole table.
Transparent background (pure magenta #FF00FF keyout color).

Color palette strictly: #6B4E2E, #8A6741, #9C7549, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
No tool icons inside the slots.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
