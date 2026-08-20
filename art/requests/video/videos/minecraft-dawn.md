# V-04 · Minecraft Dawn 矿道破晓（m11 启动菜单破晓版）

### minecraft-dawn

视频源：MiniMax-Hailuo-2.3 文生视频。模型档位 6s 1080P。

## 设计意图

**地下 → 出口的破晓**：画面以第一人称视角从黑乎乎的矿道走出，地平线
撞破光线，6 秒尾端玩家站在阳光下俯瞰森林。

平行创作：和 `aurora-burst`（夜间极光）形成昼夜轴，和 `aurora-storm`
（夜间风暴）形成"出洞见天 vs 仍在暴风中"的关系。三者都是启动菜单可选项。

## 关键帧时间轴（6 秒）

| 时间 | 画面 |
| --- | --- |
| 0.0s | 第一人称相机在一片漆黑的方块矿道里，正前方是出口（一线天白） |
| 1.0s | 往出口走，矿道壁两侧是圆石+铁矿石纹理，远方地平线开始透光 |
| 2.5s | 踏出矿道地面，脚踩草地+圆石，相机略仰 |
| 3.5s | 太阳冲破东方地平线，金光铺满整片森林顶 |
| 4.5s | 俯视拉远：玩家站在悬崖边，下方是一片森林+河流+远处的雪顶山 |
| 5.5s | 镜头再往后，远处第二座山浮现，云海推近 |
| 6.0s | 全部就位，留 0.5s 让画面静止 |

## AI 提示词

```
First-person cinematic emergence from darkness into dawn. The camera starts
inside a blocky voxel mine shaft made of cobblestone and iron ore blocks,
pitch black except for a single sliver of white light at the far end. The
camera walks forward down the shaft, walls passing on both sides. The
camera emerges from the mine into a voxel Minecraft-style landscape at
sunrise. The sun breaks over the eastern horizon, flooding golden light
across a vast blocky forest canopy beneath the camera. The camera lifts
upward and backward to reveal a sweeping panoramic view: forest, river,
snow-capped mountains in the distance, cloud sea drifting between peaks.
The camera holds the wide view at the end. Voxel blocky style throughout.
No text, no watermark, no UI elements. No night sky.
```

## 后处理

同 menu-bg 流程：mp4 → ffmpeg 转码（24fps / 1920×1080）→ 末 0.6s
无缝循环 fade → 写入 `Assets/StreamingAssets/video/minecraft-dawn.mp4`。

## 验收标准

1. 0.0–1.5s 必须是黑色矿道，不能出现蓝天/草地/光
2. 2.0s 起必须是户外，且必须是日出色温（暖金色，不是正午白光）
3. 4.0s 后必须出现至少 2 个不同的远方景物（森林+山 或 森林+河）
4. 无文字、无水印、无 UI 元素
5. 无缝循环 fade 后首尾帧 RMSE ≤ 0.15
6. 文件大小 < 30MB

## 入库路径

`Assets/StreamingAssets/video/minecraft-dawn.mp4`。
