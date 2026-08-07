#!/usr/bin/env python3
"""
调 MiniMax 文生图接口，按 art/requests/ 里已经写好的提示词批量生成素材。

提示词的唯一来源是需求文件本身——脚本从每个 .md 的「## AI 提示词」段落里解析
围栏代码块，不在这里重复维护一份，改需求文件即刻生效。

产物落 art/incoming/<资源名>.png，正好是 tools/postprocess_art.py 的输入，
两个脚本共用 postprocess_art.ASSETS 这一份资源清单，尺寸和名字不会对不上。

API Key 只从环境变量或仓库根目录的 .env 读取，绝不写进代码或仓库。

用法（PowerShell）：
    $env:MINIMAX_API_KEY = "你的key"

    python tools/generate_art.py --list              # 看每个资源的提示词解析情况
    python tools/generate_art.py --dry-run --only leaves
    python tools/generate_art.py --only leaves glass cobblestone
    python tools/generate_art.py --all               # 已存在的自动跳过
    python tools/generate_art.py --all --force --jobs 4
    python tools/generate_art.py --only leaves --n 4 # 一次出 4 张备选，挑一张

可调环境变量：
    MINIMAX_API_KEY     必填
    MINIMAX_GROUP_ID    选填
    MINIMAX_IMAGE_MODEL 默认 image-01
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import io
import json
import os
import re
import sys
import threading
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from math import gcd
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from postprocess_art import ASSETS, INCOMING, PROJECT_ROOT  # noqa: E402

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

REQUESTS_DIR = PROJECT_ROOT / "art" / "requests"
MANIFEST = INCOMING / "manifest.json"
ENDPOINT = "https://api.minimaxi.com/v1/image_generation"

PROMPT_MAX_CHARS = 1500          # 接口上限
GEN_LONG_SIDE = 1024             # 生成边长，再由后处理最近邻降采样
SIZE_MIN, SIZE_MAX = 512, 2048   # 接口允许的宽高范围

# 需求文件名 → 该文件里唯一那段提示词对应的资源名（文件名与资源名不同时才需要写）
FILE_DEFAULT = {
    "break-stages": "break-4",
    "heart": "heart-full",
    "button": "button-normal",
    "title-logo": "title-decor-block",
}

# 小标题里写的名字 → 实际产物名
ALIASES = {"moon-phases": "moon-full"}

# ores.md 只写了一段模板，四张靠替换 COLOR_DESC 行区分
ORE_COLOR_LINES = {
    "coal-ore": ("These blobs are pure dark charcoal black: #0F0F0F, #1F1F1F, "
                 "#3A3A3A only. They must NOT be red, NOT brown, NOT warm-toned — "
                 "think black coal lumps, absolutely no reddish or orange tint "
                 "anywhere."),
    "iron-ore": ("These blobs are warm rusty iron brown: #8A6A4C, #B9906B, "
                 "#D8B896 only. Muted earthy brown, not orange, not red, not gray."),
    "gold-ore": ("These blobs are rich metallic gold yellow: #A87322, #DCAE3A, "
                 "#F7DA7A only. Warm bright yellow-gold, not pale yellow, not brown."),
    "diamond-ore": ("These blobs are cyan teal diamond: #1E7C7C, #4CC6C4, #A8F2EF "
                    "only. Cool cyan-green, not blue, not purple."),
}

C_OK, C_WARN, C_ERR, C_DIM, C_END = "\033[32m", "\033[33m", "\033[31m", "\033[90m", "\033[0m"


# ---------------------------------------------------------------- 配置读取


def load_dotenv() -> None:
    env = PROJECT_ROOT / ".env"
    if not env.exists():
        return
    for raw in env.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        k, v = line.split("=", 1)
        os.environ.setdefault(k.strip(), v.strip().strip("\"'"))


# ---------------------------------------------------------------- 生成目标


def targets() -> dict[str, tuple[int, int]]:
    """产出文件名 → 目标像素尺寸。矿石只生成洋红底上的矿脉层。"""
    out: dict[str, tuple[int, int]] = {}
    for name, spec in ASSETS.items():
        if spec.kind == "ai":
            out[name] = spec.size
        elif spec.kind == "ore":
            out[f"{name}-blobs"] = spec.size
    return out


def gen_size(size: tuple[int, int]) -> tuple[int, int]:
    """
    按目标图的宽高比放大到生成尺寸：保持比例精确（避免降采样时被拉伸），
    两边都落在 [512, 2048] 且是 8 的倍数。
    """
    tw, th = size
    g = gcd(tw, th)
    a, b = tw // g, th // g
    best = None
    for m in range(1, SIZE_MAX // max(a, b) + 1):
        w, h = a * m, b * m
        if w % 8 or h % 8 or min(w, h) < SIZE_MIN:
            continue
        # 先满足「长边尽量接近 GEN_LONG_SIDE」，长边不够时以短边达标为准
        score = abs(max(w, h) - GEN_LONG_SIDE)
        if best is None or score < best[0]:
            best = (score, w, h)
    if best is None:
        raise ValueError(f"{size} 的宽高比无法映射到接口允许的尺寸")
    return best[1], best[2]


# ---------------------------------------------------------------- 提示词解析


def _blocks_in_section(lines: list[str]) -> list[tuple[str | None, str]]:
    """返回该段落内的 (最近的三级标题, 代码块内容) 列表。"""
    out: list[tuple[str | None, str]] = []
    sub: str | None = None
    buf: list[str] | None = None
    for line in lines:
        if line.startswith("```"):
            if buf is None:
                buf = []
            else:
                out.append((sub, "\n".join(buf).strip()))
                buf = None
            continue
        if buf is not None:
            buf.append(line)
        elif line.startswith("### "):
            sub = line[4:].strip()
    return out


def parse_prompts() -> tuple[dict[str, str], list[str]]:
    """扫描需求文件，返回 (资源名 → 提示词, 无法映射的告警)。"""
    prompts: dict[str, str] = {}
    warnings: list[str] = []
    known = set(targets())

    for md in sorted(REQUESTS_DIR.rglob("*.md")):
        lines = md.read_text(encoding="utf-8").splitlines()
        start = next((i for i, ln in enumerate(lines)
                      if ln.startswith("## ") and "AI 提示词" in ln
                      and "不作为最终产出" not in ln), None)
        if start is None:
            continue
        end = next((i for i in range(start + 1, len(lines))
                    if lines[i].startswith("## ")), len(lines))
        blocks = _blocks_in_section(lines[start + 1:end])
        rel = md.relative_to(PROJECT_ROOT).as_posix()

        if md.stem == "ores":                      # 一段模板派生四张
            if not blocks:
                warnings.append(f"{rel}：AI 提示词段落里没有代码块")
                continue
            template = blocks[0][1]
            for ore, colors in ORE_COLOR_LINES.items():
                swapped, n = re.subn(r"^COLOR_DESC$", colors.replace("\\", "\\\\"),
                                     template, count=1, flags=re.M)
                if n == 0:
                    warnings.append(f"{rel}：模板里找不到 COLOR_DESC 行，无法派生 {ore}")
                    continue
                prompts[f"{ore}-blobs"] = swapped
            continue

        for sub, body in blocks:
            m = re.search(r"`([a-z0-9][a-z0-9-]*)`", sub or "")
            name = m.group(1) if m else FILE_DEFAULT.get(md.stem, md.stem)
            name = ALIASES.get(name, name)
            if name not in known:
                warnings.append(f"{rel}：提示词「{sub or md.stem}」对应的 {name} 不在生成清单里，已忽略")
                continue
            if name in prompts:
                warnings.append(f"{rel}：{name} 出现了多段提示词，只用第一段")
                continue
            prompts[name] = body
    return prompts, warnings


# ---------------------------------------------------------------- 调接口


def request_images(prompt: str, size: tuple[int, int], n: int,
                   seed: int | None, api_key: str) -> list[bytes]:
    body = {
        "model": os.environ.get("MINIMAX_IMAGE_MODEL", "image-01"),
        "prompt": prompt,
        "width": size[0],
        "height": size[1],
        "response_format": "base64",
        "n": n,
        "prompt_optimizer": False,   # 提示词是逐字调过的，不能让服务端改写
    }
    if seed is not None:
        body["seed"] = seed

    url = ENDPOINT
    group = os.environ.get("MINIMAX_GROUP_ID")
    if group:
        url = f"{ENDPOINT}?GroupId={group}"

    req = urllib.request.Request(
        url, data=json.dumps(body).encode("utf-8"), method="POST",
        headers={"Content-Type": "application/json",
                 "Authorization": f"Bearer {api_key}"})
    with urllib.request.urlopen(req, timeout=300) as resp:
        payload = json.loads(resp.read().decode("utf-8"))

    code = (payload.get("base_resp") or {}).get("status_code", 0)
    if code:
        raise RuntimeError(f"status_code={code} {(payload.get('base_resp') or {}).get('status_msg', '')}")
    b64 = ((payload.get("data") or {}).get("image_base64")) or []
    if not b64:
        raise RuntimeError(f"返回里没有图像数据：{json.dumps(payload)[:200]}")
    return [base64.b64decode(s) for s in b64]


def save_png(raw: bytes, path: Path) -> tuple[int, int]:
    """接口返回的是 JPEG，转存成 PNG，免得文件名与内容不符。"""
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path, "PNG")
    return img.size


# ---------------------------------------------------------------- 驱动

_print_lock = threading.Lock()
_manifest_lock = threading.Lock()


def log(msg: str) -> None:
    with _print_lock:
        print(msg, flush=True)


def load_manifest() -> dict:
    if not MANIFEST.exists():
        return {}
    try:
        return json.loads(MANIFEST.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}


def generate_one(name: str, prompt: str, size: tuple[int, int], args,
                 api_key: str, manifest: dict) -> str:
    out = INCOMING / f"{name}.png"
    if out.exists() and not args.force:
        log(f"  {C_DIM}跳过 {name}（已存在，--force 可覆盖）{C_END}")
        return "skip"

    gw, gh = gen_size(size)
    delay = 8
    for attempt in range(1, 5):
        try:
            images = request_images(prompt, (gw, gh), args.n, args.seed, api_key)
            break
        except (urllib.error.URLError, RuntimeError, TimeoutError, OSError) as e:
            if attempt == 4:
                log(f"  {C_ERR}失败 {name}：{str(e)[:120]}{C_END}")
                return "fail"
            log(f"  {C_WARN}重试 {name}（{str(e)[:70]}），{delay}s 后第 {attempt + 1} 次{C_END}")
            time.sleep(delay)
            delay *= 2

    for i, raw in enumerate(images):
        path = out if i == 0 else INCOMING / f"{name}-alt{i}.png"
        w, h = save_png(raw, path)
    with _manifest_lock:
        manifest[name] = {
            "model": os.environ.get("MINIMAX_IMAGE_MODEL", "image-01"),
            "size": [gw, gh],
            "target": list(size),
            "n": args.n,
            "seed": args.seed,
            "prompt_sha1": hashlib.sha1(prompt.encode("utf-8")).hexdigest()[:12],
            "generated_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
        }
        MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2),
                            encoding="utf-8")
    extra = f" +{len(images) - 1} 张备选" if len(images) > 1 else ""
    log(f"  {C_OK}完成 {name}  {w}×{h}{extra}{C_END}")
    return "ok"


def show_list(prompts: dict[str, str], warnings: list[str]) -> int:
    print(f"{'资源':22s}{'目标':11s}{'生成':13s}{'提示词':8s}素材")
    for name, size in targets().items():
        gw, gh = gen_size(size)
        p = prompts.get(name)
        state = f"{len(p)} 字" if p else "缺失"
        exists = "已有" if (INCOMING / f"{name}.png").exists() else "—"
        flag = C_ERR if not p else (C_WARN if len(p) > PROMPT_MAX_CHARS else "")
        print(f"{flag}{name:22s}{f'{size[0]}×{size[1]}':11s}"
              f"{f'{gw}×{gh}':13s}{state:8s}{exists}{C_END if flag else ''}")
    for w in warnings:
        print(f"{C_WARN}  ! {w}{C_END}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="按 art/requests/ 的提示词调 MiniMax 生成素材")
    ap.add_argument("--all", action="store_true", help="生成全部缺失素材")
    ap.add_argument("--only", nargs="+", metavar="NAME", help="只生成指定资源")
    ap.add_argument("--list", action="store_true", help="列出提示词解析结果，不调接口")
    ap.add_argument("--dry-run", action="store_true", help="打印将要发送的请求，不调接口")
    ap.add_argument("--force", action="store_true", help="覆盖已存在的素材")
    ap.add_argument("--jobs", type=int, default=3, help="并发路数，默认 3")
    ap.add_argument("--n", type=int, default=1, help="每个资源出几张备选，1–9")
    ap.add_argument("--seed", type=int, help="随机种子，便于复现")
    args = ap.parse_args()

    load_dotenv()
    prompts, warnings = parse_prompts()

    if args.list:
        return show_list(prompts, warnings)

    all_targets = targets()
    if args.only:
        unknown = [n for n in args.only if n not in all_targets]
        if unknown:
            ap.error(f"未知资源：{', '.join(unknown)}（可用 --list 查看）")
        names = args.only
    elif args.all:
        names = list(all_targets)
    else:
        ap.error("请指定 --all 或 --only NAME（--list 查看清单）")

    missing = [n for n in names if n not in prompts]
    if missing:
        print(f"{C_ERR}以下资源在需求文件里找不到提示词：{', '.join(missing)}{C_END}")
        for w in warnings:
            print(f"{C_WARN}  ! {w}{C_END}")
        return 1

    too_long = [(n, len(prompts[n])) for n in names if len(prompts[n]) > PROMPT_MAX_CHARS]
    if too_long:
        for n, ln in too_long:
            print(f"{C_ERR}{n} 的提示词 {ln} 字，超过接口上限 {PROMPT_MAX_CHARS}，请精简需求文件{C_END}")
        return 1

    if args.dry_run:
        for n in names:
            gw, gh = gen_size(all_targets[n])
            print(f"\n=== {n}  生成 {gw}×{gh} → 目标 {all_targets[n][0]}×{all_targets[n][1]} ===")
            print(prompts[n])
        return 0

    api_key = os.environ.get("MINIMAX_API_KEY")
    if not api_key:
        print(f"{C_ERR}未设置 MINIMAX_API_KEY{C_END}")
        print(f"{C_DIM}PowerShell:  $env:MINIMAX_API_KEY = \"你的key\"{C_END}")
        print(f"{C_DIM}或在仓库根目录 .env 写入：MINIMAX_API_KEY=你的key（.env 已被 .gitignore 忽略）{C_END}")
        return 1
    if not 1 <= args.n <= 9:
        ap.error("--n 取值范围 1–9")

    INCOMING.mkdir(parents=True, exist_ok=True)
    manifest = load_manifest()
    print(f"=== MiniMax 文生图（{os.environ.get('MINIMAX_IMAGE_MODEL', 'image-01')}，"
          f"{len(names)} 项，并发 {args.jobs}）===")

    with ThreadPoolExecutor(max_workers=max(1, args.jobs)) as pool:
        results = list(pool.map(
            lambda n: generate_one(n, prompts[n], all_targets[n], args, api_key, manifest),
            names))

    ok = results.count("ok")
    fail = results.count("fail")
    skip = results.count("skip")
    print(f"\n完成 {ok}，跳过 {skip}，失败 {fail}")
    if ok:
        print(f"{C_DIM}接着跑：python tools/postprocess_art.py --only {' '.join(names)}{C_END}")
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
