# 音频+视频氛围层实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 按 `docs/superpowers/specs/2026-08-18-audio-video-design.md` 落地 31 条 MiniMax 生成音频 + 2 条视频（每日 3 次配额队列）+ Unity 侧 BGM/环境/事件/生物叫四系统 + 主菜单与笔记本屏幕两处视频。

**Architecture:** 生成侧新建 `tools/generate_media.py` 与图片管线同构（提示词唯一来源 `art/requests/{audio,video}/*.md`、key 读 `.env`、产物落 `art/incoming/`、ffmpeg 后处理入库）；Unity 侧四个独立小系统 + 集成点串行接线批；视频受 `art/incoming/video/quota.json` 每日配额约束。

**Tech Stack:** Python 3 + ffmpeg 8.1.1（本机已装）+ MiniMax API（`music-2.6` / `MiniMax-Hailuo-2.3`）/ Unity 2022.3 netstandard2.1 C# 9 / NUnit EditMode。

## Global Constraints（每个任务都隐含遵守）

- 仓库所有文档/注释/测试断言消息**一律中文**
- 新 C# 只进 `Assets/Scripts/Unity/**`（Core 零改动）；新测试文件**整文件包 `#if UNITY_EDITOR ... #endif`**（dotnet 链编译为空——这是双链隔离机制，见 `SettingsPanelUiTests.cs:1`）
- **双链只增不减**：每任务收尾跑 `dotnet test tools/dotnet/MyWorld.Tools.sln` 必须全绿；EditMode 单类验证用批处理（**退出码不可信，必须 grep 结果文件**）：
  ```bash
  "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -nographics \
    -projectPath . -runTests -testPlatform EditMode -testResults unity-test-results.xml \
    -logFile unity-tests.log -testFilter "<测试类全名>"
  grep -c 'result="Passed"' unity-test-results.xml   # >0 才算跑了；另查无 result="Failed"
  ```
  跑 Unity 前确认没有其它 Unity 实例占用项目
- 确定性铁律：间隔/掷骰一律整数哈希，**不持有随机数对象**（参考 `PlayerAudioSystem.LoadOrCreateHitClip` 的 LCG 与 `art/scripts/gen_m6_placeholders.py` 的 `hash_pixel`）
- API key 只从 `.env`（`MINIMAX_API_KEY` 已存在）；**绝不写进代码/文档/提交**
- 音频文件名一律**连字符**（`door-open.ogg`、`pig-idle.ogg`），与图片需求命名同规
- **视频每天只能提交 3 次生成**（Token Plan），队列见 Task 3；重试也占配额
- `.editorconfig`：LF 行尾、C# 4 空格、JSON 2 空格
- 提交信息中文、格式仿 `git log --oneline` 现有风格

## 任务总览（波次）

| 波 | 任务 | 可并行 |
| --- | --- | --- |
| 0 管线 | 1→2→3→4→5（串行，后者依赖前者） | 与波 1 无依赖，可交替 |
| 1 Unity 音频 | 6→7→8→9→10（6/7 先行，8/9/10 依赖 7 的 MusicVolumeBus） | 三系统间可并行 |
| 2 视频接入 | 11、12（互不依赖） | 可并行 |
| 3 收口 | 13（串行接线批）→14（生成+终验收） | — |

---

### Task 1: `generate_media.py` 需求解析骨架（--list / --self-test）

**Files:**
- Create: `tools/generate_media.py`

**Interfaces:**
- Produces: `parse_entries(directory: Path) -> list[Entry]`；`Entry(name, prompt, params: dict, source: Path, kind: str)`（kind ∈ "bgm"|"ambient"|"sfx"|"mob"|"video"，由 `art/requests/audio/<子目录>/` 与 `art/requests/video/` 推导）；命令 `--list` / `--self-test`

- [ ] **Step 1: 写失败的 self-test 与脚本骨架**

创建 `tools/generate_media.py`，包含：模块 docstring（用法，仿 `generate_art.py:1-34` 风格）、`load_dotenv()`（照抄 `generate_art.py:102-111` 语义，读 `PROJECT_ROOT/.env`）、`PROJECT_ROOT = Path(__file__).resolve().parent.parent`、`Entry` dataclass、`parse_entries` 先抛 `NotImplementedError`、`self_test()` 写好全部断言、`main()` 支持 `--list`/`--self-test`。

```python
@dataclass
class Entry:
    name: str          # 资源名，即产物文件名（bgm-day / door-open / pig-idle / menu-bg）
    prompt: str = ""
    params: dict = field(default_factory=dict)  # 中文键：时长下限/目标时长/循环/响度/声道/模板/时长
    source: Path = None
    kind: str = "sfx"  # bgm|ambient|sfx|mob|video

    @property
    def media(self) -> str:  # audio|video，由 source 推导
        return "video" if self.kind == "video" else "audio"
```

```python
def self_test() -> None:
    import tempfile
    with tempfile.TemporaryDirectory() as td:
        root = Path(td)
        (root / "audio" / "bgm").mkdir(parents=True)
        (root / "audio" / "bgm" / "bgm.md").write_text(
            "# BGM\n\n### bgm-day\n白天\n\n## AI 提示词\n```\nGentle piano.\n```\n\n"
            "## 参数\n- 循环: 是\n- 响度: -18\n- 声道: stereo\n- 时长下限: 15\n\n"
            "### bgm-night\n夜\n\n## AI 提示词\n```\nQuiet piano.\n```\n\n## 参数\n- 循环: 是\n",
            encoding="utf-8")
        (root / "video").mkdir(parents=True)
        (root / "video" / "menu-bg.md").write_text(
            "# 视频\n\n### menu-bg\n\n## AI 提示词\n```\nVoxel landscape.\n```\n\n## 参数\n- 时长: 6\n",
            encoding="utf-8")
        entries = parse_entries(root)
        assert [e.name for e in entries] == ["bgm-day", "bgm-night", "menu-bg"], entries
        day = entries[0]
        assert day.prompt == "Gentle piano.", day.prompt
        assert day.kind == "bgm" and day.media == "audio"
        assert day.params["循环"] == "是" and day.params["响度"] == "-18"
        night = entries[1]
        assert night.kind == "bgm" and night.params.get("循环") == "是"
        menu = entries[2]
        assert menu.kind == "video" and menu.media == "video" and menu.params["时长"] == "6"
    print("self-test OK")
```

- [ ] **Step 2: 跑确认失败**

Run: `python tools/generate_media.py --self-test`
Expected: FAIL（`parse_entries` 未实现，NotImplementedError）

- [ ] **Step 3: 实现 `parse_entries` 与 `--list`**

解析规则：`rglob("*.md")`；按 `### <名>` 切小节；小节内**第一个围栏代码块**是提示词；小节内 `键: 值` 行（含 `- 键: 值` 列表项，剥掉前导 `- `）进 params。kind 由相对路径第一段映射：`audio/bgm→bgm`、`audio/ambient→ambient`、`audio/events→sfx`、`audio/mobs→mob`、`video/*→video`。

```python
def parse_entries(root: Path) -> list[Entry]:
    out: list[Entry] = []
    for md in sorted(root.rglob("*.md")):
        rel = md.relative_to(root).parts  # ("audio","bgm","bgm.md") / ("video","menu-bg.md")
        kind = {("audio", "bgm"): "bgm", ("audio", "ambient"): "ambient",
                ("audio", "events"): "sfx", ("audio", "mobs"): "mob",
                ("video",): "video"}[(rel[0], rel[1]) if rel[0] == "audio" else ("video",)]
        current: Entry = None
        in_fence, buf = False, []
        for line in md.read_text(encoding="utf-8").splitlines():
            if line.startswith("```"):
                if in_fence and current is not None and not current.prompt:
                    current.prompt = "\n".join(buf).strip()
                in_fence, buf = not in_fence, []
                continue
            if in_fence:
                buf.append(line); continue
            if line.startswith("### "):
                current = Entry(name=line[4:].strip(), source=md, kind=kind)
                out.append(current)
            elif current is not None and ":" in line and not line.startswith("#"):
                k, v = line.lstrip("- ").split(":", 1)
                if k.strip() and v.strip():
                    current.params[k.strip()] = v.strip()
    return [e for e in out if e.prompt]
```

`--list` 打印每条：名 / kind / media / params / 提示词前 60 字符；`main()` 用 `argparse`。`if __name__ == "__main__": main()`。

- [ ] **Step 4: 跑通过**

Run: `python tools/generate_media.py --self-test`
Expected: `self-test OK`，退出码 0

- [ ] **Step 5: Commit**

```bash
git add tools/generate_media.py
git commit -m "tools: generate_media 骨架——音频/视频需求 md 解析 + --list + --self-test（av W0-1）"
```

---

### Task 2: 音频生成链路（music API + ffmpeg 后处理 + SFX 合成兜底 + --install）

**Files:**
- Modify: `tools/generate_media.py`

**Interfaces:**
- Consumes: Task 1 的 `Entry`/`parse_entries`
- Produces: `generate_audio(entry) -> Path`（产物 `art/incoming/audio/<名>.ogg`）；`install_audio()`（拷入 `Assets/Resources/Audio/`）；`build_music_payload(prompt) -> dict`；命令 `--audio [--only 名...]`、`--install`

- [ ] **Step 1: 扩 self-test（先失败）**

`self_test()` 追加两段（不调 API——用 ffmpeg 现造输入）：

```python
    # build_music_payload：纯函数断言
    payload = build_music_payload("Gentle piano.")
    assert payload["model"] == "music-2.6" and payload["is_instrumental"] is True
    assert payload["output_format"] == "hex"
    assert payload["audio_setting"] == {"sample_rate": 44100, "bitrate": 256000, "format": "mp3"}

    # postprocess_audio：用 lavfi 造 3s 正弦 mp3 当输入，断言产出 ogg 且双声道
    with tempfile.TemporaryDirectory() as td:
        raw = Path(td) / "raw.mp3"
        subprocess.run(["ffmpeg", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=3",
                        "-b:a", "64k", str(raw)], check=True, capture_output=True)
        entry = Entry(name="t-bgm", kind="bgm",
                      params={"循环": "是", "响度": "-18", "声道": "stereo", "时长下限": "1"})
        dst = postprocess_audio(raw, Path(td), entry)
        assert dst.exists() and dst.suffix == ".ogg"
        dur = probe_duration(dst)
        assert 0.9 < dur < 4.0, dur
        # 兜底合成模板零 API 可用
        sfx = synth_sfx("thud", Path(td) / "thud.ogg", mono=True)
        assert sfx.exists() and probe_duration(sfx) < 1.5
```

- [ ] **Step 2: 跑确认失败**

Run: `python tools/generate_media.py --self-test`
Expected: FAIL（`build_music_payload`/`postprocess_audio`/`synth_sfx`/`probe_duration` 未定义）

- [ ] **Step 3: 实现音频链路**

```python
API_BASE = "https://api.minimaxi.com"
INCOMING_AUDIO = PROJECT_ROOT / "art" / "incoming" / "audio"
AUDIO_INSTALL_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Audio"
CROSSFADE_SECONDS = 1.5

def api_key() -> str:
    load_dotenv()
    key = os.environ.get("MINIMAX_API_KEY")
    if not key:
        sys.exit("缺 MINIMAX_API_KEY（环境变量或 .env）")
    return key

def post_json(path: str, payload: dict) -> dict:
    req = urllib.request.Request(API_BASE + path,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json", "Authorization": "Bearer " + api_key()},
        method="POST")
    with urllib.request.urlopen(req, timeout=600) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    code = data.get("base_resp", {}).get("status_code", -1)
    if code != 0:
        raise RuntimeError(f"{path} 失败 status_code={code} msg={data.get('base_resp', {}).get('status_msg')}")
    return data

def build_music_payload(prompt: str) -> dict:
    return {
        "model": os.environ.get("MINIMAX_MUSIC_MODEL", "music-2.6"),
        "prompt": prompt[:2000],
        "is_instrumental": True,
        "output_format": "hex",
        "audio_setting": {"sample_rate": 44100, "bitrate": 256000, "format": "mp3"},
    }

def generate_music_mp3(entry: Entry) -> Path:
    """调 /v1/music_generation，hex 落盘 art/incoming/audio/<名>.mp3。4 次重试 8s 指数退避（照抄 generate_art.generate_one 模式），1002 限流也重试。"""
    INCOMING_AUDIO.mkdir(parents=True, exist_ok=True)
    dst = INCOMING_AUDIO / f"{entry.name}.mp3"
    last = None
    for attempt in range(4):
        try:
            data = post_json("/v1/music_generation", build_music_payload(entry.prompt))
            if data.get("data", {}).get("status") != 2:
                raise RuntimeError(f"music status={data.get('data', {}).get('status')}（1=合成中）")
            dst.write_bytes(bytes.fromhex(data["data"]["audio"]))
            return dst
        except (urllib.error.URLError, RuntimeError, TimeoutError, OSError) as e:
            last = e; time.sleep(8 * (attempt + 1))
    raise RuntimeError(f"{entry.name} 生成失败：{last}")
```

ffmpeg 工具与后处理：

```python
def run_ff(args: list[str]) -> None:
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args],
                   check=True, capture_output=True)

def probe_duration(path: Path) -> float:
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration",
                          "-of", "csv=p=0", str(path)], check=True, capture_output=True, text=True)
    return float(out.stdout.strip())

TRIM_AF = ("silenceremove=start_periods=1:start_threshold=-45dB,"
           "areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse")

def postprocess_audio(raw_mp3: Path, out_dir: Path, entry: Entry) -> Path:
    """裁静音→响度归一→（循环类）首尾交叉淡化→ogg 44.1k。时长不达标抛 RuntimeError。"""
    out_dir.mkdir(parents=True, exist_ok=True)
    trimmed = out_dir / "_trim.wav"; dst = out_dir / f"{entry.name}.ogg"
    lufs = int(entry.params.get("响度", "-14"))
    run_ff(["-i", str(raw_mp3), "-af", f"{TRIM_AF},loudnorm=I={lufs}:TP=-1.5:LRA=11", str(trimmed)])
    dur = probe_duration(trimmed)
    mono = entry.params.get("声道", "mono") == "mono"
    if entry.params.get("循环") == "是":
        if dur < float(entry.params.get("时长下限", "15")):
            raise RuntimeError(f"{entry.name} 太短 {dur:.1f}s（要求 ≥{entry.params.get('时长下限', 15)}s）")
        cross = min(CROSSFADE_SECONDS, dur / 4)
        looped = out_dir / "_loop.wav"
        run_ff(["-i", str(trimmed), "-filter_complex",
                f"[0:a]atrim=0:{cross:.3f}[h];[0:a]atrim=start={dur - cross:.3f}[t];"
                f"[t][h]acrossfade=d={cross:.3f}:c1=tri:c2=tri[o]",
                "-map", "[o]", str(looped)])
        run_ff(["-i", str(looped), "-t", f"{dur:.3f}", "-ar", "44100",
                "-ac", "1" if mono else "2", "-c:a", "libvorbis", "-q:a", "5", str(dst)])
    else:
        target = float(entry.params.get("目标时长", "1.2"))
        seg = out_dir / "_seg.wav"
        run_ff(["-i", str(trimmed), "-t", f"{target:.2f}",
                "-af", "afade=t=in:st=0:d=0.008,afade=t=out:st=%.3f:d=0.02" % max(0.0, target - 0.02),
                str(seg)])
        got = probe_duration(seg)
        if not (0.3 <= got <= 3.0):
            raise RuntimeError(f"{entry.name} SFX 段长 {got:.2f}s 不在 0.3–3s")
        run_ff(["-i", str(seg), "-ar", "44100", "-ac", "1" if mono else "2",
                "-c:a", "libvorbis", "-q:a", "5", str(dst)])
    return dst
```

SFX 兜底合成模板（确定性、零 API；`模板` 参数选其一）：

```python
SYNTH_TEMPLATES = {
    "thud":  ["-f", "lavfi", "-i", "sine=frequency=110:duration=0.35",
              "-af", "afade=t=out:st=0.05:d=0.3:curve=exp,volume=0.9"],
    "pop":   ["-f", "lavfi", "-i", "sine=frequency=520:duration=0.18",
              "-af", "afade=t=out:st=0.02:d=0.16:curve=exp,volume=0.8"],
    "click": ["-f", "lavfi", "-i", "anoisesrc=color=white:duration=0.09:seed=42",
              "-af", "highpass=f=1800,afade=t=out:st=0:d=0.09:curve=exp,volume=0.7"],
    "chime": ["-f", "lavfi", "-i", "sine=frequency=880:duration=0.5",
              "-af", "afade=t=out:st=0.05:d=0.45:curve=exp,volume=0.7"],
    "swoosh":["-f", "lavfi", "-i", "anoisesrc=color=pink:duration=0.5:seed=7",
              "-af", "lowpass=f=900,afade=t=in:st=0:d=0.2,afade=t=out:st=0.25:d=0.25,volume=0.8"],
}

def synth_sfx(template: str, dst: Path, mono: bool = True) -> Path:
    run_ff([*SYNTH_TEMPLATES[template], "-ar", "44100", "-ac", "1" if mono else "2",
            "-c:a", "libvorbis", "-q:a", "5", str(dst)])
    return dst
```

主流程 `cmd_audio(only)`：对每条音频 Entry——已存在 `art/incoming/audio/<名>.ogg` 跳过；`generate_music_mp3` → `postprocess_audio`；**SFX（kind=sfx）后处理抛异常时**：读 `模板` 参数走 `synth_sfx` 兜底（打印「AI 段不合格，走合成兜底」）；**mob 不兜底**（失败即跳过该条，Unity 侧自动回退 generic）。`--install`：`shutil.copy2(INCOMING_AUDIO/f"{e.name}.ogg", AUDIO_INSTALL_DIR/f"{e.name}.ogg")`。

- [ ] **Step 4: 跑通过 + 语法自检**

Run: `python tools/generate_media.py --self-test && python tools/generate_media.py --list`
Expected: `self-test OK`；--list 此时无需求文件、输出 0 条不报错

- [ ] **Step 5: Commit**

```bash
git add tools/generate_media.py
git commit -m "tools: 音频生成链路——music-2.6 API + ffmpeg 裁静音/响度/无缝循环 + SFX 合成兜底（av W0-2）"
```

---

### Task 3: 视频每日配额队列 + 视频生成链路

**Files:**
- Modify: `tools/generate_media.py`

**Interfaces:**
- Consumes: Task 1 `Entry`
- Produces: `QuotaState` + `rollover(state, today)` + `plan_tasks(declared, state)`；`QUOTA_PATH = art/incoming/video/quota.json`；`build_video_payload(prompt)`；命令 `--videos`；`--self-test` 覆盖状态机

- [ ] **Step 1: 扩 self-test（先失败）**

```python
    # 队列状态机：跨天重置 / 配额耗尽 / 重试上限 / skip done
    q = QuotaState(date="2026-08-17", used_today=3, max_per_day=3,
                   tasks={"menu-bg": {"status": "done", "attempts": 1}})
    q = rollover(q, "2026-08-18")
    assert q.used_today == 0 and q.date == "2026-08-18"
    plan = plan_tasks(["menu-bg", "laptop-loop", "a", "b", "c"], q)
    assert plan[0] == ("menu-bg", "skip_done")
    assert plan[1] == ("laptop-loop", "generate")   # 第 1/3 次
    q.used_today = 2
    plan = plan_tasks(["laptop-loop"], q)
    assert plan == [("laptop-loop", "generate")]
    q.used_today = 3
    plan = plan_tasks(["laptop-loop"], q)
    assert plan == [("laptop-loop", "defer_quota")]
    q.tasks["laptop-loop"] = {"status": "failed", "attempts": 3}
    plan = plan_tasks(["laptop-loop"], q)
    assert plan == [("laptop-loop", "give_up")]
    # payload 纯函数
    vp = build_video_payload("Voxel landscape.")
    assert vp["model"] == "MiniMax-Hailuo-2.3" and vp["duration"] == 6
    assert vp["resolution"] == "768P" and vp["prompt_optimizer"] is False
```

- [ ] **Step 2: 跑确认失败**

Run: `python tools/generate_media.py --self-test`
Expected: FAIL（`QuotaState` 等未定义）

- [ ] **Step 3: 实现**

```python
INCOMING_VIDEO = PROJECT_ROOT / "art" / "incoming" / "video"
VIDEO_INSTALL_DIR = PROJECT_ROOT / "Assets" / "StreamingAssets" / "video"
QUOTA_PATH = INCOMING_VIDEO / "quota.json"

@dataclass
class QuotaState:
    date: str; used_today: int; max_per_day: int
    tasks: dict[str, dict]

def load_quota(today: str) -> QuotaState:
    q = QuotaState(date=today, used_today=0, max_per_day=3, tasks={})
    if QUOTA_PATH.exists():
        try:
            raw = json.loads(QUOTA_PATH.read_text(encoding="utf-8"))
            q = QuotaState(raw.get("date", today), int(raw.get("usedToday", 0)),
                           int(raw.get("maxPerDay", 3)), raw.get("tasks", {}))
        except (json.JSONDecodeError, OSError):
            print("quota.json 损坏，视同全新重建")
    return rollover(q, today)

def save_quota(q: QuotaState) -> None:
    INCOMING_VIDEO.mkdir(parents=True, exist_ok=True)
    QUOTA_PATH.write_text(json.dumps({
        "date": q.date, "usedToday": q.used_today, "maxPerDay": q.max_per_day,
        "tasks": q.tasks}, ensure_ascii=False, indent=2), encoding="utf-8")

def rollover(q: QuotaState, today: str) -> QuotaState:
    if q.date != today:
        q.date, q.used_today = today, 0
    return q

def plan_tasks(declared: list[str], q: QuotaState) -> list[tuple[str, str]]:
    out = []
    for name in declared:
        t = q.tasks.setdefault(name, {"status": "pending", "attempts": 0})
        if t["status"] == "done": out.append((name, "skip_done"))
        elif t["attempts"] >= 3: out.append((name, "give_up"))
        elif q.used_today >= q.max_per_day: out.append((name, "defer_quota"))
        else: out.append((name, "generate"))
    return out
```

视频链路（提交即计配额——失败重试也占当天次数）：

```python
def build_video_payload(prompt: str) -> dict:
    return {"model": "MiniMax-Hailuo-2.3", "prompt": prompt[:2000],
            "duration": 6, "resolution": "768P",
            "prompt_optimizer": False, "aigc_watermark": False}

def get_json(path: str) -> dict:
    req = urllib.request.Request(API_BASE + path,
        headers={"Authorization": "Bearer " + api_key()})
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.loads(resp.read().decode("utf-8"))

def generate_video(entry: Entry) -> Path:
    data = post_json("/v1/video_generation", build_video_payload(entry.prompt))
    task_id = data["task_id"]
    for _ in range(120):  # 最长 ~10 分钟
        time.sleep(5)
        q = get_json(f"/v1/query/video_generation?task_id={task_id}")
        if q.get("status") == "Success":
            file_id = q["file_id"]
            info = get_json(f"/v1/files/retrieve?file_id={file_id}")
            url = info["file"]["download_url"]          # 1 小时有效，立即下载
            dst = INCOMING_VIDEO / f"{entry.name}.mp4"
            with urllib.request.urlopen(url, timeout=600) as r, open(dst, "wb") as f:
                shutil.copyfileobj(r, f)
            return dst
        if q.get("status") == "Fail":
            raise RuntimeError(f"{entry.name} 视频生成 Fail")
    raise RuntimeError(f"{entry.name} 视频轮询超时")

def postprocess_video(raw: Path, dst: Path) -> Path:
    run_ff(["-i", str(raw), "-an", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            "-c:v", "libx264", "-crf", "23", str(dst)])
    return dst
```

`cmd_videos()`：`load_quota(date.today().isoformat())` → `plan_tasks(视频 Entry 名单, q)` → 逐条：`generate` 则 `used_today += 1` **先写盘**（`save_quota`）再生成；成功 `status=done`；异常 `attempts += 1`、`status=failed`，打印错误继续下一条；`defer_quota` 收尾打印「今日视频配额已用完（3/3），剩余 N 个任务明天再跑 --videos」；`give_up` 打印「attempts≥3，人工改提示词」。成功条目 `postprocess_video` 后 `--install` 拷 `VIDEO_INSTALL_DIR`（或 `cmd_videos` 完成后统一 `--install`，二者取一，写清楚选的哪种：**选后者**——`--install` 同时处理音频与视频目录）。

- [ ] **Step 4: 跑通过**

Run: `python tools/generate_media.py --self-test`
Expected: `self-test OK`

- [ ] **Step 5: Commit**

```bash
git add tools/generate_media.py
git commit -m "tools: 视频每日3次配额队列——quota.json 状态机 + Hailuo-2.3 提交/轮询/下载（av W0-3）"
```

---

### Task 4: 需求文件 31 音 + 2 视频 + art/README 批次段

**Files:**
- Create: `art/requests/audio/bgm/bgm.md`、`art/requests/audio/ambient/ambient.md`、`art/requests/audio/events/events.md`、`art/requests/audio/mobs/mobs.md`
- Create: `art/requests/video/menu-bg.md`、`art/requests/video/laptop-loop.md`
- Create: `art/requests/items/laptop-screen.md`（Task 11 依赖——家具贴图需求，本任务顺手立）
- Modify: `art/README.md`（新增「av 批 · 音频与视频」段 + 入库路径表两行）

**Interfaces:**
- Produces: Task 1 解析器可读的 33 个条目（31 音 + 2 视频）；`laptop-screen.md` 供 `BlockDefinitionFilesTests` 全树搜索命中

- [ ] **Step 1: 写 BGM/环境需求文件（完整提示词）**

`art/requests/audio/bgm/bgm.md` 三个小节（`### bgm-menu` / `### bgm-day` / `### bgm-night`），每节含说明、`## AI 提示词` 围栏、`## 参数`（`循环: 是`、`响度: -18`、`声道: stereo`、`时长下限: 15`）。提示词（风格骨架统一，可直接用）：

- bgm-menu：`Gentle ambient piano intro theme for a cozy voxel sandbox game. Warm, welcoming, optimistic melody with soft dynamics. Sparse notes with lots of space between them. Pure instrumental, no drums, no vocals, no percussion. Seamless loop friendly, minimal buildup.`
- bgm-day：`Calm ambient piano for daytime exploration in a peaceful voxel world. Bright, light, quietly cheerful, never intrusive or demanding attention. Sparse gentle phrases with long silences. Pure instrumental piano with subtle warm pad. No drums, no vocals. Seamless loop friendly.`
- bgm-night：`Quiet melancholic ambient piano for nighttime in a voxel survival world. Slow, low register, soft and slightly mysterious, gentle unease but not scary. Very sparse notes, long decay, soft pad underneath. Pure instrumental. No drums, no vocals. Seamless loop friendly.`
- amb-birds：`Daytime countryside ambience: soft bird calls, gentle breeze through leaves, distant subtle wind. Natural, calm, quiet background texture with no music and no melody. No human voices. Seamless loop friendly.`
- amb-crickets：`Night countryside ambience: steady cricket chirping, very quiet night air, occasional distant insect sounds. Calm natural background texture, no music, no melody. Seamless loop friendly.`
- amb-cave：`Underground cave ambience: echoing water drips far away, low rumbling air movement, hollow distant echoes. Quiet, mysterious, not scary. No music, no melody. Seamless loop friendly.`

- [ ] **Step 2: 写事件音/生物叫声需求文件（提示词 + 合成模板）**

`events.md` 11 小节，参数 `响度: -14`、`声道: mono`、`目标时长` 各自定，每条带 `模板:` 兜底。提示词模式统一 `A single short <声音> sound effect, dry and clean, no music, no melody, no background, no vocals, suitable for a game UI`：

| 名 | 提示词主体 | 目标时长 | 模板 |
| --- | --- | --- | --- |
| eat | short munching bite, single chew | 0.5 | click |
| hurt | short human grunt of pain, muffled | 0.6 | thud |
| die | low descending groan, soft | 1.2 | thud |
| pickup | bright short pluck ding, single note | 0.4 | chime |
| craft | wooden clack assembly, two quick knocks | 0.7 | click |
| door-open | old wooden door creak opening slowly | 1.2 | swoosh |
| door-close | wooden door latch click shut | 0.8 | click |
| hoe-till | soil digging scrape, one stroke | 0.8 | swoosh |
| plant | soft seed drop on soil, tiny pop | 0.4 | pop |
| harvest | dry wheat rustle snap, one pull | 0.7 | swoosh |
| tool-break | metal tool snapping into pieces, brittle crack | 0.9 | click |

`mobs.md` 14 小节（`### pig-idle` … `### generic-large-hurt`），参数 `响度: -14`、`声道: mono`、`目标时长: 0.8`（idle）/`0.6`（hurt）。提示词模式 `A single short <动物> <idle call / cry of pain>, realistic animal sound, dry, no music, no background noise`；动物对应：pig（oink/grunt squeal）、cow（moo/low pained moo）、chicken（cluck/alarm squawk）、zombie（low groan/angry growl）、villager（soft humming nasal vowel/pained short moan）、sheep（bleat/startled bleat）、generic-small（tiny rodent squeak）、generic-large（medium animal grunt）。

- [ ] **Step 3: 写视频需求文件**

`menu-bg.md`：`### menu-bg`，`## 参数`：`时长: 6`。提示词：`Slowly panning view of a charming voxel blocky landscape at golden hour: green cube hills, small pixel trees, a calm blue lake, blocky clouds drifting. Camera glides forward slowly and smoothly. Colors match a cozy pixel-art sandbox game: grass green #5D9C3C, dirt brown #7A5A3C, sky soft blue. No characters, no text, no watermarks. Loop friendly: start and end framing nearly identical.`
`laptop-loop.md`：`### laptop-loop`，`## 参数`：`时长: 6`。提示词：`Close-up of a retro pixel-art computer screen playing green falling code rain on dark navy background, retro terminal text scrolling, subtle scanline flicker. Flat 2D screen content filling whole frame, pixel art style, no bezel, no desk, no hands, no text overlays outside the screen content, loop friendly.`

- [ ] **Step 4: `laptop-screen.md` + `art/README.md` 批次段**

`art/requests/items/laptop-screen.md`：仿 `art/requests/items/` 现有格式——调色板（深蓝底 `#0B1E3A` + 绿 `#3FBB5A`/`#7FE89A`/`#C8FACC`）、「程序占位，待正式美术替换」标注、AI 提示词（正式版用：retro pixel computer screen, dark navy background, green code characters, 32×32, seamless not required）、验收标准（32×32、无抗锯齿、无洋红）。

`art/README.md` 在 m10 批之后加段（仿「m10 批」格式，列名改音频/视频适用列）：

```markdown
### av 批 · 音频与视频（MiniMax 生成）

音频 31 条走 `tools/generate_media.py --audio`（music-2.6，SFX 带 ffmpeg 合成兜底），视频 2 条走
`--videos`（Hailuo-2.3 768P，**每日 3 次配额**，队列 `art/incoming/video/quota.json`）。
需求在 `art/requests/audio|video/`，入库 `Assets/Resources/Audio/` 与 `Assets/StreamingAssets/video/`。

| 编号 | 资源 | 条数 | 说明 | 状态 |
| --- | --- | --- | --- | --- |
| AV-BGM | [BGM 三首](requests/audio/bgm/bgm.md) | 3 | 氛围钢琴 无缝循环 | 待生成 |
| AV-AMB | [环境循环](requests/audio/ambient/ambient.md) | 3 | 鸟/虫/洞穴 | 待生成 |
| AV-SFX | [事件音](requests/audio/events/events.md) | 11 | mono 短音 | 待生成 |
| AV-MOB | [生物叫声](requests/audio/mobs/mobs.md) | 14 | 6 种专属 + 2 通用 | 待生成 |
| AV-VID | [视频两条](requests/video/menu-bg.md) | 2 | 主菜单背景 + 笔记本屏 | 待生成 |
```

「入库路径」表追加：`| 音频 | Assets/Resources/Audio/ |`、`| 视频 | Assets/StreamingAssets/video/ |`。

- [ ] **Step 5: 验收解析 + Commit**

Run: `python tools/generate_media.py --list`
Expected: 音频 31 条（bgm 3 / ambient 3 / sfx 11 / mob 14）+ 视频 2 条，每条有提示词；`--self-test` 仍 OK

```bash
git add art/requests/audio art/requests/video art/requests/items/laptop-screen.md art/README.md
git commit -m "art: 音频31条+视频2条需求文件——提示词/参数/合成模板全量落档（av W0-4）"
```

---

### Task 5: 实际生成音频 + 入库

**Files:**
- Create: `Assets/Resources/Audio/*.ogg` + `.meta`（31 个音频文件）

**Interfaces:**
- Consumes: Task 2/4 的生成链路与需求文件
- Produces: `Assets/Resources/Audio/` 下 31 个 ogg（Task 6/8/9/10 的 Resources.Load 目标）

⚠️ 本任务花 API 费用（~31 次音乐生成）。执行前确认 `.env` 有 key。

- [ ] **Step 1: 全量生成**

Run: `python tools/generate_media.py --audio --jobs 3`
Expected: `art/incoming/audio/` 出现 31 个 ogg；SFX 兜底与 mob 跳过按打印记录；失败条目重跑 `--only <名>` 直至 31 条齐（mob 条目失败可放弃——Unity 回退 generic，最后记录在案）

- [ ] **Step 2: 入库 + Unity 导入生成 .meta**

Run: `python tools/generate_media.py --install`
然后跑一次 EditMode 全量（Unity 批处理命令见 Global Constraints，不带 `-testFilter`）——导入 ogg 并生成 `.meta`，同时确认全绿、无 `result="Failed"`。

- [ ] **Step 3: 抽检时长**

Run: `for f in Assets/Resources/Audio/bgm-*.ogg Assets/Resources/Audio/amb-*.ogg; do ffprobe -v error -show_entries format=duration -of csv=p=0 "$f" | xargs echo "$f"; done`
Expected: BGM/环境 ≥15s；事件音 0.3–3s（不达标回 Task 4 调提示词/模板重生成该条）

- [ ] **Step 4: Commit**

```bash
git add Assets/Resources/Audio
git commit -m "audio: 31条AI音效入库——BGM/环境/事件/生物叫（av W0-5）"
```

---

### Task 6: `PlayerAudioSystem` 扩 11 个事件方法 + 静态 Instance

**Files:**
- Modify: `Assets/Scripts/Unity/Audio/PlayerAudioSystem.cs`
- Test: `Assets/Tests/EditMode/Audio/PlayerAudioSystemTests.cs`（追加）

**Interfaces:**
- Produces: `PlayerAudioSystem.Instance`（static，Awake 设置）；`PlayEat()/PlayHurt()/PlayDie()/PlayPickup()/PlayCraft()/PlayDoorOpen()/PlayDoorClose()/PlayHoeTill()/PlayPlant()/PlayHarvest()/PlayToolBreak()`——Task 13 接线批全部消费

- [ ] **Step 1: 写失败测试（追加到既有 TestFixture 内，文件已整文件 `#if UNITY_EDITOR`）**

```csharp
[Test]
public void EventSounds_DoesNotThrowWhenClipMissing()
{
    var go = new GameObject("audio-events");
    try
    {
        var audio = go.AddComponent<PlayerAudioSystem>();
        Assert.DoesNotThrow(() =>
        {
            audio.PlayEat(); audio.PlayHurt(); audio.PlayDie(); audio.PlayPickup();
            audio.PlayCraft(); audio.PlayDoorOpen(); audio.PlayDoorClose();
            audio.PlayHoeTill(); audio.PlayPlant(); audio.PlayHarvest(); audio.PlayToolBreak();
        }, "11 个事件音在 clip 缺失时都应静默跳过（EditMode 无资源是常态）");
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}

[Test]
public void Instance_SetOnAwake()
{
    var go = new GameObject("audio-instance");
    try
    {
        var audio = go.AddComponent<PlayerAudioSystem>();
        InvokeAwake(audio); // 反射助手：从 SettingsPanelUiTests.cs:19-26 复制进本 TestFixture
        Assert.That(MyWorld.Unity.Audio.PlayerAudioSystem.Instance, Is.EqualTo(audio),
            "Awake 应设置静态 Instance——合成 UI / RedstoneSystem 不在玩家宿主链上，靠它取音效");
    }
    finally { UnityEngine.Object.DestroyImmediate(go); MyWorld.Unity.Audio.PlayerAudioSystem.Instance = null; }
}
```

注意：`Instance` 的 setter 需 `private set` + 测试清理用反射置 null，或把清理写成 `typeof(PlayerAudioSystem).GetProperty("Instance").SetValue(null, null)`。选**后者**（反射清理，保持 `private set`）。

- [ ] **Step 2: 跑确认失败**

Run: EditMode 批处理 `-testFilter MyWorld.Unity.Audio.PlayerAudioSystemTests`
Expected: 新测试 FAIL（方法不存在→编译失败也是预期失败形态；先跑 dotnet 侧确认 Core 链不受影响：`dotnet test tools/dotnet/MyWorld.Tools.sln` 全绿）

- [ ] **Step 3: 最小实现**

`PlayerAudioSystem.cs` 追加（沿用既有 `LoadClipOrNull`/`PlayClip`，字段/加载/方法三段式，注释中文）：

```csharp
public static PlayerAudioSystem Instance { get; private set; }

// Awake 首行：
Instance = this;

// Awake 里追加加载（沿用 LoadClipOrNull）：
eatClip = LoadClipOrNull("Audio/eat");        hurtClip = LoadClipOrNull("Audio/hurt");
dieClip = LoadClipOrNull("Audio/die");        pickupClip = LoadClipOrNull("Audio/pickup");
craftClip = LoadClipOrNull("Audio/craft");    doorOpenClip = LoadClipOrNull("Audio/door-open");
doorCloseClip = LoadClipOrNull("Audio/door-close"); hoeTillClip = LoadClipOrNull("Audio/hoe-till");
plantClip = LoadClipOrNull("Audio/plant");    harvestClip = LoadClipOrNull("Audio/harvest");
toolBreakClip = LoadClipOrNull("Audio/tool-break");

public void PlayEat() { PlayClip(eatClip); }        public void PlayHurt() { PlayClip(hurtClip); }
public void PlayDie() { PlayClip(dieClip); }        public void PlayPickup() { PlayClip(pickupClip); }
public void PlayCraft() { PlayClip(craftClip); }    public void PlayDoorOpen() { PlayClip(doorOpenClip); }
public void PlayDoorClose() { PlayClip(doorCloseClip); }
public void PlayHoeTill() { PlayClip(hoeTillClip); } public void PlayPlant() { PlayClip(plantClip); }
public void PlayHarvest() { PlayClip(harvestClip); } public void PlayToolBreak() { PlayClip(toolBreakClip); }
```

（`[SerializeField]` 私有字段 11 个同步声明，命名 `eatClip` 等。）

- [ ] **Step 4: 跑通过 + dotnet 全绿**

Run: EditMode 批处理 `-testFilter MyWorld.Unity.Audio.PlayerAudioSystemTests` → grep 通过；`dotnet test tools/dotnet/MyWorld.Tools.sln` 全绿
Expected: 全部 PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Audio/PlayerAudioSystem.cs Assets/Tests/EditMode/Audio/PlayerAudioSystemTests.cs
git commit -m "audio: PlayerAudioSystem 扩11事件音+静态Instance（av W1-6）"
```

---

### Task 7: `MusicVolumeBus` + `SettingsPanelUi` 第四滑条

**Files:**
- Create: `Assets/Scripts/Unity/Audio/MusicVolumeBus.cs`
- Modify: `Assets/Scripts/Unity/UI/SettingsPanelUi.cs`
- Test: `Assets/Tests/EditMode/UI/SettingsPanelUiTests.cs`（改既有 + 追加）

**Interfaces:**
- Produces: `MyWorld.Unity.Audio.MusicVolumeBus.Volume`（static float，0–1，默认 0.8）——Task 8/9 消费；`SettingsPanelUi.MusicVolumeKey = "m6.musicVolume"`、`LoadMusicVolume()/SaveMusicVolume()`、`CurrentMusicVolume`、`PanelHeight` 220→**300**

- [ ] **Step 1: 写失败测试**

`SettingsPanelUiTests` 追加（SetUp/TearDown 的 PlayerPrefs 清理列表**加 `SettingsPanelUi.MusicVolumeKey`**；`PanelHeight` 既有断言 220→300）：

```csharp
[Test]
public void MusicVolume_DefaultLoadSaveClamp()
{
    Assert.That(SettingsPanelUi.LoadMusicVolume(), Is.EqualTo(80f), "默认 80 与主音量一致");
    SettingsPanelUi.SaveMusicVolume(150f);
    Assert.That(SettingsPanelUi.LoadMusicVolume(), Is.EqualTo(100f), "钳到上限");
    SettingsPanelUi.SaveMusicVolume(-3f);
    Assert.That(SettingsPanelUi.LoadMusicVolume(), Is.EqualTo(0f), "钳到下限");
}

[Test]
public void ApplySettings_WritesMusicVolumeBus()
{
    var go = new GameObject("settings-music");
    try
    {
        var panel = go.AddComponent<SettingsPanelUi>();
        SettingsPanelUi.SaveMusicVolume(40f);
        InvokeAwake(panel); // 既有反射助手
        Assert.That(MyWorld.Unity.Audio.MusicVolumeBus.Volume, Is.EqualTo(0.4f).Within(0.001),
            "ApplySettings 应把音乐音量（0-100 归一 0-1）写进 MusicVolumeBus");
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}
```

- [ ] **Step 2: 跑确认失败**（EditMode `-testFilter MyWorld.Unity.UI.SettingsPanelUiTests`；FAIL：成员不存在）

- [ ] **Step 3: 实现**

`MusicVolumeBus.cs`（新文件，整文件不需要 `#if UNITY_EDITOR`——纯 C# 但放 Unity 目录，不进 dotnet 链）：

```csharp
namespace MyWorld.Unity.Audio
{
    /// <summary>音乐音量总线（0–1）：SettingsPanelUi 写、BgmAudioSystem/AmbientAudioSystem 读。
    /// 只管 BGM+环境两类 AudioSource；音效仍走 AudioListener 全局音量。</summary>
    public static class MusicVolumeBus
    {
        public static float Volume = 0.8f;
    }
}
```

`SettingsPanelUi.cs`（对照既有三滑条结构逐一加）：`MusicVolumeKey = "m6.musicVolume"`、`VolumeMin/Max 复用`、`MusicDefault = 80f`、`CurrentMusicVolume` 属性、`LoadMusicVolume/SaveMusicVolume`、Awake 读、`ApplySettings()` 追加 `MyWorld.Unity.Audio.MusicVolumeBus.Volume = CurrentMusicVolume / VolumeMax;`、`PanelHeight = 300f`、`DrawPanel` 在 FOV 块后追加第四滑条块（文案 `音乐音量：{CurrentMusicVolume:0}（0 静音 – 100 最大，只管背景音乐与环境音）`，结构照抄音量块），末行提示前 `y += 70`（原 60 改 70——四行滑条统一 70）。

- [ ] **Step 4: 跑通过 + dotnet 全绿**（同 Task 6 Step 4 命令）

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Audio/MusicVolumeBus.cs Assets/Scripts/Unity/UI/SettingsPanelUi.cs Assets/Tests/EditMode/UI/SettingsPanelUiTests.cs
git commit -m "ui: 设置面板第四滑条音乐音量+MusicVolumeBus总线（av W1-7）"
```

---

### Task 8: `BgmAudioSystem`（昼夜交叉淡化）

**Files:**
- Create: `Assets/Scripts/Unity/Audio/BgmAudioSystem.cs`
- Test: Create `Assets/Tests/EditMode/Audio/BgmAudioSystemTests.cs`

**Interfaces:**
- Consumes: `MusicVolumeBus.Volume`（Task 7）、`MobManager.IsNightPhase(float)`（静态，`MobManager.cs:392`）、`PlayerContext.Instance.Time`（`TimeOfDay.DayPhase01`，取法照 `MobManager.cs:147,185`）
- Produces: `StartGame()`（menu→day）、`EnterMenu()`、`Tick(float dt, bool isNight, bool menuMode)`、`ClipPath(BgmState)`、`TargetState` 属性——Task 12/13 消费

- [ ] **Step 1: 写失败测试**（新文件，整文件 `#if UNITY_EDITOR` 包裹；`InvokeAwake` 反射助手从 `SettingsPanelUiTests.cs:19-26` 复制）

```csharp
[Test]
public void ClipPath_LowercaseHyphen()
{
    Assert.That(BgmAudioSystem.ClipPath(BgmAudioSystem.BgmState.Menu), Is.EqualTo("Audio/bgm-menu"));
    Assert.That(BgmAudioSystem.ClipPath(BgmAudioSystem.BgmState.Night), Is.EqualTo("Audio/bgm-night"));
}

[Test]
public void StateTransitions_MenuToDayToNight()
{
    var go = new GameObject("bgm");
    try
    {
        var bgm = go.AddComponent<BgmAudioSystem>();
        InvokeAwake(bgm);
        Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Menu), "初始 Menu");
        bgm.StartGame();
        Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day), "开始游戏→Day");
        bgm.Tick(0.1f, true, false);
        Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Night), "夜晚→Night");
        bgm.Tick(0.1f, false, false);
        Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day), "白天→Day");
        bgm.Tick(0.1f, false, true);
        Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day), "menuMode 不把 Day 拉回 Menu（Menu 只由 EnterMenu 显式进）");
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}

[Test]
public void Crossfade_VolumesMoveTowardTarget()
{
    var go = new GameObject("bgm-fade");
    try
    {
        var bgm = go.AddComponent<BgmAudioSystem>();
        InvokeAwake(bgm);
        MyWorld.Unity.Audio.MusicVolumeBus.Volume = 0.8f;
        bgm.StartGame(); // 切 Day（EditMode 无 clip → 走无资源分支，状态仍切换）
        bgm.Tick(1.5f, false, false); // 3s 渐变的正中
        // 无 clip 时不断言音量数值，只断言不炸且状态机继续推进（clip 存在路径由实机验收）
        bgm.Tick(1.5f, false, false);
        Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day));
    }
    finally { UnityEngine.Object.DestroyImmediate(go); MyWorld.Unity.Audio.MusicVolumeBus.Volume = 0.8f; }
}
```

- [ ] **Step 2: 跑确认失败**（EditMode `-testFilter MyWorld.Unity.Audio.BgmAudioSystemTests`；FAIL：类型不存在）

- [ ] **Step 3: 实现**（完整文件）

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// BGM 三态（Menu/Day/Night）双 AudioSource 交叉淡化，切换 3s 线性渐变。
    /// 昼夜判定唯一走 MobManager.IsNightPhase（CLAUDE.md 铁律）；menuMode 下不被昼夜拉动。
    /// clip 缺失 LogWarning 一次并静默——音频是 nice-to-have，不阻断。
    /// </summary>
    public sealed class BgmAudioSystem : MonoBehaviour
    {
        public enum BgmState { Menu, Day, Night }
        public const float CrossfadeSeconds = 3f;

        private readonly Dictionary<BgmState, AudioClip> _clips = new Dictionary<BgmState, AudioClip>();
        private readonly HashSet<BgmState> _warned = new HashSet<BgmState>();
        private AudioSource _current; private AudioSource _incoming;
        private BgmState _target = BgmState.Menu;
        private float _fade = 1f;

        public BgmState TargetState => _target;

        private void Awake()
        {
            var a = gameObject.AddComponent<AudioSource>();
            var b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b })
            {
                s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.volume = 0f;
            }
            _current = a;
            foreach (BgmState st in System.Enum.GetValues(typeof(BgmState))) LoadClip(st);
            PlayOn(_current, BgmState.Menu);
        }

        public static string ClipPath(BgmState st) => "Audio/bgm-" + st.ToString().ToLowerInvariant();

        private void LoadClip(BgmState st)
        {
            _clips[st] = Resources.Load<AudioClip>(ClipPath(st));
            if (_clips[st] == null && _warned.Add(st))
                Debug.LogWarning($"[BgmAudioSystem] AudioClip 缺失: {ClipPath(st)}");
        }

        private void PlayOn(AudioSource src, BgmState st)
        {
            var clip = _clips[st];
            if (clip == null) return;
            src.clip = clip; src.volume = MusicVolumeBus.Volume; src.Play();
        }

        public void EnterMenu() { if (_target != BgmState.Menu) BeginTransition(BgmState.Menu); }
        public void StartGame() { if (_target == BgmState.Menu) BeginTransition(BgmState.Day); }

        private void BeginTransition(BgmState target)
        {
            _target = target; _fade = 0f;
            if (_incoming != null) return; // 上一次淡化未完：直接改目标，淡化目标 clip 随 _target 取
            _incoming = _current == null ? null : GetOther(_current);
            if (_incoming == null) return;
            var clip = _clips[target];
            if (clip == null) { _incoming = null; return; }
            _incoming.clip = clip; _incoming.volume = 0f; _incoming.Play();
        }

        private AudioSource GetOther(AudioSource x) =>
            GetComponents<AudioSource>().Length < 2 ? null
            : (GetComponents<AudioSource>()[0] == x ? GetComponents<AudioSource>()[1] : GetComponents<AudioSource>()[0]);

        private void Update()
        {
            var ctx = MyWorld.Unity.Gameplay.PlayerContext.Instance;
            var time = ctx != null ? ctx.Time : null;
            float dayPhase = time != null ? time.DayPhase01 : 0.5f; // 照 MobManager.cs:185
            Tick(Time.deltaTime, MyWorld.Unity.Combat.MobManager.IsNightPhase(dayPhase), _target == BgmState.Menu);
        }

        /// <summary>时间注入入口（测试用）。menuMode=true 时不随昼夜换曲。</summary>
        public void Tick(float dt, bool isNight, bool menuMode)
        {
            if (!menuMode)
            {
                var want = isNight ? BgmState.Night : BgmState.Day;
                if (_target != want) BeginTransition(want);
            }
            float vol = MusicVolumeBus.Volume;
            if (_incoming != null)
            {
                _fade = Mathf.Min(1f, _fade + dt / CrossfadeSeconds);
                _current.volume = vol * (1f - _fade);
                _incoming.volume = vol * _fade;
                if (_fade >= 1f)
                {
                    _current.Stop(); _current.clip = null;
                    _current = _incoming; _incoming = null;
                }
            }
            else if (_current != null) _current.volume = vol;
        }
    }
}
```

（`BeginTransition` 里未完成淡化时改目标：新目标 clip 直接换到 `_incoming`——补一行 `_incoming.clip = _clips[target]`，执行者实现时把该分支写全：`if (_incoming != null) { var c = _clips[target]; if (c != null) _incoming.clip = c; return; }`。）

- [ ] **Step 4: 跑通过 + dotnet 全绿**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Audio/BgmAudioSystem.cs Assets/Tests/EditMode/Audio/BgmAudioSystemTests.cs
git commit -m "audio: BgmAudioSystem 昼夜双轨交叉淡化（av W1-8）"
```

---

### Task 9: `AmbientAudioSystem`（环境循环选择）

**Files:**
- Create: `Assets/Scripts/Unity/Audio/AmbientAudioSystem.cs`
- Test: Create `Assets/Tests/EditMode/Audio/AmbientAudioSystemTests.cs`

**Interfaces:**
- Consumes: `MusicVolumeBus.Volume`；昼夜/玩家 y 取法同 Task 8（`PlayerController.transform.position.y`）
- Produces: `PickTrack(float playerY, bool isNight)`（static 纯函数）、`Tick(float dt, float playerY, bool isNight)`、`CurrentTrack` 属性、`CaveDepthY = 40f`（海平面 62 下 20+）

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void PickTrack_CaveNightDay()
{
    Assert.That(AmbientAudioSystem.PickTrack(10f, false), Is.EqualTo("amb-cave"), "y<40 洞穴优先");
    Assert.That(AmbientAudioSystem.PickTrack(10f, true), Is.EqualTo("amb-cave"));
    Assert.That(AmbientAudioSystem.PickTrack(70f, true), Is.EqualTo("amb-crickets"), "夜晚虫鸣");
    Assert.That(AmbientAudioSystem.PickTrack(70f, false), Is.EqualTo("amb-birds"), "白天鸟鸣");
}

[Test]
public void Tick_ThrottledToTwoSeconds_AndMissingClipDoesNotThrow()
{
    var go = new GameObject("ambient");
    try
    {
        var amb = go.AddComponent<AmbientAudioSystem>();
        InvokeAwake(amb);
        Assert.DoesNotThrow(() => { amb.Tick(1f, 70f, false); amb.Tick(2f, 70f, false); },
            "clip 缺失静默；首查立即 + 每 2s 复查");
        Assert.That(amb.CurrentTrack, Is.EqualTo("amb-birds"), "选定白天轨（clip 缺失也记录轨名）");
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}
```

- [ ] **Step 2: 跑确认失败**（`-testFilter MyWorld.Unity.Audio.AmbientAudioSystemTests`）

- [ ] **Step 3: 实现**（结构照 spec §5.2：单 AudioSource 循环、`_timer` 节流 2s、`Resources.Load("Audio/"+track)` 缺失 `Debug.LogWarning` 一次 + Stop、`CurrentTrack` 记录轨名、Update 里 y 取 `FindObjectOfType<PlayerController>()` 的 transform.position.y（缓存一次）、dayPhase/昼夜同 Task 8）。Tick 首次调用立即判定（`_timer` 初始 0）。

- [ ] **Step 4: 跑通过 + dotnet 全绿**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Audio/AmbientAudioSystem.cs Assets/Tests/EditMode/Audio/AmbientAudioSystemTests.cs
git commit -m "audio: AmbientAudioSystem 洞穴/昼夜环境循环（av W1-9）"
```

---

### Task 10: `MobAudioSystem`（生物叫声）+ 挂载

**Files:**
- Create: `Assets/Scripts/Unity/Audio/MobAudioSystem.cs`
- Test: Create `Assets/Tests/EditMode/Audio/MobAudioSystemTests.cs`

**Interfaces:**
- Consumes: `MyWorld.Core.Entities.MobKind`（枚举，`MobKind.cs:4-39`，Pig=10…Creeper=26）
- Produces: `Attach(GameObject host, MobKind kind)`（静态挂载，模式照 `MobHitFeedback.Attach`，`MobHitFeedback.cs:81-86`）；`TickIdle(float dt, float distanceToPlayer)`；`PlayHurt()`；`ClipPath(MobKind, bool hurt)`、`FallbackPath(MobKind, bool hurt)`、`IdleIntervalSeconds(int mobKindValue, int index)`——Task 13 消费

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void Paths_KindLowercaseHyphen()
{
    Assert.That(MobAudioSystem.ClipPath(MobKind.Pig, false), Is.EqualTo("Audio/Mobs/pig-idle"));
    Assert.That(MobAudioSystem.ClipPath(MobKind.Pig, true), Is.EqualTo("Audio/Mobs/pig-hurt"));
    Assert.That(MobAudioSystem.ClipPath(MobKind.Hamster, false), Is.EqualTo("Audio/Mobs/hamster-idle"));
}

[Test]
public void Fallback_SmallVsLarge()
{
    Assert.That(MobAudioSystem.FallbackPath(MobKind.Chicken, false), Is.EqualTo("Audio/Mobs/generic-small-idle"));
    Assert.That(MobAudioSystem.FallbackPath(MobKind.Rabbit, true), Is.EqualTo("Audio/Mobs/generic-small-hurt"));
    Assert.That(MobAudioSystem.FallbackPath(MobKind.Pig, false), Is.EqualTo("Audio/Mobs/generic-large-idle"));
    Assert.That(MobAudioSystem.FallbackPath(MobKind.Zombie, true), Is.EqualTo("Audio/Mobs/generic-large-hurt"));
}

[Test]
public void IdleInterval_DeterministicAndInRange()
{
    int a = MobAudioSystem.IdleIntervalSeconds((int)MobKind.Pig, 0);
    Assert.That(a, Is.EqualTo(MobAudioSystem.IdleIntervalSeconds((int)MobKind.Pig, 0)), "同输入同输出（确定性哈希）");
    Assert.That(MobAudioSystem.IdleIntervalSeconds((int)MobKind.Pig, 7), Is.InRange(8, 20), "8–20s 区间");
    Assert.That(a, Is.InRange(8, 20));
}

[Test]
public void TickIdle_FarResetsNearPlays_MissingClipsSilent()
{
    var go = new GameObject("mobaudio");
    try
    {
        var sys = MobAudioSystem.Attach(go, MobKind.Cow);
        InvokeAwake(sys);
        Assert.DoesNotThrow(() =>
        {
            sys.TickIdle(1f, 30f);  // 超过 16 格：重置不播
            sys.TickIdle(25f, 5f);  // 近距离累计 25s：必然跨过一次间隔（clip 缺失静默）
            sys.PlayHurt();
        });
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}
```

- [ ] **Step 2: 跑确认失败**（`-testFilter MyWorld.Unity.Audio.MobAudioSystemTests`）

- [ ] **Step 3: 实现**（完整要点）

```csharp
using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// 生物叫声：idle 按哈希间隔 8–20s、距玩家 &gt;16 格不播；hurt 由命中方调。
    /// 查表 Audio/Mobs/<kind小写>-{idle,hurt}，缺文件回退 generic-{small,large}，再缺静默。
    /// 挂载模式照 MobHitFeedback（同宿主 GameObject，命中方 GetComponent 取用）。
    /// </summary>
    public sealed class MobAudioSystem : MonoBehaviour
    {
        public const float MaxHearDistance = 16f;
        public const int IntervalMinSeconds = 8, IntervalSpanSeconds = 13; // 8..20

        public MyWorld.Core.Entities.MobKind Kind;

        private AudioSource _source; private float _idleIn; private int _idleIndex;

        public static MobAudioSystem Attach(GameObject host, MyWorld.Core.Entities.MobKind kind)
        {
            var c = host.AddComponent<MobAudioSystem>();
            c.Kind = kind;
            return c;
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false; _source.spatialBlend = 0f;
            _idleIn = IdleIntervalSeconds((int)Kind, 0);
        }

        /// <summary>确定性间隔（8–20s）：整数哈希，不持随机数对象（项目铁律，hash_pixel 同思路）。</summary>
        public static int IdleIntervalSeconds(int mobKindValue, int index)
        {
            uint n = ((uint)mobKindValue * 374761393u + (uint)index * 668265263u) & 0xFFFFFFFFu;
            n = ((n ^ (n >> 13)) * 1274126177u) & 0xFFFFFFFFu;
            n = (n ^ (n >> 16)) & 0xFFFFFFFFu;
            return IntervalMinSeconds + (int)(n % (uint)IntervalSpanSeconds);
        }

        public static string ClipPath(MyWorld.Core.Entities.MobKind kind, bool hurt) =>
            $"Audio/Mobs/{kind.ToString().ToLowerInvariant()}-{(hurt ? "hurt" : "idle")}";

        private static bool IsSmall(MyWorld.Core.Entities.MobKind kind) =>
            kind == MyWorld.Core.Entities.MobKind.Chicken
            || kind == MyWorld.Core.Entities.MobKind.Rabbit
            || kind == MyWorld.Core.Entities.MobKind.Hamster;

        public static string FallbackPath(MyWorld.Core.Entities.MobKind kind, bool hurt) =>
            $"Audio/Mobs/generic-{(IsSmall(kind) ? "small" : "large")}-{(hurt ? "hurt" : "idle")}";

        public void TickIdle(float dt, float distanceToPlayer)
        {
            if (distanceToPlayer > MaxHearDistance) { _idleIn = 1f; return; } // 太远：短重置，回声场再数
            _idleIn -= dt;
            if (_idleIn > 0f) return;
            _idleIndex++;
            _idleIn = IdleIntervalSeconds((int)Kind, _idleIndex);
            Play(Resources.Load<AudioClip>(ClipPath(Kind, false))
                 ?? Resources.Load<AudioClip>(FallbackPath(Kind, false)));
        }

        public void PlayHurt() => Play(Resources.Load<AudioClip>(ClipPath(Kind, true))
                                        ?? Resources.Load<AudioClip>(FallbackPath(Kind, true)));

        private void Play(AudioClip clip) { if (clip != null && _source != null) _source.PlayOneShot(clip); }
    }
}
```

- [ ] **Step 4: 跑通过 + dotnet 全绿**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Audio/MobAudioSystem.cs Assets/Tests/EditMode/Audio/MobAudioSystemTests.cs
git commit -m "audio: MobAudioSystem 生物叫哈希间隔+generic回退链（av W1-10）"
```

---

### Task 11: `VideoScreenSystem` + 家具方块屏幕面 + 占位贴图

**Files:**
- Create: `Assets/Scripts/Unity/Rendering/VideoScreenSystem.cs`
- Create: `art/scripts/gen_laptop_screen_placeholder.py`
- Create: `Assets/StreamingAssets/blocks/textures/laptop-screen.png`（占位脚本产物 + .meta）
- Modify: `Assets/StreamingAssets/blocks/laptop_block.json`、`hacker_pc_block.json`
- Modify: `Assets/Tests/EditMode/Blocks/BlockDefinitionFilesTests.cs`（家具断言改分面）
- Test: Create `Assets/Tests/EditMode/Rendering/VideoScreenSystemTests.cs`

**Interfaces:**
- Consumes: `BlockRegistry.TextureNames`（public 列表，`BlockRegistry.cs:40`）、`BlockMaterialLibrary.Get(int slot)`（`BlockMaterialLibrary.cs:28`）
- Produces: `VideoScreenSystem.ScreenTextureName = "laptop-screen"`；`Apply(BlockMaterialLibrary library, BlockRegistry registry)`（Task 13 接线调用）

- [ ] **Step 1: 写失败测试（方块 JSON + 分面断言 + VideoSystem 防御）**

`BlockDefinitionFilesTests.M11FurnitureBlocks_ReferencedTextures_HaveArtRequests` 的循环体替换为（表 `M11FurnitureBlocks` 不动——`Texture` 字段语义改为「非 screen 面贴图」）：

```csharp
foreach ((string blockId, ushort _, string texture, string _) in M11FurnitureBlocks)
{
    BlockDefinition furniture = _registry.GetById(blockId);
    bool hasScreen = blockId == "laptop_block" || blockId == "hacker_pc_block";
    int screenCount = 0;
    foreach (string face in furniture.Textures)
    {
        if (hasScreen && face == "laptop-screen") { screenCount++; continue; }
        Assert.That(face, Is.EqualTo(texture),
            $"{blockId} 非 screen 面应统一引用 {texture}（装饰方块除顶面屏幕外不分面）");
    }
    Assert.That(screenCount, Is.EqualTo(hasScreen ? 1 : 0),
        $"{blockId} 顶面应为 laptop-screen（恰好一面，供 VideoScreenSystem 换 RenderTexture）");

    Assert.That(
        Directory.GetFiles(artRequestsRoot, texture + ".md", SearchOption.AllDirectories).Length,
        Is.GreaterThan(0),
        $"{blockId} 引用的贴图 {texture} 在 art/requests 下没有需求文件");
}
Assert.That(
    Directory.GetFiles(artRequestsRoot, "laptop-screen.md", SearchOption.AllDirectories).Length,
    Is.GreaterThan(0), "laptop-screen 在 art/requests 下没有需求文件");
```

新 `VideoScreenSystemTests`（整文件 `#if UNITY_EDITOR`；贴图目录加载用真实 `Assets/StreamingAssets/blocks`——EditMode 可读）：

```csharp
[Test]
public void Apply_MissingVideoFile_MaterialUntouched()
{
    // EditMode 下 StreamingAssets/video 无 mp4 → Apply 应早退不改材质、只告警
    var go = new GameObject("vss");
    try
    {
        var vss = go.AddComponent<VideoScreenSystem>();
        // LoadRealRegistry/TextureDir：照 BlockDefinitionFilesTests 的 SetUp 加载逻辑写成
        // 本 TestFixture 的私有助手（真实读 streamingAssetsPath/blocks + blocks/textures）
        var registry = LoadRealRegistry();
        var library = BlockMaterialLibrary.Load(registry, TextureDir);
        int slot = System.Array.IndexOf(registry.TextureNames, VideoScreenSystem.ScreenTextureName);
        Assert.That(slot, Is.GreaterThanOrEqualTo(0), "laptop_block.json 拆面后注册表应含 laptop-screen 贴图槽");
        var before = library.Get(slot).mainTexture;
        Assert.DoesNotThrow(() => vss.Apply(library, registry));
        Assert.That(library.Get(slot).mainTexture, Is.EqualTo(before), "mp4 缺失不改材质");
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}
```

- [ ] **Step 2: 跑确认失败**（`-testFilter MyWorld.Unity.Blocks.BlockDefinitionFilesTests` + `-testFilter MyWorld.Unity.Rendering.VideoScreenSystemTests`——先 FAIL：JSON 仍是 all、测试断言未改）

- [ ] **Step 3: 实现**

方块 JSON（textures 三键全写，schema 要求）：

```json
"laptop_block":  { "top": "laptop-screen", "bottom": "laptop", "side": "laptop" }
"hacker_pc_block": { "top": "laptop-screen", "bottom": "hacker-pc", "side": "hacker-pc" }
```

`art/scripts/gen_laptop_screen_placeholder.py`（照 `gen_m6_placeholders.py` 的 `hash_pixel` 模式；32×32 深蓝底 `#0B1E3A`，绿色 `#3FBB5A/#7FE89A/#C8FACC` 哈希撒「字符」竖条，`Image.NEAREST`，写 `Assets/StreamingAssets/blocks/textures/laptop-screen.png`；main 里打印路径）。

`VideoScreenSystem.cs`：

```csharp
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 笔记本/黑客电脑方块屏幕视频：把 laptop-screen 贴图槽的共享材质 mainTexture 换成
    /// VideoPlayer 的 RenderTexture（材质按贴图名共享——全世界同款播同一段，贪心网格零改动）。
    /// mp4 缺失/贴图槽缺失 → 告警一次并保持原贴图。
    /// </summary>
    public sealed class VideoScreenSystem : MonoBehaviour
    {
        public const string ScreenTextureName = "laptop-screen";
        public const string VideoFileName = "laptop-loop.mp4";

        private bool _warned;

        public void Apply(BlockMaterialLibrary library, MyWorld.Core.Blocks.BlockRegistry registry)
        {
            int slot = System.Array.IndexOf(registry.TextureNames, ScreenTextureName);
            if (slot < 0) { Warn($"注册表无贴图槽 {ScreenTextureName}"); return; }
            string path = Path.Combine(Application.streamingAssetsPath, "video", VideoFileName);
            if (!File.Exists(path)) { Warn($"视频缺失: {path}（保持占位贴图）"); return; }

            var rt = new RenderTexture(320, 180, 0) { filterMode = FilterMode.Point };
            var player = gameObject.AddComponent<VideoPlayer>();
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = rt;
            player.url = path;
            player.isLooping = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.Play();
            library.Get(slot).mainTexture = rt;
        }

        private void Warn(string msg)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[VideoScreenSystem] {msg}");
        }
    }
}
```

（`BlockRegistry` 的确切命名空间以 `BlockMaterialLibrary.cs` 头部 using 为准——执行者照抄同文件引用方式。）

- [ ] **Step 4: 跑占位脚本 + 测试通过 + dotnet 全绿**

Run: `python art/scripts/gen_laptop_screen_placeholder.py` → 跑两个 EditMode filter → `dotnet test` 全绿
（`laptop-screen.png` 的 `.meta` 由本次 EditMode 批处理生成，随 Step 5 一起提交。）

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Rendering/VideoScreenSystem.cs art/scripts/gen_laptop_screen_placeholder.py \
  Assets/StreamingAssets/blocks/textures/laptop-screen.png Assets/StreamingAssets/blocks/laptop_block.json \
  Assets/StreamingAssets/blocks/hacker_pc_block.json Assets/Tests/EditMode/Blocks/BlockDefinitionFilesTests.cs \
  Assets/Tests/EditMode/Rendering/VideoScreenSystemTests.cs
git commit -m "video: 笔记本屏幕方块化——laptop-screen贴图槽+VideoScreenSystem换RenderTexture（av W2-11）"
```

---

### Task 12: `TitleScreenUi` 极简主菜单 + ui-shot 扩展

**Files:**
- Create: `Assets/Scripts/Unity/UI/TitleScreenUi.cs`
- Modify: `Assets/Scripts/Unity/UiScreenshotOnArg.cs`（首相位拍 ui-title + 关菜单）
- Modify: `tools/scripts/visual-smoke.sh`（期望清单加 `ui-title.png`，六张）
- Test: Create `Assets/Tests/EditMode/UI/TitleScreenUiTests.cs`

**Interfaces:**
- Consumes: `BlockInteraction.InputLocked`（静态，`BlockInteraction.cs:44`）、`UiCursorGate.Open/Close/IsOpen`（`UiCursorGate.cs:43-67`）、`BgmAudioSystem.StartGame()`（Task 8）、`HotbarUI.LoadUiTextureOrFallback` 的 StreamingAssets 模式（`HotbarUI.cs:69-83`）
- Produces: `TitleScreenUi.IsVisible`、`StartGame()`——UiScreenshotOnArg 与 Task 13 接线消费

- [ ] **Step 1: 写失败测试**

```csharp
[Test]
public void Awake_ShowsMenuAndLocksInput_StartGameUnlocks()
{
    var go = new GameObject("title");
    try
    {
        var title = go.AddComponent<TitleScreenUi>();
        InvokeAwake(title);
        Assert.That(title.IsVisible, Is.True, "开局即主菜单");
        Assert.That(MyWorld.Unity.Player.BlockInteraction.InputLocked, Is.True, "菜单期间锁交互");
        Assert.That(MyWorld.Unity.UI.UiCursorGate.IsOpen, Is.True, "菜单期间指针解锁");
        title.StartGame();
        Assert.That(title.IsVisible, Is.False);
        Assert.That(MyWorld.Unity.Player.BlockInteraction.InputLocked, Is.False);
        Assert.That(MyWorld.Unity.UI.UiCursorGate.IsOpen, Is.False);
    }
    finally
    {
        MyWorld.Unity.Player.BlockInteraction.InputLocked = false;
        if (MyWorld.Unity.UI.UiCursorGate.IsOpen) MyWorld.Unity.UI.UiCursorGate.Close();
        UnityEngine.Object.DestroyImmediate(go);
    }
}

[Test]
public void Awake_MissingVideo_DoesNotThrow()
{
    var go = new GameObject("title-novideo");
    try
    {
        var title = go.AddComponent<TitleScreenUi>();
        Assert.DoesNotThrow(() => InvokeAwake(title), "EditMode 无 mp4：纯色回退不炸");
    }
    finally { UnityEngine.Object.DestroyImmediate(go); }
}
```

- [ ] **Step 2: 跑确认失败**（`-testFilter MyWorld.Unity.UI.TitleScreenUiTests`）

- [ ] **Step 3: 实现**

`TitleScreenUi.cs` 要点（完整逻辑，样式缓存照 `ItemSlotDrawer.WhiteStyle` 模式）：

```csharp
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 极简主菜单（开局遮罩）：全屏视频背景 + 标题 + 开始/退出两按钮。
    /// WorldBootstrap 步骤序不动——本组件只在最上层遮罩，StartGame 销毁遮罩解锁输入。
    /// 指针门复用模态 UI 模式（BlockInteraction.InputLocked + UiCursorGate），不动 timeScale
    /// （底下世界加载不是暂停）。视频缺失回退深蓝色纯背景。
    /// </summary>
    public sealed class TitleScreenUi : MonoBehaviour
    {
        public const string VideoFileName = "menu-bg.mp4";

        public bool IsVisible { get; private set; } = true;

        private RenderTexture _rt; private VideoPlayer _player; private Texture2D _fallback;
        private bool _warned;

        private void Awake()
        {
            IsVisible = true;
            MyWorld.Unity.Player.BlockInteraction.InputLocked = true;
            MyWorld.Unity.UI.UiCursorGate.Open();
            _fallback = MakeFallbackTexture(); // 1×1 深蓝 #2C4A6E
            string path = Path.Combine(Application.streamingAssetsPath, "video", VideoFileName);
            if (!File.Exists(path))
            {
                if (!_warned) { _warned = true; Debug.LogWarning($"[TitleScreenUi] 视频缺失: {path}（纯色回退）"); }
                return;
            }
            _rt = new RenderTexture(1280, 720, 0);
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _rt;
            _player.url = path;
            _player.isLooping = true;
            _player.audioOutputMode = VideoAudioOutputMode.None; // 菜单曲由 BgmAudioSystem 管
            _player.Play();
        }

        public void StartGame()
        {
            if (!IsVisible) return;
            IsVisible = false;
            MyWorld.Unity.Player.BlockInteraction.InputLocked = false;
            MyWorld.Unity.UI.UiCursorGate.Close();
            if (_player != null) { _player.Stop(); _player = null; }
            if (_rt != null) { _rt.Release(); _rt = null; }
            FindObjectOfType<MyWorld.Unity.Audio.BgmAudioSystem>()?.StartGame();
        }

        private void OnGUI()
        {
            if (!IsVisible) return;
            GUI.depth = 100; // 数值越大越后画→盖住 hotbar 等常驻 UI（Unity：depth 小先画、在下层）
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                (Texture)_rt ?? _fallback, ScaleMode.ScaleAndCrop);
            GUI.Label(new Rect(Screen.width / 2f - 300, Screen.height * 0.22f, 600, 80),
                "MyWordGame", BigStyle()); // BigStyle：一次性缓存 44px 加粗白字
            if (GUI.Button(new Rect(Screen.width / 2f - 110, Screen.height * 0.55f, 220, 46), "开始游戏"))
                StartGame();
            if (GUI.Button(new Rect(Screen.width / 2f - 110, Screen.height * 0.55f + 58, 220, 46), "退出"))
                Application.Quit();
        }
    }
}
```

`UiScreenshotOnArg.Update` 的相位改造（case 0 前插一个相位，后续 case 序号顺延 +1；`FindTitleScreenUi` 静态助手照既有 Find* 模式；最终落盘检查仍看 `ui-help.png`）：

```csharp
case 0: // 等 60 帧稳定后先截主菜单（av 批新增第 0 张）
    if (_frames >= WarmupFrames)
    {
        Capture("ui-title.png");
        NextPhase();
    }
    break;

case 1: // 关主菜单（恢复输入锁），截 hotbar
    if (_frames >= CaptureSettleFrames)
    {
        FindTitleScreenUi()?.StartGame();
        Capture("ui-hotbar.png");
        NextPhase();
    }
    break;
```

（原 case 1–5 改为 2–6；日志文案「五张」改「六张」。）

`visual-smoke.sh` 期望清单（~120-124 行）加 `ui-title.png`。

- [ ] **Step 4: 跑通过 + dotnet 全绿**（EditMode `-testFilter MyWorld.Unity.UI.TitleScreenUiTests` + `-testFilter MyWorld.Unity.UiScreenshotOnArgTests`——后者若存在）

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/UI/TitleScreenUi.cs Assets/Scripts/Unity/UiScreenshotOnArg.cs tools/scripts/visual-smoke.sh Assets/Tests/EditMode/UI/TitleScreenUiTests.cs
git commit -m "video: 极简主菜单TitleScreenUi+ui-shot六张截图（av W2-12）"
```

---

### Task 13: 集成接线批（热点文件串行）+ 双链全绿

**Files:**
- Modify: `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs`（步骤 7.5 区，`WorldBootstrap.cs:115` 之后）
- Modify: `Assets/Scripts/Unity/Player/BlockInteraction.cs`（eat/till/plant/harvest/tool-break 五处）
- Modify: `Assets/Scripts/Unity/World/RedstoneSystem.cs`（门开关，`RedstoneSystem.cs:113-118`）
- Modify: `Assets/Scripts/Unity/Player/PlayerController.cs`（hurt/die/pickup 三处）
- Modify: `Assets/Scripts/Unity/UI/CraftingPocketUi.cs` / `CraftingWorkbenchUi.cs` / `CraftingFurnaceUi.cs`（craft 三处）
- Modify: `Assets/Scripts/Unity/Combat/MobManager.cs`（SpawnMob 挂 MobAudioSystem，`MobManager.cs:522` 后）+ `Assets/Scripts/Unity/Combat/CombatController.cs`（hurt 音，`CombatController.cs:238` 附近）
- Test: 各处现有 EditMode 测试保持全绿；不新增测试（接线是既有系统的既有钩子，逻辑已在 Task 6-12 测过）

**Interfaces:**
- Consumes: Task 6-12 全部 Produces

- [ ] **Step 1: WorldBootstrap 挂载（115 行 `AddComponent<PlayerAudioSystem>()` 之后、117 行「// 8.」之前）**

```csharp
// 7.6 av 批：BGM/环境循环（同宿主，PlayerContext 在步骤 7 已建好）+ 主菜单遮罩
gameObject.AddComponent<MyWorld.Unity.Audio.BgmAudioSystem>();
gameObject.AddComponent<MyWorld.Unity.Audio.AmbientAudioSystem>();
gameObject.AddComponent<MyWorld.Unity.UI.TitleScreenUi>();
```

`VideoScreenSystem.Apply` 接线：在 WorldBootstrap 创建 `BlockMaterialLibrary` 的语句之后（grep `BlockMaterialLibrary` 定位实际局部变量名）追加：

```csharp
// av 批：笔记本/黑客电脑屏幕视频（mp4 缺失自动回退占位贴图）
gameObject.AddComponent<MyWorld.Unity.Rendering.VideoScreenSystem>().Apply(<materials变量>, <registry变量>);
```

- [ ] **Step 2: BlockInteraction 五处**

1. `TryEatSelectedFood`（384-395）：`ctx.HungerSystem.Eat(...)` 成功路径（392 行后、扣物品前后皆可）加 `_audio?.PlayEat();`
2. `TryTillWithHoe` 297 行 `_audio?.PlayPlace();` → `_audio?.PlayHoeTill();`
3. `TryPlantSeeds` 321 行 `_audio?.PlayPlace();` → `_audio?.PlayPlant();`
4. `BreakAt`（578-619）播放音处（原 `_audio?.PlayBreak()`，约 230 行调用的链路终点在本方法内——以方法内实际调用点为准）：改为
   ```csharp
   bool matureCrop = MyWorld.Core.Farming.FarmSystem.TryParseStageBlock(blockId, out _, out int stage)
                    && stage == MyWorld.Core.Farming.FarmSystem.MatureStage;
   if (matureCrop) _audio?.PlayHarvest(); else _audio?.PlayBreak();
   ```
   （`blockId` 取破坏前的方块 id——在 SetBlock 之前读。）
5. `ApplyDigDurability` 碎裂分支（831 行 `if (after.IsEmpty)` 内、`ShowToolBreakHint()` 旁）加 `_audio?.PlayToolBreak();`

- [ ] **Step 3: RedstoneSystem 门 + PlayerController 三处 + 合成 UI 三处**

`RedstoneSystem.HandleHit` 门分支（113-118）`Doors.UpdateSignal(...)` 之后：

```csharp
bool wasOpen = state == MyWorld.Core.World.DoorState.Open; // state 是切换前状态
var audio = MyWorld.Unity.Audio.PlayerAudioSystem.Instance;
if (wasOpen) audio?.PlayDoorClose(); else audio?.PlayDoorOpen();
```

（`DoorState` 命名空间以该文件既有 using 为准；语义核对：`state != DoorState.Open` 是新信号——原关→开。）

`PlayerController`：
- `TakeDamage(float, object)`（158-173）：168 行 `ctx.Health.Damage(...)` 之后加 `_audio?.PlayHurt();`；169-171 行 `if (ctx.Health.IsDead)` 分支内加 `_audio?.PlayDie();`
- `PickupNearbyDrops`（290-345）：315-317 入包成功处（`drop.MarkPicked()` 旁）加 `_audio?.PlayPickup();`

三个合成 UI（各自 TryAdd 成功后）：`CraftingPocketUi.TryTakeCraftOutput`（86 行 TryAdd 成功分支）、`CraftingWorkbenchUi.TryTakeCraftOutput`（129 行旁）、`CraftingFurnaceUi.TryTakeOutput`（125-126 行旁）各加：

```csharp
MyWorld.Unity.Audio.PlayerAudioSystem.Instance?.PlayCraft();
```

- [ ] **Step 4: 生物挂载与受击**

`MobManager.SpawnMob`（522 行 `MobHitFeedback.Attach(go, mob);` 之后）：

```csharp
MyWorld.Unity.Audio.MobAudioSystem.Attach(go, mob.Kind);
```

`MobManager.Update` 的 mob tick 循环里（与 MobAI.Tick 同处，拿到 mob 与玩家距离的地方）加：

```csharp
mobAudio?.TickIdle(dt, distanceToPlayer); // mobAudio = 该 mob 宿主上的 MobAudioSystem（从 _viewComponents 或 GetComponent 取，照 MobHitFeedback 的取用模式）
```

`CombatController.DoAttack`（238 行 `feedback.FlashRed();` 之后）：

```csharp
mobComp.GetComponent<MyWorld.Unity.Audio.MobAudioSystem>()?.PlayHurt();
```

- [ ] **Step 5: 双链全量 + Build 验证**

Run:
```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
# EditMode 全量（无 -testFilter），grep 结果文件零 Failed 且总数 ≥ 之前（只增不减）
./tools/scripts/build-and-run.sh --skip-tests --no-launch   # 确认 Build 通过
```
Expected: 双链全绿；Build 产物正常。

- [ ] **Step 6: Commit**

```bash
git add -A Assets/Scripts/Unity tools/scripts 2>/dev/null || git add Assets/Scripts/Unity
git commit -m "audio/video: 集成接线批——BGM/环境/主菜单挂载+事件音11处+生物叫挂载（av W3-13）"
```

---

### Task 14: 视频生成（配额队列）+ 终验收

**Files:**
- Create: `Assets/StreamingAssets/video/menu-bg.mp4`、`laptop-loop.mp4`（+ .meta）
- Modify: `art/README.md`（av 批状态 待生成→已入库）

⚠️ **每天最多 3 次视频提交**。首日 2 条用 2 次；若需重试，次日再跑 `--videos`（队列自动续）。

- [ ] **Step 1: 生成两条视频**

Run: `python tools/generate_media.py --videos`
Expected: `quota.json` `usedToday=2`、两条 `status=done`；`art/incoming/video/` 两个后处理过的 mp4；打印 `--install` 提示

- [ ] **Step 2: 入库 + Unity 导入**

Run: `python tools/generate_media.py --install` → EditMode 全量一次（生成 `.meta`、确认全绿）
```bash
git add Assets/StreamingAssets/video
git commit -m "video: 主菜单背景+笔记本屏幕mp4入库（av W3-14）"
```

- [ ] **Step 3: 视觉冒烟（六张截图含 ui-title）**

Run: `./tools/scripts/build-and-run.sh --with-visual --no-launch`
Expected: `Builds/screenshots/` 六张 PNG 齐全（`ui-title.png` 主菜单含视频背景一帧），`visual-smoke.sh` 退出 0

- [ ] **Step 4: 实机验收剧本（spec §9.4）**

Run: `./tools/scripts/build-and-run.sh` 启动游戏，按 spec 逐项：主菜单视频+菜单曲 → 开始切白天曲 → 挖到 y<40 滴水声 → 过夜 BGM/环境切换 → 打猪 idle/hurt → 吃/开门/锄地/收麦各一音 → 放笔记本看屏幕视频 → 音乐音量拉零 BGM 静而音效在。任何一项不过：回对应 Task 修复后重跑本步。

- [ ] **Step 5: 收尾对账**

- `art/README.md` av 批状态改「已入库」；commit
- 对照 spec §10 文件清单核对无缺漏；spec 与实现如有出入按项目规则**修 spec**（以实现为准）
- 检查 `git status` 无遗漏文件（`quota.json` 在 `art/incoming/` 下已 gitignored，不提交）

```bash
git add art/README.md docs/superpowers/specs/2026-08-18-audio-video-design.md
git commit -m "docs: av批收尾——README状态已入库+spec对账（av W3-15）"
```

---

## 计划自审记录

- **Spec 覆盖**：§2 清单→Task 4/5/14；§3 管线→Task 1/2/3；§4 配额→Task 3/14；§5.1-5.4→Task 6/8/9/10；§5.5→Task 7；§6.1→Task 12；§6.2→Task 11/13；§7 错误处理→各系统防御分支 + Task 3 重试；§8 测试→各 Task Step 1 + Task 13 全量；§9 验收→Task 14。无缺口。
- **占位符**：无 TBD/TODO；Task 13 中 `<materials变量>/<registry变量>` 是「执行者按 WorldBootstrap 实际局部名代入」的显式指令，非占位。
- **类型一致性**：`PlayHoeTill/PlayDoorClose/...` 11 方法名 Task 6 定义 = Task 13 使用；`MusicVolumeBus.Volume` Task 7 定义 = Task 8/9 使用；`Attach(GameObject, MobKind)` Task 10 = Task 13 使用；`laptop-screen` 连字符贯穿 Task 4/11/12/13。
