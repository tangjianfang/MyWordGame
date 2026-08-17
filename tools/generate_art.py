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
    python tools/generate_art.py --tree              # 12 大类资源树 + 每叶状态
    python tools/generate_art.py --dry-run --only leaves
    python tools/generate_art.py --only leaves glass cobblestone
    python tools/generate_art.py --all               # 已存在的自动跳过
    python tools/generate_art.py --all --force --jobs 4
    python tools/generate_art.py --only leaves --n 4 # 一次出 4 张备选，挑一张

    # 母版程序换色（0 次 API 调用）：调色板映射或色相偏移，产物落 incoming
    python tools/generate_art.py --variants wool --names wool-red,wool-blue \
        --palette "#8C1B1B,#D42B2B;#1A3A7C,#3A5AC4"
    python tools/generate_art.py --dry-run --variants potion-base \
        --names potion-speed,potion-strength  # 只打印计划不写盘

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

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from postprocess_art import ASSETS, H, INCOMING, PROCESSED, PROJECT_ROOT  # noqa: E402

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
    按目标图的宽高比放大到生成尺寸：优先保持比例精确（避免降采样时被拉伸），
    两边都落在 [512, 2048] 且是 8 的倍数；凑不出精确比例时取最接近的一档。
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
        # 精确比例在接口允许范围内凑不出来（m11 的三帧横排 1024×341 就是这种：
        # 341 与 1024 互质，任何 8 的倍数放大都超界）。退而求其次，在允许尺寸里
        # 找宽高比最接近的一档，降采样时轻微校正（1536×512 → 1024×341 纵向仅差 1 像素）
        target = tw / th
        cand = min(((w, h)
                    for w in range(SIZE_MIN, SIZE_MAX + 1, 8)
                    for h in range(SIZE_MIN, SIZE_MAX + 1, 8)),
                   key=lambda wh: (abs(wh[0] / wh[1] - target),
                                   abs(max(wh) - GEN_LONG_SIDE)))
        return cand
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


def parse_prompts() -> tuple[dict[str, str], list[str], dict[str, str]]:
    """扫描需求文件，返回 (资源名 → 提示词, 无法映射的告警, 资源名 → 需求文件相对路径)。

    第三个返回值给 --tree 用：包括没注册进 ASSETS 的名字（树里标 ⬜ 缺注册）。
    """
    prompts: dict[str, str] = {}
    warnings: list[str] = []
    origin: dict[str, str] = {}
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
                origin[f"{ore}-blobs"] = rel
            continue

        for sub, body in blocks:
            # 允许下划线：方块名如 iron_door / redstone_dust / crafting_table-top
            # 都会出现在反引号小标题里，正则必须容纳下划线
            m = re.search(r"`([a-z0-9_][a-z0-9_-]*)`", sub or "")
            name = m.group(1) if m else FILE_DEFAULT.get(md.stem, md.stem)
            name = ALIASES.get(name, name)
            if name not in known:
                # m11 资源名统一用连字符，容错需求文件把连字符写成下划线的笔误
                alt = name.replace("_", "-")
                if alt in known:
                    name = alt
            origin[name] = rel
            if name not in known:
                warnings.append(f"{rel}：提示词「{sub or md.stem}」对应的 {name} 不在生成清单里，已忽略")
                continue
            if name in prompts:
                warnings.append(f"{rel}：{name} 出现了多段提示词，只用第一段")
                continue
            prompts[name] = body
    return prompts, warnings, origin


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


# ---------------------------------------------------------------- 资源树（--tree）

# 12 大类打印顺序：既有六类在前、m11 新六类在后（spec 口径）；
# 出现清单外的新目录时排在最后，不丢
CATEGORY_ORDER = ["blocks", "items", "entities", "player", "sky", "ui",
                  "effects", "scenes", "codex", "architecture", "marketing",
                  "seasonal"]

ST_INSTALLED, ST_INCOMING, ST_REQUEST, ST_MISSING = "已入库", "📥", "📄", "⬜"


def _installed_stems() -> set[str]:
    """Assets 下已入库 PNG 的资源名（含 StreamingAssets 的 ui/items 贴图）。"""
    stems: set[str] = set()
    for base in (PROJECT_ROOT / "Assets" / "Art",
                 PROJECT_ROOT / "Assets" / "StreamingAssets"):
        if base.exists():
            stems.update(p.stem for p in base.rglob("*.png"))
    return stems


def _resource_status(name: str, installed: set[str]) -> str:
    """单资源状态：⬜ 缺注册（名字不在 ASSETS）> 已入库 > 📥 > 📄 仅需求。"""
    if name not in ASSETS:
        return ST_MISSING
    if name in installed:
        return ST_INSTALLED
    if (INCOMING / f"{name}.png").exists() or (INCOMING / f"{name}-blobs.png").exists():
        return ST_INCOMING
    return ST_REQUEST


def show_tree(origin: dict[str, str], warnings: list[str]) -> int:
    """按 art/requests/ 目录树打印：大类 → 子目录 → 需求文件 → 资源状态。"""
    installed = _installed_stems()
    # 矿石的生成名是 <名>-blobs，折回主名显示
    names_by_file: dict[str, list[str]] = {}
    for raw, rel in origin.items():
        name = raw[:-len("-blobs")] if raw.endswith("-blobs") else raw
        names_by_file.setdefault(rel, []).append(name)

    # 目录树：大类 → 子目录（可为 ""）→ (需求文件相对路径, 资源名列表)
    tree: dict[str, dict[str, list[tuple[str, list[str]]]]] = {}
    for md in sorted(REQUESTS_DIR.rglob("*.md")):
        rel = md.relative_to(PROJECT_ROOT).as_posix()
        parts = md.relative_to(REQUESTS_DIR).parts
        cat = parts[0] if len(parts) > 1 else "（散件）"
        sub = "/".join(parts[1:-1])
        tree.setdefault(cat, {}).setdefault(sub, []).append(
            (rel, names_by_file.get(rel, [])))

    def cat_key(n: str) -> tuple[int, str]:
        return (CATEGORY_ORDER.index(n) if n in CATEGORY_ORDER
                else len(CATEGORY_ORDER), n)

    counts = {ST_INSTALLED: 0, ST_INCOMING: 0, ST_REQUEST: 0, ST_MISSING: 0}
    no_prompt: list[str] = []
    total = 0

    print("=== art/requests 美术资源树（大类 → 子目录 → 资源）===")
    for cat in sorted(tree, key=cat_key):
        # 先数这一类的状态给标题行用
        cat_counts = dict.fromkeys(counts, 0)
        cat_total = 0
        for sub in tree[cat]:
            for _, names in tree[cat][sub]:
                cat_total += len(names)
                for n in names:
                    cat_counts[_resource_status(n, installed)] += 1
        head = f"{cat}（{cat_total} 资源：" + " ".join(
            f"{k} {v}" for k, v in cat_counts.items() if v) + "）"
        print(f"\n{head}")
        for sub in sorted(tree[cat]):
            if sub:
                print(f"  {sub}/")
            for rel, names in tree[cat][sub]:
                if not names:
                    # 程序占位批次/纯说明文件：没有「AI 提示词」段落，不参与生成
                    no_prompt.append(rel)
                    print(f"    {rel}  —— 无「AI 提示词」段落（不参与生成）")
                    continue
                print(f"    {rel}")
                for n in names:
                    st = _resource_status(n, installed)
                    counts[st] += 1
                    total += 1
                    print(f"        {st}  {n}")

    print("\n=== 汇总 ===")
    print(f"共 {total} 个资源：已入库 {counts[ST_INSTALLED]} / 📥 {counts[ST_INCOMING]}"
          f" / 📄 {counts[ST_REQUEST]} / ⬜ {counts[ST_MISSING]}")
    if no_prompt:
        print(f"另有 {len(no_prompt)} 份需求文件没有「AI 提示词」段落（程序占位/纯说明，不计入）：")
        for d in no_prompt:
            print(f"    {d}")
    for w in warnings:
        print(f"{C_WARN}  ! {w}{C_END}")
    return 0


# ---------------------------------------------------------------- 母版换色（--variants）


def _luminance(rgb: tuple[int, int, int]) -> float:
    return 0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2]


def _recolor_by_palette(arr: np.ndarray, master: list[tuple[int, int, int]],
                        target: list[tuple[int, int, int]]) -> np.ndarray:
    """
    调色板映射：母版色与目标色都按明度排序后按秩配对（第 i 暗配第 i 暗），
    保留原图明暗结构——染色羊毛/药水变体的标准换色手法。
    母版色未知时退化为整图最近色量化（按明度，不做 RGB 欧氏，避免偏色）。
    """
    out = arr.copy()
    rgb = out[:, :, :3]
    if master:
        src = sorted(master, key=_luminance)
        dst = sorted(target, key=_luminance)
        for i, c in enumerate(src):
            # 母版色多于目标色时按比例归并到目标档位
            t = dst[round(i * (len(dst) - 1) / max(len(src) - 1, 1))]
            rgb[np.all(rgb == np.array(c, dtype=np.uint8), axis=-1)] = t
    else:
        ranks = np.asarray(sorted(target, key=_luminance), dtype=np.uint8)
        flat = rgb.reshape(-1, 3).astype(np.int32)
        lum = flat @ np.array([299, 587, 114], dtype=np.int32)
        order = np.argsort(np.argsort(lum))          # 每像素的明度秩
        slot = (order * (len(ranks) - 1) // max(int(order.max()), 1)
                ).clip(0, len(ranks) - 1)
        rgb[:] = ranks[slot].reshape(rgb.shape)
    return out


def _hue_shifted(img: Image.Image, delta_deg: int) -> np.ndarray:
    """整图色相偏移（PIL 的 HSV 通道 H 是 0–255 刻度），返回 RGB 数组。"""
    hsv = np.array(img.convert("HSV"), dtype=np.int32)
    shift = round(delta_deg * 255 / 360) % 256
    hsv[:, :, 0] = (hsv[:, :, 0] + shift) % 256
    return np.array(Image.fromarray(hsv.astype(np.uint8), "HSV").convert("RGB"))


def run_variants(args) -> int:
    """取 incoming（次选 processed）母版程序换色，输出多张变体——0 次 API 调用。"""
    base = args.variants
    names = [n.strip() for n in (args.names or "").split(",") if n.strip()]
    if not names:
        print(f"{C_ERR}--variants 需要配合 --names 指定变体输出名（逗号分隔）{C_END}")
        return 2
    if base in names:
        print(f"{C_ERR}变体名 {base} 与母版同名，会覆盖母版{C_END}")
        return 2

    src = INCOMING / f"{base}.png"
    src_where = "incoming"
    if not src.exists():
        alt = PROCESSED / f"{base}.png"
        if alt.exists():
            src, src_where = alt, "processed"
    if not src.exists():
        print(f"{C_ERR}母版不存在：art/incoming/{base}.png（processed/ 里也没有），"
              f"先 --only {base} 生成{C_END}")
        return 1

    # --palette：分号分组对应各变体，组内逗号分色；不给则用色相偏移
    groups: list[list[tuple[int, int, int]]] = []
    for chunk in args.palette or []:
        for g in chunk.split(";"):
            colors = [H(c.strip()) for c in g.split(",") if c.strip()]
            if colors:
                groups.append(colors)
    if groups and len(groups) != len(names):
        print(f"{C_ERR}--palette 给了 {len(groups)} 组调色板，与 {len(names)} 个变体名不一致"
              f"（分号分组对应各变体，组内逗号分色）{C_END}")
        return 2

    img = Image.open(src)
    arr = np.array(img.convert("RGBA"), dtype=np.uint8)
    # 母版色：优先用 ASSETS 里登记的调色板（更稳），否则取图内不透明高频色
    master: list[tuple[int, int, int]] = list(ASSETS[base].palette) \
        if base in ASSETS and ASSETS[base].palette else []
    if not master:
        opaque = arr[:, :, :3][arr[:, :, 3] > 0]
        if opaque.size:
            colors, cnt = np.unique(opaque.reshape(-1, 3), axis=0, return_counts=True)
            master = [tuple(int(v) for v in colors[i])
                      for i in np.argsort(-cnt)[:24]]

    print(f"=== 母版 {src.relative_to(PROJECT_ROOT)}（{src_where}）"
          f"→ {len(names)} 个变体，0 次 API 调用 ===")
    for i, name in enumerate(names):
        out_path = INCOMING / f"{name}.png"
        if groups:
            desc = "调色板映射 " + ",".join("#%02X%02X%02X" % c for c in groups[i])
        else:
            # 色相偏移量按序号均分圆周，确定性可复现（不持随机数）
            desc = f"色相偏移 {((i + 1) * (360 // (len(names) + 1))) % 360}°"
        if args.dry_run:
            print(f"  {C_DIM}[dry-run] {name}：{desc} → "
                  f"{out_path.relative_to(PROJECT_ROOT)}{C_END}")
            continue
        if out_path.exists() and not args.force:
            print(f"  {C_DIM}跳过 {name}（已存在，--force 可覆盖）{C_END}")
            continue
        if groups:
            variant = _recolor_by_palette(arr, master, groups[i])
        else:
            delta = ((i + 1) * (360 // (len(names) + 1))) % 360
            variant = arr.copy()
            variant[:, :, :3] = _hue_shifted(img, delta)
        Image.fromarray(variant, "RGBA").save(out_path, "PNG")
        print(f"  {C_OK}完成 {name}（{desc}）{C_END}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="按 art/requests/ 的提示词调 MiniMax 生成素材")
    ap.add_argument("--all", action="store_true", help="生成全部缺失素材")
    ap.add_argument("--only", nargs="+", metavar="NAME", help="只生成指定资源")
    ap.add_argument("--list", action="store_true", help="列出提示词解析结果，不调接口")
    ap.add_argument("--tree", action="store_true",
                    help="按 art/requests/ 目录树打印大类→子目录→资源与每叶状态")
    ap.add_argument("--variants", metavar="BASE",
                    help="取 incoming（次选 processed）母版程序换色出多张变体，0 次 API 调用")
    ap.add_argument("--names", metavar="A,B,...", help="--variants 的变体输出名，逗号分隔")
    ap.add_argument("--palette", action="append", metavar="HEX;HEX;...",
                    help="--variants 的目标调色板：分号分组对应各变体，组内逗号分色；"
                         "不给则按序号均分色相偏移")
    ap.add_argument("--dry-run", action="store_true", help="打印将要发送的请求，不调接口")
    ap.add_argument("--force", action="store_true", help="覆盖已存在的素材")
    ap.add_argument("--jobs", type=int, default=3, help="并发路数，默认 3")
    ap.add_argument("--n", type=int, default=1, help="每个资源出几张备选，1–9")
    ap.add_argument("--seed", type=int, help="随机种子，便于复现")
    args = ap.parse_args()

    load_dotenv()
    prompts, warnings, origin = parse_prompts()

    if args.list:
        return show_list(prompts, warnings)
    if args.tree:
        return show_tree(origin, warnings)
    if args.variants:
        return run_variants(args)

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
