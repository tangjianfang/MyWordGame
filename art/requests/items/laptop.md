# 笔记本电脑

## 用途

热键栏/背包中的笔记本电脑物品图标（可摆放家具/黑客玩法入口）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 壳暗 | `#443D34` |
| 壳主 | `#6B6155` |
| 壳亮 | `#8B7F6F` |
| 屏深蓝 | `#2C5893` |
| 屏亮蓝 | `#4E88CE` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角翻开态：屏盖上仰显深蓝屏面（一道浅蓝高光条），底座键盘位几粒深色键暗示
- 电脑三件套（laptop/keyboard/mouse）统一复古暖灰壳系（UI 暖灰 `#443D34`/`#6B6155`/`#8B7F6F`），与家具木色同一暖调
- 屏幕纯色不显示内容

## AI 提示词

```
A single laptop computer icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. An open laptop seen from a slight
three-quarter front angle: a warm beige-gray screen lid tilted up showing a
flat dark blue screen with one small light blue highlight bar, connected to
a wider beige base keyboard deck with a few small darker key blocks hinted
on it. Black 1-pixel outline. The empty corners are one flat solid dark
backdrop color #2A2620.

Color palette strictly: #443D34, #6B6155, #8B7F6F, #2C5893, #4E88CE,
#2A2620, #1A1A1A only.

No text, no watermark, no logo, no desktop icons, no cursor, no person, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, logo, brand, desktop icons, windows, cursor, hands, person,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 7
- 屏盖与键盘底座成翻开夹角，屏面无内容
