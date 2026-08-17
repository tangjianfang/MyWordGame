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
        print("（--audio 在 Task 2 实现）")
        return 0
    if args.videos:
        print("（--videos 在 Task 3 实现）")
        return 0
    if args.install:
        print("（--install 在 Task 2 实现）")
        return 0
    parser.print_help()
    return 0


if __name__ == "__main__":
    sys.exit(main())