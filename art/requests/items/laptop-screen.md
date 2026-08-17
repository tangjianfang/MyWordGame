# 笔记本/黑客电脑屏幕贴图（laptop-screen）

> 程序占位，待正式美术替换。

笔记本方块（`laptop_block`）与黑客电脑方块（`hacker_pc_block`）的顶面贴图。
**视频背景专用** —— 实际运行时由 `VideoScreenSystem` 把这个贴图槽的共享材质
`mainTexture` 替换为 VideoPlayer 的 RenderTexture（mp4 缺失时回退此占位）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否（屏幕面不平铺） |
| Alpha | 否（整片实心深蓝底） |
| 抗锯齿 | 否 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 深蓝底 | `#0B1E3A` |
| 绿主 | `#3FBB5A` |
| 绿亮 | `#7FE89A` |
| 绿高亮 | `#C8FACC` |

## 视觉描述

- 整片深蓝底 `#0B1E3A`，满帧
- 绿色字符（`#3FBB5A`/`#7FE89A`/`#C8FACC`）竖条排列，模拟代码雨 / 终端字符
- 无边框、无笔记本外壳、无键盘——只画屏幕内容
- 占位时一眼能看出是「电脑屏幕」即可，正式美术替换后会接 mp4 视频

## AI 提示词（正式版）

```
Retro pixel computer screen playing green code characters on dark navy background, 32x32 pixel art with no anti-aliasing. Show vertical columns of small green pixel characters (like terminal text or code rain) on flat dark navy #0B1E3A background. Only green tones #3FBB5A #7FE89A #C8FACC for characters. No bezel, no frame, no keyboard, no desk. Seamless not required (the screen face is a single small block face, not tiled). Magenta is forbidden.
```

## 验收标准

- 32 × 32 像素，无抗锯齿
- 主色为深蓝底 + 绿色字符
- 无洋红（magenta）色块
- 单一面、不平铺（屏幕面）