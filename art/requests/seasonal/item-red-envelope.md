# SEASON-09 红包（物品图标）

## 用途

春节活动的**红包物品图标**：任务奖励/互赠道具，进背包要立刻有图。
32×32 物品图标，洋红背景键控（同 `items/stick.md` 范式）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 颜色数 | ≤ 6 色（不含洋红） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 红包主色 | `#C43C3C` |
| 封口暗红 | `#A83232` |
| 侧面亮红 | `#E05252` |
| 金封印 | `#DCAE3A` `#F7DA7A` |
| 描边 | `#241A11` |

与 `lantern-spring` / `logo-spring-festival` 共用节日红金。

## 视觉描述

- 一只竖放的红纸信封，正视略带 15° 侧转：正面主红，右缘一线亮红翻边
- 上 1/3 是向下翻的**暗红封口盖**，盖沿一条细亮线
- 封口盖中央一枚**圆形金色蜡封**
- 底边一条细金边线
- 整体 1 像素深描边，主体占画幅约 80%，居中
- 不画铜钱、不画字（包括「福」之类的汉字——AI 画不对）

## AI 提示词

```
A single red envelope icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A thick red paper envelope seen from
the front with a slight 15 degree turn: main red face, one thin bright
red fold line along the right edge, a darker red flap folded down over
the top third with a thin light line along its lower edge, one round
gold wax seal centered on the flap, one thin gold edge line along the
bottom. A dark 1-pixel outline around the whole envelope. The envelope
fills about 80 percent of the frame, centered, with pure magenta #FF00FF
keyout background on all sides, fully saturated, hard edges, no
anti-aliasing.

Color palette strictly: #C43C3C, #A83232, #E05252, #DCAE3A, #F7DA7A,
#241A11 outline only.

No text, no letters, no Chinese characters, no coin, no watermark, no
shadow, no glow. Hard pixel edges.
```

## 负面提示词

```
text, letters, Chinese characters, coin, watermark, shadow, glow,
gradient, 3D render, anti-aliasing, transparent background, checkerboard
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明，去洋红边
3. 量化到上表 6 个色值

## 验收标准

- [ ] 尺寸恰为 32 × 32，32 位 RGBA，Alpha 只有 0/255
- [ ] 深浅两种背景上预览无紫边
- [ ] 缩到背包槽实际大小（16×16）仍认得出是红包
- [ ] 图中不含任何文字/汉字
