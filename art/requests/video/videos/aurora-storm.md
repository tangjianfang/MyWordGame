# V-05 · Aurora Storm 暴风雪之夜（m11 启动菜单暴风极光版）

### aurora-storm

视频源：MiniMax-Hailuo-2.3 文生视频。模型档位 6s 1080P。

## 设计意图

**暴风雪中的极光**：与 `aurora-burst`（平静夜空极光）形成"平静 vs 风暴"
的姐妹关系，是启动菜单可选项里最有戏剧张力的一个。

镜头是一只在方块雪原上独行的狼（剪影），远处 3 道极光穿越暴风雪，
雪粒横扫镜头，营造"恶劣天气下仍有壮丽天象"的世界感。

## 关键帧时间轴（6 秒）

| 时间 | 画面 |
| --- | --- |
| 0.0s | 黑屏，风声呼啸起，远处地平线一条青光 |
| 1.0s | 镜头进入暴风雪侧风，雪粒横扫，一只狼剪影站雪脊顶 |
| 2.5s | 第一道绿色极光从北升起，被风搅动扭曲 |
| 4.0s | 极光爆裂成紫红+翠绿双带，狼抬头 |
| 5.0s | 镜头快速推近狼脸 0.3s，然后切回全景 |
| 6.0s | 极光余晖、狼剪影、风雪不停 |

## AI 提示词

```
Cinematic voxel-blocky scene: a lone wolf silhouette standing on a snowy
ridge during a heavy blizzard at night, seen from behind at first. Strong
sideways wind drives snow across the camera. In the distant sky, an
intense aurora storm is in progress — the aurora is being whipped into
distorted twisted ribbons of green and magenta-purple light by the same
wind. The aurora suddenly bursts into vivid violet-crimson and emerald
green twin bands across the entire sky. The wolf tilts its head up
toward the aurora. Quick 0.3 second push-in toward the wolf's face,
then cut back to wide shot. The blizzard and aurora continue into the
final frame. Voxel blocky style. No text, no watermark, no UI elements.
No daytime.
```

## 后处理

同 menu-bg 流程：mp4 → ffmpeg 转码（24fps / 1920×1080）→ 末 0.6s
无缝循环 fade → 写入 `Assets/StreamingAssets/video/aurora-storm.mp4`。

## 验收标准

1. 全程是暴风雪（不能出现宁静风雪/静止雪花）
2. 至少 2 道不同颜色的极光（绿+紫红）
3. 狼剪影出现在画面中（不一定全程，5.0s 必须有脸特写一瞬）
4. 末帧 → 首帧 RMSE ≤ 0.15（无缝循环 fade 可处理）
5. 文件 < 30MB
6. 无文字、无水印、无 UI 元素

## 入库路径

`Assets/StreamingAssets/video/aurora-storm.mp4`。
