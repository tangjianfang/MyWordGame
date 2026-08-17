# 笔记本电脑

## 用途

热键栏/背包中的笔记本电脑物品图标（可摆放家具/黑客玩法入口）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 背景 | 整片纯洋红 `#FF00FF`，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 壳暗 | `#443D34` |
| 壳主 | `#6B6155` |
| 壳亮 | `#8B7F6F` |
| 屏深蓝 | `#2C5893` |
| 屏亮蓝 | `#4E88CE` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角翻开态：屏盖上仰显深蓝屏面（一道浅蓝高光条），底座键盘位几粒深色键暗示
- 电脑三件套（laptop/keyboard/mouse）统一复古暖灰壳系（UI 暖灰 `#443D34`/`#6B6155`/`#8B7F6F`），与家具木色同一暖调
- 屏幕纯色不显示内容
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single laptop computer icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. An open laptop seen from a slight
three-quarter front angle: a warm beige-gray screen lid tilted up showing a
flat dark blue screen with one small light blue highlight bar, connected to
a wider beige base keyboard deck with a few small darker key blocks hinted
on it. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #443D34, #6B6155, #8B7F6F, #2C5893, #4E88CE, #1A1A1A outline only.

No text, no watermark, no logo, no desktop icons, no cursor, no person, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, logo, brand, desktop icons, windows, cursor, hands, person,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 屏盖与键盘底座成翻开夹角，屏面无内容
