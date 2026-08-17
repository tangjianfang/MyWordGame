# 音频与视频元素设计（MiniMax 生成管线 + Unity 氛围层）

> 日期：2026-08-18 ｜ 状态：已评审通过
> 需求出处：`docs/新增一些音频元素.txt`（原文：参考图片生成调用 minimax 生成图片，也参考调用 minimax 新增一些音频元素）
> 约束出处：用户口头——**MiniMax Token Plan 视频每天只能生成 3 次**，必须做每日配额队列。

## 1. 背景与目标

游戏目前只有 4 个音（footstep/place/break 真资源 + hit 程序生成兜底），无 BGM、无生物叫声、无环境音、无任何视频元素。本设计一次性补齐氛围层：

- **音频全量铺开**：BGM（氛围钢琴风，用户选定）+ 环境循环 + 玩家事件音 + 生物叫声
- **视频两处**：极简主菜单背景循环视频 + 笔记本/黑客电脑家具方块的屏幕循环视频
- **生成管线与图片管线同构**：提示词唯一来源是 `art/requests/` 需求文件、key 从 `.env` 读、产物落 `art/incoming/`、后处理验收入库 `Assets/`

成功标准：孩子进游戏 30 秒内能说出「有音乐了」；昼夜 BGM 自动切换；打猪有叫声；开主菜单有视频背景；放下笔记本电脑方块屏幕在播视频。任何资源缺失都不阻断游戏（沿用 m3 起「音频是 nice-to-have」的错误处理模式）。

## 2. 资源清单（音频 31 条 + 视频 2 条）

### BGM ×3（music-2.6 纯音乐，氛围钢琴风，无缝循环，-18 LUFS，立体声 ogg）

> 模型名以 `/v1/music_generation` 端点 enum 为准：只有 `music-2.6` / `music-cover`（+free 变体），概览页的 music-3.0 不在该端点。该接口**无时长参数**，生成时长由模型定（一般 20–60s）——裁静音后 ≥15s 即入库，不设 ±20% 硬卡。模型名留 `MINIMAX_MUSIC_MODEL` env 覆盖（与 `MINIMAX_IMAGE_MODEL` 同模式，默认 music-2.6）。

| 名 | 内容 |
| --- | --- |
| `bgm-menu` | 主菜单主题，明亮温和的开场钢琴 |
| `bgm-day` | 白天，轻快明亮、不抓注意力 |
| `bgm-night` | 夜晚，低沉安静、略带不安 |

### 环境循环 ×3（30–60s 无缝循环，-18 LUFS，立体声 ogg）

| 名 | 内容 |
| --- | --- |
| `amb-birds` | 白天鸟鸣 + 微风 |
| `amb-crickets` | 夜晚虫鸣 |
| `amb-cave` | 洞穴滴水 + 空旷风 |

### 玩家事件音 ×11（0.5–2s，-14 LUFS，单声道 ogg）

`eat` / `hurt` / `die` / `pickup` / `craft` / `door-open` / `door-close` / `hoe-till` / `plant` / `harvest` / `tool-break`

> 无 `levelup`（经验只累计显示、没有升级事件）、无 `chest-open`（箱子右键目前是 no-op，`BlockInteraction.cs:258` 注释「UI 第 2 波接」——箱子 UI 落地时再补这条音）。文件名一律连字符（`door-open.ogg`），与图片需求命名同规。
>
> **SFX 来源策略（music-2.6 是歌曲模型，产不出 1–2s 拟声）**：事件音先试「AI 生成器乐短素材 → ffmpeg 裁剪出目标段」，裁不出合格段（时长 0.3–3s 内、无突兀旋律）则用 **ffmpeg 确定性程序合成**兜底——每条事件音在需求文件里声明合成模板（thud/pop/click/chirp/swoosh 参数化），脚本能零 API 复现。生物叫声不走程序合成（合成动物叫难听），AI 失败则该生物回退 generic 叫声（Unity 侧已有回退链）。

### 生物叫声 ×14

- 首批专属 6 种（`MobKind` 枚举名转小写做文件名）：`pig` / `cow` / `chicken` / `zombie` / `villager` / `sheep`，每种 2 条（`<mob>-idle` + `<mob>-hurt`，连字符）= 12 条，-14 LUFS 单声道
- 通用回退 2 条：`generic-small` / `generic-large`（其余 11 种生物先共用，后续按需补需求文件）

### 视频 ×2（视频模型 768P、4–6s 循环、H.264 无音轨、mp4）

| 名 | 内容 |
| --- | --- |
| `menu-bg` | 体素方块风景缓慢平移，与游戏画面同色系 |
| `laptop-loop` | 像素风屏幕内容（代码雨/复古界面），供家具方块屏幕播放 |

## 3. 生成管线：`tools/generate_media.py`

图片仍走 `generate_art.py`，**本脚本只管音频+视频**。与图片管线同构的部分不再重复设计：`.env` 读 `MINIMAX_API_KEY`（已存在）、提示词唯一来源是需求文件、`--list` / `--dry-run` / `--only <名>` / `--jobs N` 参数语义一致、并发与重试模式一致。

### 3.1 需求文件

- `art/requests/audio/*.md`、`art/requests/video/*.md`，格式沿用图片需求文件，新增两段：
  - `## 参数`：时长、是否无缝循环、目标响度（LUFS）、声道（事件音 mono / 其余 stereo）
  - `## 验收标准`：可判定条款（无突兀人声、循环接缝听不出、音量不炸耳、时长误差 ±20% 内）
- 一个 md 可声明多条资源（BGM 三条可同文件分小节），与图片 `ores.md` 多资源同文件模式一致

### 3.2 音频链路

`POST https://api.minimaxi.com/v1/music_generation`（Bearer key，`is_instrumental: true`，`prompt` ≤2000 字符，`audio_setting: {sample_rate: 44100, bitrate: 256000, format: mp3}`，`output_format: "hex"` 同步返回 hex mp3；`base_resp.status_code` 0=成功、1002=限流重试、1004=鉴权失败；`data.status` 2=已完成；`extra_info.music_duration` 毫秒用于时长校验）→ 产物 `art/incoming/audio/<名>.mp3` → ffmpeg 后处理（本机已有 8.1.1）：

1. 静音首尾裁剪（`silenceremove`）
2. 响度归一（`loudnorm` 到需求文件目标 LUFS）
3. 循环类做首尾交叉淡化接缝（尾部 cross 秒 `acrossfade` 拼回头部，取前 dur 秒）保证无缝
4. 转码 ogg 44.1kHz（Vorbis q5），事件音 mono
5. 时长校验：BGM/环境裁静音后 ≥15s；SFX 裁剪段 0.3–3s；不合格判失败、退出码非 0（SFX 失败触发程序合成兜底，见 §2）

`--install` 入库 `Assets/Resources/Audio/`（与现有 footstep/place/break 同目录同加载模式）， Unity 导入由既有流程处理。**替换风险**：`hit` 不生成 AI 版（程序生成已有且达标），避免无谓占用。

### 3.3 视频链路（受每日配额约束）

视频生成走三步（均 Bearer key）：`POST /v1/video_generation`（model `MiniMax-Hailuo-2.3`——H3 在 V2 接口且按量付费，Token Plan 用不了；`duration: 6`、`resolution: "768P"`、`prompt_optimizer: false` 保像素风精确控制、`aigc_watermark: false`）→ 返回 `task_id` → 轮询 `GET /v1/query/video_generation?task_id=`（status Preparing/Queueing/Processing/Success/Fail，成功给 `file_id`）→ `GET /v1/files/retrieve?file_id=` 取 `download_url`（**1 小时有效**，拿到立即下载）。产物 `art/incoming/video/<名>.mp4` → ffmpeg 后处理（剥音轨、`-pix_fmt yuv420p`、`-movflags +faststart`）→ 入库 `Assets/StreamingAssets/video/`（视频不走 Resources，走 streamingAssetsPath，与 UI 贴图同模式——standalone build 必须可读）。

## 4. 视频每日配额队列（核心机制）

**Token Plan 限制：视频每天只能提交 3 次生成。** 状态文件 `art/incoming/video/quota.json`（gitignored，与 `art/incoming/` 同策略），它和需求文件共同构成唯一真源——不依赖会话、不依赖工作区，任何一天任何机器接着跑都能续上：

```json
{
  "date": "2026-08-18",
  "usedToday": 2,
  "maxPerDay": 3,
  "tasks": [
    { "name": "menu-bg",     "status": "done",    "attempts": 1 },
    { "name": "laptop-loop", "status": "pending", "attempts": 0 }
  ]
}
```

状态机规则（`generate_media.py --videos` 每次执行）：

1. 读 `quota.json`；`date` 非今天 → `usedToday=0`、`date` 更新（每天第一次跑自动重置，无需定时器）
2. 待办 = 需求文件已声明但 `status != done` 的视频，按文件内声明顺序处理；`tasks` 里没有的新声明项自动入队
3. **每次提交前检查 `usedToday < maxPerDay`**，耗尽即停并打印：「今日视频配额已用完（3/3），剩余 N 个任务明天跑 `--videos` 继续」
4. 单条失败 → `attempts+1`、`status=failed`，次日重试（重试同样占配额）；`attempts >= 3` 不再自动重试，打印提示人工改提示词
5. 成功 → `status=done`、`usedToday+1`；`quota.json` 每次状态变更即写回（半截中断不丢进度）
6. 音频不受配额（Token Plan 只限视频）；队列按媒体类型泛化，日后音乐若也限量，`quota.json` 加一类即可

每日自动跑定时任务**默认不挂**（用户确认默认手动）；如需，加一条每天跑 `--videos` 的提醒即可，队列机制本身不依赖它。

## 5. Unity 音频架构（四系统，各 ≤150 行、独立可测）

### 5.1 `BgmAudioSystem`（新，`Unity/Audio/`，挂 WorldBootstrap）

- 双 `AudioSource` 交叉淡入淡出，切换 3s 线性渐变；同状态不打断
- 状态机 `menu → day ↔ night`；**昼夜判定唯一走 `MobManager.IsNightPhase`**（CLAUDE.md 铁律，不在别处手写时间比较）
- clip 加载 `Resources.Load("Audio/bgm-<state>")`，缺失 LogWarning 一次 + 静默
- 音量 = 音乐音量滑条 × `AudioListener.volume`（全局仍管总闸）

### 5.2 `AmbientAudioSystem`（新，同目录同挂点）

- 单 `AudioSource` 循环当前环境音；选择规则（每 2s 判定一次，帧内不做采样）：
  - 玩家 `y < 40`（海平面 62 下 20+ 格，仅洞穴/深矿）→ `amb-cave`
  - 否则 `IsNightPhase` → `amb-crickets`；白天 → `amb-birds`
- 音量同受音乐滑条；资源缺失静默

### 5.3 `PlayerAudioSystem` 扩展（改既有文件）

- 新增 §2 的 12 个事件播放方法（`PlayEat` / `PlayHurt` / ... / `PlayToolBreak`），加载与防御模式沿用现状（缺失 LogWarning 一次、`PlayClip` null 跳过）
- 挂点全在既有钩子上，**热点文件（`BlockInteraction` / `WorldBootstrap`）接线批一次串行改完**（m11 并行规则）：

| 事件 | 挂点（均已核实存在） |
| --- | --- |
| eat | `BlockInteraction.TryEatSelectedFood`（`HungerSystem.Eat` 后） |
| hurt / die | `PlayerController.TakeDamage` 扣血后 / 其 `IsDead` 分支 |
| pickup | `PlayerController.PickupNearbyDrops` 入包成功处 |
| craft | `CraftingPocketUi.TryTakeCraftOutput` / `CraftingWorkbenchUi.TryTakeCraftOutput` / `CraftingFurnaceUi.TryTakeOutput` 取到产出时（熔炉共用 craft 音） |
| door-open/close | `RedstoneSystem.HandleHit` 门分支（`BlockInteraction.UseAt` 对门只挡放置，开关真源在红石系统） |
| hoe-till / plant | `TryTillWithHoe` / `TryPlantSeeds`（现在误用 `PlayPlace`，改为专属音） |
| harvest | `BlockInteraction.BreakAt`：`FarmSystem.TryParseStageBlock` 且 `stage == FarmSystem.MatureStage` 时播 harvest，否则 break |
| tool-break | `BlockInteraction.ApplyDigDurability` 耐久尽分支 |

### 5.4 `MobAudioSystem`（新，`SpawnMob` 自动挂，与 `MobHitFeedback` 同模式）

- idle：每 mob 随机间隔 8–20s（**整数哈希掷骰**定间隔，不持有随机数对象——项目铁律），距玩家 >16 格不播（省性能防吵）
- hurt：`MobHitFeedback` 受击时调用
- 查表：`Resources.Load("Audio/Mobs/<kind小写>-idle")`，缺文件回退 `generic-small`（鸡/兔/仓鼠类）或 `generic-large`（其余），再缺则静默——**新生物加专属叫声 = 放两个 ogg，零代码**

### 5.5 音量设置

`SettingsPanelUi` 加第四滑条「音乐音量」（0–100，PlayerPrefs key `m6.musicVolume`，与 `m6.volume` 同前缀惯例），经静态 `MusicVolumeBus.Volume`（0–1）只管 BGM+环境两类 AudioSource；音效仍走原音量滑条；`AudioListener.volume` 保持全局总闸。同步更新面板高度常量（220→300）与既有测试。

## 6. 视频接入（两处）

### 6.1 `TitleScreenUi`（新，`Unity/UI/`）——极简主菜单

- **开局遮罩模式**：`WorldBootstrap` 步骤序**不动**（步骤 7.5→8→9 的顺序敏感受不了重排）；菜单以最上层遮罩呈现，底下世界照常生成
- 组成：全屏视频背景（`VideoPlayer` → `RenderTexture`，`StreamingAssets/video/menu-bg.mp4` 循环）+ 游戏标题 + 「开始游戏」「退出」两个按钮（IMGUI，样式与暂停菜单一致）
- 打开期间复用模态 UI 指针门（锁输入/解锁光标，与暂停菜单同款）；点「开始游戏」→ 销毁遮罩、解锁、`BgmAudioSystem` menu→day；「退出」→ 走既有同步保存退出路径
- 主菜单期间 BGM 播 `bgm-menu`
- 验证走 `--ui-shot` 截图管线（IMGUI 铁律：`Camera.Render` 拍不到 OnGUI）

### 6.2 `VideoScreenSystem`（新，`Unity/Rendering/`）——笔记本屏幕

- `laptop_block` / `hacker_pc_block` 的 JSON 改专用屏幕面：`textures` 由 `all` 改为 **top/bottom/side 三键全写**（schema 要求三键齐备，`_format.md`），top 填 `laptop-screen`（连字符，与家具贴图 `hacker-pc`/`office-desk` 同规），其余面维持原贴图名；**两方块共用 `laptop-screen` 贴图名与同一段视频**（未来要区分再拆独立贴图名）；屏幕贴图先程序占位一张静态图（`art/scripts/` 确定性生成，m6/m10 占位同模式），同步改 `BlockDefinitionFilesTests.M11FurnitureBlocks` 对照表断言（六面统一 → 顶面 screen + 其余原贴图）
- `BlockMaterialLibrary` 材质按贴图名共享 → `VideoScreenSystem` 启动时创建一个 VideoPlayer+RenderTexture 循环播 `laptop-loop.mp4`，并把 `laptop_screen` 贴图名对应材质的 `mainTexture` 指到 RenderTexture——**贪心网格零改动**，全世界同款笔记本播同一段（可接受且实现最省）
- 降级方案（实机若发现网格/图标渲染异常）：改为方块位置挂附加 quad 播视频（`ItemDropView` 同款做法），破坏时销毁
- 已知副作用接受：hotbar 里笔记本图标是等距渲染真实方块，图标里屏幕面会拍到视频某一帧——视觉上等同「屏幕亮着」，不处理

## 7. 错误处理

| 场景 | 行为 |
| --- | --- |
| 生成 API 失败/超时 | 音频：跳过该条继续其余；视频：`attempts+1` 次日重试（§4） |
| 后处理不合格（时长超标/响度异常） | 不入库，退出码非 0，改提示词重跑 |
| AudioClip/Resources 加载失败 | LogWarning 一次 + 静默跳过（沿用 m3 模式），游戏不阻断 |
| 视频 mp4 缺失/损坏 | 主菜单回退纯色渐变背景、笔记本屏幕维持占位贴图，LogWarning |
| `quota.json` 损坏/缺失 | 视同全新：`usedToday=0` 重建（音频不受影响） |

## 8. 测试（双链只增不减）

- **EditMode 新增**：
  - `BgmAudioSystemTests`：状态机 menu→day→night→day 切换、交叉淡入淡出起止音量、`IsNightPhase` 联动（时间注入）、clip 缺失静默
  - `AmbientAudioSystemTests`：y<40 判洞穴、昼夜分支、2s 节流
  - `MobAudioSystemTests`：kind→路径映射、缺文件回退 generic、idle 间隔哈希确定性（同 seed 同序列）
  - `TitleScreenUiTests`：开局门锁输入、开始后解锁且遮罩销毁
  - `SettingsPanelUiTests`：四滑条（改既有）+ 持久化 key
  - `VideoScreenSystemTests`：材质 mainTexture 替换、mp4 缺失回退
- **`generate_media.py --self-test`**：队列状态机冒烟（临时目录）：跨天重置、配额耗尽停、失败重试、attempts≥3 停、写回幂等
- **dotnet 侧**：Core 无改动（音频全在 Unity 层），数量自然只增不减
- **实机**：`build-and-run.sh` 全绿后 `--ui-shot` 截主菜单；`MyWorld.Preview` 不受影响（无 Core 改动）

## 9. 验收

1. `dotnet test` + Unity EditMode 双链全绿
2. `generate_media.py --self-test` 过；`--audio` 全量生成、后处理、入库一次跑通
3. `--videos` 首日：2 条视频用掉 2/3 配额、队列文件状态正确；人为再入队 2 条测试边界——第 3 条照常生成（3/3），第 4 条被正确拒绝并提示次日再跑
4. 实机剧本（孩子操作）：开游戏见主菜单视频+听见菜单曲 → 开始游戏 BGM 切白天曲 → 挖洞到 y<40 听见滴水声 → 过夜听 BGM/环境切换 → 打猪听叫声、猪受伤有 hurt 音 → 吃面包/开门/锄地/收麦各有一音 → 放笔记本电脑方块屏幕播视频 → 设置面板音乐音量拉零 BGM 静而音效在

## 10. 文件清单

**新增**：`tools/generate_media.py`；`art/requests/audio/`（bgm/ambient/events/mobs 四个 md）；`art/requests/video/`（menu-bg、laptop-loop 两个 md）；`Assets/Resources/Audio/**`（31 ogg）；`Assets/StreamingAssets/video/**`（2 mp4）；`Unity/Audio/{BgmAudioSystem,AmbientAudioSystem,MobAudioSystem}.cs`；`Unity/UI/TitleScreenUi.cs`；`Unity/Rendering/VideoScreenSystem.cs`；`art/scripts/gen_laptop_screen_placeholder.py`；对应 EditMode 测试若干

**修改**：`PlayerAudioSystem.cs`（13 事件方法）；`SettingsPanelUi.cs`（+测试，第四滑条）；`MobHitFeedback.cs`（hurt 音接线）；`laptop_block.json` / `hacker_pc_block.json`（textures.top 拆分 + 占位贴图）；`BlockInteraction.cs` / `WorldBootstrap.cs` / `PlayerController.cs` / `HungerSystem` 接线处（集成点串行批）；`art/README.md`（音频/视频需求索引段）；`BlockDefinitionFilesTests` 若新贴图引用需要同步（占位贴图走 `art/requests/` 立需求保贴图引用差集恒为空）

**不做（明确排除）**：`hit` AI 版（程序生成已达标）；Skeleton/Spider/Creeper 等 11 种生物专属叫声（先用 generic 回退，后续按需补 md）；视频音轨（背景/屏幕视频均无声）；每日自动跑视频的定时任务（默认手动）；3D 空间音效（第一批全 2D，距离衰减只做 >16 格不播的开关）
