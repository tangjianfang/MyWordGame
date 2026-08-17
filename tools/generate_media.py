#!/usr/bin/env python3
"""
音频 + 视频生成入口。

需求文件是提示词的「唯一来源」（仿 generate_art.py 风格）：

    art/requests/audio/{bgm,ambient,events,mobs}/*.md   ← 31 条音频
    art/requests/video/{menu-bg,laptop-loop}.md        ← 2 条视频

每个 .md 里的 `### <名>` 小节是单条资源，节内首个围栏代码块是提示词，
`键: 值` 行（含 `- 键: 值` 列表项）进 params。脚本只解析、不存元信息；
跑过 `--audio` / `--videos` 后产物落 `art/incoming/`：
    音频：`art/incoming/audio/<名>.ogg`（API 出 mp3 → ffmpeg 后处理成 ogg）
    视频：`art/incoming/video/<名>.mp4`（Hailuo-2.3 直接出 mp4 → ffmpeg 转码）

入库走 `--install`：音频拷 `Assets/Resources/Audio/`，视频拷
`Assets/StreamingAssets/video/`。ffmpeg / ffprobe 走 PATH，需本机已装。

API key 只从环境变量或仓库根 .env（`MINIMAX_API_KEY`）读，绝不写进代码。

视频每日配额（Token Plan：3 次/天）：状态机落
`art/incoming/video/quota.json`，重试也占当日次数。

用法：
    python tools/generate_media.py --self-test
    python tools/generate_media.py --list
    python tools/generate_media.py --audio [--only <名>...]
    python tools/generate_media.py --videos
    python tools/generate_media.py --install
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

# ──────────────────────────────────────────────────────────── 配置


PROJECT_ROOT = Path(__file__).resolve().parent.parent
API_BASE = "https://api.minimaxi.com"

REQUESTS_ROOT = PROJECT_ROOT / "art" / "requests"
INCOMING_AUDIO = PROJECT_ROOT / "art" / "incoming" / "audio"
INCOMING_VIDEO = PROJECT_ROOT / "art" / "incoming" / "video"
QUOTA_PATH = INCOMING_VIDEO / "quota.json"

AUDIO_INSTALL_DIR = PROJECT_ROOT / "Assets" / "Resources" / "Audio"
VIDEO_INSTALL_DIR = PROJECT_ROOT / "Assets" / "StreamingAssets" / "video"

CROSSFADE_SECONDS = 1.5


# ──────────────────────────────────────────────────────────── .env


def load_dotenv() -> None:
    """照抄 generate_art.py 的 .env 解析（路径可能带引号）。"""
    env = PROJECT_ROOT / ".env"
    if not env.exists():
        return
    for raw in env.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        k, v = line.split("=", 1)
        os.environ.setdefault(k.strip(), v.strip().strip("\"'"))


# ──────────────────────────────────────────────────────────── Entry


@dataclass
class Entry:
    """单条需求条目（音频/视频通用）。

    Attributes:
        name: 资源名 = 产物文件名（bgm-day / door-open / pig-idle / menu-bg）
        prompt: 提示词正文（围栏代码块内）
        params: 中文键值对（循环/响度/声道/模板/时长 等）
        source: 原始 .md 路径（出错时报错用）
        kind: bgm | ambient | sfx | mob | video
    """

    name: str = ""
    prompt: str = ""
    params: dict = field(default_factory=dict)
    source: Path = None  # type: ignore
    kind: str = "sfx"

    @property
    def media(self) -> str:
        """audio 或 video——由 kind 推导。"""
        return "video" if self.kind == "video" else "audio"


# ──────────────────────────────────────────────────────────── 解析


# 相对路径第一段 → kind 映射
_KIND_BY_DIR = {
    ("audio", "bgm"): "bgm",
    ("audio", "ambient"): "ambient",
    ("audio", "events"): "sfx",
    ("audio", "mobs"): "mob",
    ("video",): "video",
}


def parse_entries(root: Path) -> list[Entry]:
    """从 art/requests/{audio,video}/.../*.md 解析所有 Entry。

    规则：
        - rglob("*.md") 找所有需求文件
        - 按 `### <名>` 切小节
        - 节内首个围栏代码块是提示词（其余围栏忽略）
        - `键: 值` 与 `- 键: 值` 行进 params（剥前导 "- "）
        - kind 由目录第一段推导
    """
    out: list[Entry] = []
    if not root.exists():
        return out
    for md in sorted(root.rglob("*.md")):
        try:
            rel = md.relative_to(root).parts
        except ValueError:
            continue
        if len(rel) < 2:
            continue
        key = (rel[0],) if rel[0] == "video" else (rel[0], rel[1])
        if key not in _KIND_BY_DIR:
            continue
        kind = _KIND_BY_DIR[key]

        current: Entry = None  # type: ignore
        in_fence = False
        buf: list[str] = []
        for line in md.read_text(encoding="utf-8").splitlines():
            if line.startswith("```"):
                # 切换围栏状态：进入/离开代码块。**关闭**时把累积内容写进 prompt。
                if in_fence and current is not None and not current.prompt:
                    current.prompt = "\n".join(buf).strip()
                in_fence = not in_fence
                if not in_fence:
                    buf = []
                continue
            if in_fence:
                buf.append(line)
                continue
            if line.startswith("### "):
                current = Entry(name=line[4:].strip(), source=md, kind=kind)
                out.append(current)
            elif current is not None and ":" in line and not line.startswith("#"):
                k, v = line.lstrip("- ").split(":", 1)
                if k.strip() and v.strip():
                    current.params[k.strip()] = v.strip()
    return [e for e in out if e.prompt]


# ──────────────────────────────────────────────────────────── Task 2: 音频生成


def api_key() -> str:
    """从环境/.env 拿 API key；缺失 sys.exit 拒绝静默调用。"""
    load_dotenv()
    key = os.environ.get("MINIMAX_API_KEY")
    if not key:
        sys.exit("缺 MINIMAX_API_KEY（环境变量或 .env）")
    return key


def post_json(path: str, payload: dict, timeout: int = 600) -> dict:
    """POST JSON 到 MiniMax API，断言 base_resp.status_code == 0。"""
    req = urllib.request.Request(
        API_BASE + path,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "Authorization": "Bearer " + api_key(),
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    code = data.get("base_resp", {}).get("status_code", -1)
    if code != 0:
        raise RuntimeError(
            f"{path} 失败 status_code={code} "
            f"msg={data.get('base_resp', {}).get('status_msg')}"
        )
    return data


def build_music_payload(prompt: str) -> dict:
    """构造 music-2.6 文生音乐请求（纯函数）。"""
    return {
        "model": os.environ.get("MINIMAX_MUSIC_MODEL", "music-2.6"),
        "prompt": prompt[:2000],
        "is_instrumental": True,
        "output_format": "hex",
        "audio_setting": {
            "sample_rate": 44100, "bitrate": 256000, "format": "mp3",
        },
    }


def generate_music_mp3(entry: Entry) -> Path:
    """调 /v1/music_generation，hex 落盘 art/incoming/audio/<名>.mp3。

    4 次重试 8s 指数退避（照抄 generate_art.generate_one 模式），1002 限流也重试。
    """
    INCOMING_AUDIO.mkdir(parents=True, exist_ok=True)
    dst = INCOMING_AUDIO / f"{entry.name}.mp3"
    last: Exception | None = None
    for attempt in range(4):
        try:
            data = post_json("/v1/music_generation", build_music_payload(entry.prompt))
            if data.get("data", {}).get("status") != 2:
                raise RuntimeError(
                    f"music status={data.get('data', {}).get('status')}（1=合成中）"
                )
            dst.write_bytes(bytes.fromhex(data["data"]["audio"]))
            return dst
        except (urllib.error.URLError, RuntimeError, TimeoutError, OSError) as e:
            last = e
            time.sleep(8 * (attempt + 1))
    raise RuntimeError(f"{entry.name} 生成失败：{last}")


# ──────────────────────────────────────────────────────────── ffmpeg 后处理


def run_ff(args: list[str]) -> None:
    """跑 ffmpeg 命令，捕获 stderr，失败抛 CalledProcessError。"""
    subprocess.run(
        ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args],
        check=True, capture_output=True,
    )


def probe_duration(path: Path) -> float:
    """ffprobe 取音频时长（秒）。"""
    out = subprocess.run(
        [
            "ffprobe", "-v", "error", "-show_entries", "format=duration",
            "-of", "csv=p=0", str(path),
        ],
        check=True, capture_output=True, text=True,
    )
    return float(out.stdout.strip())


# 裁头尾静音 + 反向再裁一次（处理尾静音）
TRIM_AF = (
    "silenceremove=start_periods=1:start_threshold=-45dB,"
    "areverse,silenceremove=start_periods=1:start_threshold=-45dB,areverse"
)


def postprocess_audio(raw_mp3: Path, out_dir: Path, entry: Entry) -> Path:
    """裁静音 → 响度归一 → （循环类）首尾交叉淡化 → ogg 44.1k。

    时长不达标抛 RuntimeError。
    """
    out_dir.mkdir(parents=True, exist_ok=True)
    trimmed = out_dir / "_trim.wav"
    dst = out_dir / f"{entry.name}.ogg"
    lufs = int(entry.params.get("响度", "-14"))
    run_ff([
        "-i", str(raw_mp3),
        "-af", f"{TRIM_AF},loudnorm=I={lufs}:TP=-1.5:LRA=11",
        str(trimmed),
    ])
    dur = probe_duration(trimmed)
    mono = entry.params.get("声道", "mono") == "mono"
    if entry.params.get("循环") == "是":
        if dur < float(entry.params.get("时长下限", "15")):
            raise RuntimeError(
                f"{entry.name} 太短 {dur:.1f}s"
                f"（要求 ≥{entry.params.get('时长下限', 15)}s）"
            )
        cross = min(CROSSFADE_SECONDS, dur / 4)
        looped = out_dir / "_loop.wav"
        # 写法 B：头段 atrim=0..dur-cross + 尾段 atrim=end=cross，
        # 各自 asetpts 重置时间戳后再 acrossfade —— 输出长度 = dur - cross ≈ 原时长，
        # 写法 A（各自 atrim 头尾再 af）会输出 dur - 2*cross，丢了时长。
        run_ff([
            "-i", str(trimmed),
            "-filter_complex",
            f"[0:a]atrim=end={cross:.3f},asetpts=PTS-STARTPTS[t];"
            f"[0:a]atrim=0:{dur - cross:.3f},asetpts=PTS-STARTPTS[h];"
            f"[h][t]acrossfade=d={cross:.3f}:c1=tri:c2=tri[o]",
            "-map", "[o]", str(looped),
        ])
        run_ff([
            "-i", str(looped),
            "-ar", "44100",
            "-ac", "1" if mono else "2",
            "-c:a", "libvorbis", "-q:a", "5",
            str(dst),
        ])
    else:
        target = float(entry.params.get("目标时长", "1.2"))
        seg = out_dir / "_seg.wav"
        run_ff([
            "-i", str(trimmed),
            "-t", f"{target:.2f}",
            "-af",
            "afade=t=in:st=0:d=0.008,"
            f"afade=t=out:st={max(0.0, target - 0.02):.3f}:d=0.02",
            str(seg),
        ])
        got = probe_duration(seg)
        if not (0.3 <= got <= 3.0):
            raise RuntimeError(f"{entry.name} SFX 段长 {got:.2f}s 不在 0.3–3s")
        run_ff([
            "-i", str(seg),
            "-ar", "44100",
            "-ac", "1" if mono else "2",
            "-c:a", "libvorbis", "-q:a", "5",
            str(dst),
        ])
    return dst


# ──────────────────────────────────────────────────────────── SFX 兜底合成


# 确定性合成模板（lavfi 滤镜，零 API）
SYNTH_TEMPLATES = {
    "thud": [
        "-f", "lavfi", "-i", "sine=frequency=110:duration=0.35",
        "-af", "afade=t=out:st=0.05:d=0.3:curve=exp,volume=0.9",
    ],
    "pop": [
        "-f", "lavfi", "-i", "sine=frequency=520:duration=0.18",
        "-af", "afade=t=out:st=0.02:d=0.16:curve=exp,volume=0.8",
    ],
    "click": [
        "-f", "lavfi", "-i", "anoisesrc=color=white:duration=0.09:seed=42",
        "-af", "highpass=f=1800,afade=t=out:st=0:d=0.09:curve=exp,volume=0.7",
    ],
    "chime": [
        "-f", "lavfi", "-i", "sine=frequency=880:duration=0.5",
        "-af", "afade=t=out:st=0.05:d=0.45:curve=exp,volume=0.7",
    ],
    "swoosh": [
        "-f", "lavfi", "-i", "anoisesrc=color=pink:duration=0.5:seed=7",
        "-af", "lowpass=f=900,afade=t=in:st=0:d=0.2,afade=t=out:st=0.25:d=0.25,volume=0.8",
    ],
}


def synth_sfx(template: str, dst: Path, mono: bool = True) -> Path:
    """用 lavfi 模板合成一段短 SFX（零 API 调用、确定性）。"""
    if template not in SYNTH_TEMPLATES:
        raise ValueError(f"未知模板 {template}（可选：{list(SYNTH_TEMPLATES)}）")
    run_ff([
        *SYNTH_TEMPLATES[template],
        "-ar", "44100",
        "-ac", "1" if mono else "2",
        "-c:a", "libvorbis", "-q:a", "5",
        str(dst),
    ])
    return dst


# ──────────────────────────────────────────────────────────── --audio 主流程


def _process_entry(entry: Entry) -> Path | None:
    """处理单条音频 Entry：已存在跳过；失败按 kind 走兜底或跳过。

    Returns: 产物路径（.ogg）；None = 跳过。
    """
    out_dir = INCOMING_AUDIO
    out_dir.mkdir(parents=True, exist_ok=True)
    dst = out_dir / f"{entry.name}.ogg"
    if dst.exists():
        print(f"  [跳过] {entry.name} 已存在")
        return dst

    try:
        raw = generate_music_mp3(entry)
        postprocess_audio(raw, out_dir, entry)
        print(f"  [AI OK] {entry.name}")
        return dst
    except Exception as e:
        if entry.kind == "sfx":
            tpl = entry.params.get("模板", "click")
            print(f"  [兜底] {entry.name} AI 段不合格，走合成模板 {tpl}：{e}")
            synth_sfx(tpl, dst, mono=entry.params.get("声道", "mono") == "mono")
            return dst
        elif entry.kind == "mob":
            print(f"  [跳过] {entry.name} mob 生成失败（Unity 回退 generic）：{e}")
            return None
        else:
            print(f"  [失败] {entry.name}：{e}")
            return None


def cmd_audio(only: list[str] | None) -> int:
    """--audio 主流程：遍历所有音频 Entry，生成/后处理/兜底。"""
    entries = [e for e in parse_entries(REQUESTS_ROOT) if e.kind != "video"]
    if only:
        entries = [e for e in entries if e.name in only]
        missing = [n for n in only if not any(e.name == n for e in parse_entries(REQUESTS_ROOT))]
        if missing:
            print(f"[警告] --only 指定但未找到：{missing}")
    if not entries:
        print("（无音频需求文件）")
        return 0
    print(f"生成 {len(entries)} 条音频…")
    ok = fail = skip = 0
    for e in entries:
        result = _process_entry(e)
        if result is None:
            fail += 1
        elif result.exists():
            ok += 1
        else:
            skip += 1
    print(f"\n完成：OK {ok} / 失败 {fail} / 跳过 {skip}")
    print(f"入库跑: python tools/generate_media.py --install")
    return 0 if fail == 0 else 1


def cmd_install() -> int:
    """--install：把 art/incoming/{audio,video}/ 拷到 Unity 资源目录。"""
    n = 0
    if INCOMING_AUDIO.exists():
        AUDIO_INSTALL_DIR.mkdir(parents=True, exist_ok=True)
        for src in INCOMING_AUDIO.glob("*.ogg"):
            shutil.copy2(src, AUDIO_INSTALL_DIR / src.name)
            n += 1
        print(f"[audio] 拷 {n} 条到 {AUDIO_INSTALL_DIR}")
    if INCOMING_VIDEO.exists():
        VIDEO_INSTALL_DIR.mkdir(parents=True, exist_ok=True)
        n2 = 0
        for src in INCOMING_VIDEO.glob("*.mp4"):
            shutil.copy2(src, VIDEO_INSTALL_DIR / src.name)
            n2 += 1
        print(f"[video] 拷 {n2} 条到 {VIDEO_INSTALL_DIR}")
        n += n2
    if n == 0:
        print("（art/incoming/ 为空，没东西可拷）")
    return 0


# ──────────────────────────────────────────────────────────── 入口（CLI）


def _cmd_list(args: argparse.Namespace) -> int:
    """打印所有条目（名 / kind / media / params / 提示词前 60 字符）。"""
    entries = parse_entries(REQUESTS_ROOT)
    if not entries:
        print(f"（无需求文件）根：{REQUESTS_ROOT}")
        return 0
    for e in entries:
        params = ", ".join(f"{k}={v}" for k, v in e.params.items())
        snippet = e.prompt.replace("\n", " ")[:60]
        print(f"[{e.kind:7s}] {e.name:20s} {e.media:5s} | {params}")
        print(f"          prompt: {snippet}{'...' if len(e.prompt) > 60 else ''}")
    print(f"\n共 {len(entries)} 条")
    return 0


# ──────────────────────────────────────────────────────────── self-test


def self_test() -> None:
    """Task 1 骨架测试：解析器 + Entry 基本行为。"""
    import tempfile

    with tempfile.TemporaryDirectory() as td:
        root = Path(td)
        (root / "audio" / "bgm").mkdir(parents=True)
        (root / "audio" / "bgm" / "bgm.md").write_text(
            "# BGM\n\n### bgm-day\n白天\n\n## AI 提示词\n```\nGentle piano.\n```\n\n"
            "## 参数\n- 循环: 是\n- 响度: -18\n- 声道: stereo\n- 时长下限: 15\n\n"
            "### bgm-night\n夜\n\n## AI 提示词\n```\nQuiet piano.\n```\n\n## 参数\n- 循环: 是\n",
            encoding="utf-8",
        )
        (root / "video").mkdir(parents=True)
        (root / "video" / "menu-bg.md").write_text(
            "# 视频\n\n### menu-bg\n\n## AI 提示词\n```\nVoxel landscape.\n```\n\n## 参数\n- 时长: 6\n",
            encoding="utf-8",
        )

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

    # ── Task 2 扩展 ──
    # build_music_payload：纯函数断言
    payload = build_music_payload("Gentle piano.")
    assert payload["model"] == "music-2.6" and payload["is_instrumental"] is True
    assert payload["output_format"] == "hex"
    assert payload["audio_setting"] == {
        "sample_rate": 44100, "bitrate": 256000, "format": "mp3"
    }

    # postprocess_audio：用 lavfi 造 3s 正弦 mp3 当输入，断言产出 ogg 且双声道
    with tempfile.TemporaryDirectory() as td:
        raw = Path(td) / "raw.mp3"
        subprocess.run(
            [
                "ffmpeg", "-y", "-f", "lavfi", "-i",
                "sine=frequency=440:duration=3", "-b:a", "64k", str(raw),
            ],
            check=True, capture_output=True,
        )
        entry = Entry(
            name="t-bgm", kind="bgm",
            params={"循环": "是", "响度": "-18", "声道": "stereo", "时长下限": "1"},
        )
        dst = postprocess_audio(raw, Path(td), entry)
        assert dst.exists() and dst.suffix == ".ogg", dst
        dur = probe_duration(dst)
        assert 0.9 < dur < 4.0, dur

        # 兜底合成模板零 API 可用
        sfx = synth_sfx("thud", Path(td) / "thud.ogg", mono=True)
        assert sfx.exists() and probe_duration(sfx) < 1.5

    print("self-test OK")


# ──────────────────────────────────────────────────────────── main


def main() -> int:
    parser = argparse.ArgumentParser(description="音频 + 视频生成入口")
    parser.add_argument("--self-test", action="store_true", help="跑 self-test 后退出")
    parser.add_argument("--list", action="store_true", help="列出所有需求条目")
    parser.add_argument("--audio", action="store_true", help="生成音频（music-2.6 + ffmpeg）")
    parser.add_argument("--only", nargs="*", default=None, help="只跑指定名（用于重试单条）")
    parser.add_argument("--videos", action="store_true", help="生成视频（Hailuo-2.3，配额队列）")
    parser.add_argument("--install", action="store_true", help="拷贝产物入库")
    args = parser.parse_args()

    if args.self_test:
        self_test()
        return 0
    if args.list:
        return _cmd_list(args)
    if args.audio:
        return cmd_audio(args.only)
    if args.videos:
        print("（--videos 在 Task 3 实现）")
        return 0
    if args.install:
        return cmd_install()
    parser.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())