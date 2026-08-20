# V-03 · Aurora Burst 极光突发（m11 启动菜单夜版）

### aurora-burst

视频源：MiniMax-Hailuo-2.3 文生视频。模型档位 6s 1080P（API 最高档）。

## 设计意图

`menu-bg` 是当前启动菜单主背景（白天草原），硬在白天曲面上容易审美疲劳。
**夜版极光突发**作为 3 个新选项之一，提供"夜间极光爆发"主题。

平行创作：和 `minecraft-dawn`（破晓）形成昼夜轴关系。和 `aurora-storm`
（暴雪之夜）形成"平静极光 vs 风暴极光"的关系。三者构成启动菜单的可选项，
在不需要任何代码改动的前提下让玩家挑喜欢的氛围。

## 关键帧时间轴（6 秒）

| 时间 | 画面 |
| --- | --- |
| 0.0s | 全黑，主角背身站在雪山顶，单手举向天 |
| 1.5s | 第一缕极光从北边升起，绿色光帘淡入 |
| 3.0s | 极光扩到天顶，绿色→紫色→蓝色彩虹带 |
| 4.5s | 极光高峰：3 道主光束同时亮起，雪地反射成青色 |
| 5.5s | 极光缓慢回落，画面保留主体剪影 |
| 6.0s | 极光余晖，主角背影伸手姿势保持 |

## AI 提示词

```
Cinematic establishing shot of a single voxel-style character standing on
a snowy mountain peak at night, seen from behind in silhouette, right arm
raised toward the sky. A massive aurora borealis erupts overhead, expanding
from the northern horizon into a vast green-purple-blue rainbow curtain that
fills the entire sky. The aurora peaks with three bright shafts of colored
light cascading down onto the snowy landscape, briefly tinting the snow
cyan-violet. The aurora then gently recedes back toward the horizon while
the character silhouette holds the same pose. The art style is voxel blocky
voxel game (Minecraft-like), cinematic, atmospheric, cinematic camera.
No text, no watermark, no UI elements. No daytime sky.
```

## 后处理

走 `tools/generate_media.py --videos` 后流程：Hailuo-2.3 输出 mp4 → ffmpeg 转码
标准化（H.264 / 24fps / 1920×1080）→ 末 0.6s 无缝循环 fade 处理（与 menu-bg
同等流程）→ 写入 `Assets/StreamingAssets/video/aurora-burst.mp4`。

## 验收标准

1. 画面视野内 6 秒全是夜空/极光/雪，没有日出/白昼
2. 主角剪影轮廓在画面中可识别（不必面部细节，背身+举起的手可辨即可）
3. 极光至少有 3 种颜色（绿/紫/蓝），不是单调单一色
4. 无文字、无水印、无 UI 元素
5. 6s 末帧 → 首帧 RMSE ≤ 0.15（让无缝循环 fade 可处理）
6. 文件大小 < 30MB（Hailuo-2.3 6s 1080P 通常 8-15MB，转码后稳定）

## 入库路径

`Assets/StreamingAssets/video/aurora-burst.mp4`，由 `TitleScreenUi` 在选
Aurora Burst 项时播放（m11 W3-3 已经预留 video slot）。
