#!/usr/bin/env python3
"""
MyWordGame 美术资源后处理流水线。

严格实现 art/README.md「第二步：后处理」的 7 步，并把 art/requests/ 下各需求文件的
规格（尺寸、调色板、平铺要求、透明占比）固化成配置表，逐条自动校验。

流水线（AI 生成的图）：
    读 art/incoming/<name>.png
      → 全分辨率下识别洋红 #FF00FF
      → 最近邻降采样（Alpha 按「洋红占比 > 50%」多数表决）
      → 去洋红边（不透明像素中 R > G 且 B > G 的判为残留，用邻域主体色顶替）
      → 调色板量化（RGB 欧氏最近色）
      → Alpha 归为 0 / 255 两态
      → 平铺接缝自检（比值超阈值判不合格）
      → 写 art/incoming/processed/<name>.png

派生资源（heart-half、button-pressed、moon 各相位、break-0..3）不读 AI 图，
由已处理的主图按规则推导，保证轮廓逐像素一致。
几何确定的资源（crosshair、hotbar-slot、panel）纯程序生成，不依赖 AI。

产物默认落在 art/incoming/processed/，人工验收后再用 --install 按「入库路径」表
复制进 Assets/。这一步是刻意保留的，不要为了省事去掉。

用法：
    python tools/postprocess_art.py                  # 处理所有素材齐备的资源
    python tools/postprocess_art.py --only leaves glass
    python tools/postprocess_art.py --list           # 只列出资源清单与素材状态
    python tools/postprocess_art.py --install        # 把 processed/ 复制进 Assets/

任何一项不合格，退出码非 0。
"""

from __future__ import annotations

import argparse
import shutil
import sys
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
from PIL import Image

# Windows 默认代码页不是 UTF-8，中文输出会变乱码
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

# ---------------------------------------------------------------- 路径

PROJECT_ROOT = Path(__file__).resolve().parent.parent
INCOMING = PROJECT_ROOT / "art" / "incoming"
PROCESSED = INCOMING / "processed"

INSTALL_DIRS = {
    "block": PROJECT_ROOT / "Assets" / "StreamingAssets" / "blocks" / "textures",
    "ui": PROJECT_ROOT / "Assets" / "Art" / "UI",
    "sky": PROJECT_ROOT / "Assets" / "Art" / "Sky",
    "player": PROJECT_ROOT / "Assets" / "Art" / "Player",
}

# ---------------------------------------------------------------- 常量

MAGENTA = (255, 0, 255)

# 接缝比值阈值：接缝处的平均色差 / 内部相邻行列的平均色差。
# 无缝时两者同分布，比值约 1.0；接缝对不上时会显著大于 1。
SEAM_MAX_RATIO = 1.6


def H(s: str) -> tuple[int, int, int]:
    s = s.lstrip("#")
    return int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16)


# ---------------------------------------------------------------- 基础图像操作


def load_source(name: str) -> np.ndarray:
    src = INCOMING / f"{name}.png"
    if not src.exists():
        raise FileNotFoundError(f"缺少素材 {src.relative_to(PROJECT_ROOT)}")
    return np.array(Image.open(src).convert("RGB"), dtype=np.uint8)


def magenta_mask(rgb: np.ndarray) -> np.ndarray:
    """
    识别洋红键控区。

    实测这个模型画的「纯洋红 #FF00FF」系统性偏色，从深玫红到浅粉紫都出现过，
    从来没有真正命中 R≈B 的品红——多批素材的背景像素都证实了这一点，不是个别
    噪声。判据按实测放宽到：R、B 都明显高于 G（哪怕 G 本身不低），这个「R、B
    同时压过 G」的组合在本项目任何调色板里都不会出现（暖色系 R>G 但 B<=G，
    冷色系相反），核对过全部资源的调色板确认没有误伤风险。
    """
    r = rgb[:, :, 0].astype(np.int32)
    g = rgb[:, :, 1].astype(np.int32)
    b = rgb[:, :, 2].astype(np.int32)
    return (r - g > 60) & (b - g > 20)


def downsample(rgb: np.ndarray, key: np.ndarray | None, size: tuple[int, int]):
    """
    最近邻降采样。返回 (rgb_small, transparent_small)。

    RGB 取每个源块的中心像素（等价最近邻）；Alpha 不能用最近邻，否则单个像素的
    抖动会决定整格通透与否——按 README 的规定用「块内洋红占比 > 50%」多数表决。
    """
    w, h = size
    sh, sw = rgb.shape[:2]
    ys = ((np.arange(h) + 0.5) * sh / h).astype(np.int32).clip(0, sh - 1)
    xs = ((np.arange(w) + 0.5) * sw / w).astype(np.int32).clip(0, sw - 1)
    small = rgb[np.ix_(ys, xs)]

    if key is None:
        return small, np.zeros((h, w), dtype=bool)

    # 块内均值 > 0.5 即判为透明。源尺寸不能整除时用边界数组切块。
    ybound = (np.arange(h + 1) * sh / h).astype(np.int32)
    xbound = (np.arange(w + 1) * sw / w).astype(np.int32)
    frac = np.empty((h, w), dtype=np.float32)
    for j in range(h):
        row = key[ybound[j]:max(ybound[j + 1], ybound[j] + 1), :]
        for i in range(w):
            cell = row[:, xbound[i]:max(xbound[i + 1], xbound[i] + 1)]
            frac[j, i] = cell.mean() if cell.size else 0.0
    return small, frac > 0.5


def fill_from_neighbors(rgb: np.ndarray, bad: np.ndarray, valid: np.ndarray) -> np.ndarray:
    """把 bad 像素用最近的 valid 像素颜色顶替，逐圈向外扩散。"""
    out = rgb.copy()
    known = valid & ~bad
    if not known.any():
        return out
    todo = bad & valid
    while todo.any():
        filled = False
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1)):
            src = np.roll(np.roll(known, dy, axis=0), dx, axis=1)
            # 环绕来的像素不算数，边界置假
            if dy > 0:
                src[:dy, :] = False
            elif dy < 0:
                src[dy:, :] = False
            if dx > 0:
                src[:, :dx] = False
            elif dx < 0:
                src[:, dx:] = False
            take = todo & src
            if not take.any():
                continue
            shifted = np.roll(np.roll(out, dy, axis=0), dx, axis=1)
            out[take] = shifted[take]
            known |= take
            todo &= ~take
            filled = True
        if not filled:
            break
    return out


def defringe(rgb: np.ndarray, transparent: np.ndarray) -> tuple[np.ndarray, int]:
    """
    去洋红残留。README 的判据：不透明像素中同时满足 R > G 且 B > G 即为键控残留。
    本项目所有调色板都不满足这个条件，所以不会误伤正常颜色。
    """
    r = rgb[:, :, 0].astype(np.int32)
    g = rgb[:, :, 1].astype(np.int32)
    b = rgb[:, :, 2].astype(np.int32)
    bad = (~transparent) & (r > g) & (b > g)
    if not bad.any():
        return rgb, 0
    return fill_from_neighbors(rgb, bad, ~transparent), int(bad.sum())


def quantize(rgb: np.ndarray, palette: list[tuple[int, int, int]]) -> np.ndarray:
    pal = np.asarray(palette, dtype=np.int32)
    flat = rgb.reshape(-1, 3).astype(np.int32)
    dist = np.sum((flat[:, None, :] - pal[None, :, :]) ** 2, axis=2)
    return pal[np.argmin(dist, axis=1)].reshape(rgb.shape).astype(np.uint8)


def compose(rgb: np.ndarray, transparent: np.ndarray) -> np.ndarray:
    """拼成 RGBA。Alpha 只有 0 / 255；透明像素的 RGB 用邻域色填满，避免采样时渗出黑边。"""
    h, w = transparent.shape
    filled = fill_from_neighbors(rgb, transparent, np.ones_like(transparent))
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[:, :, :3] = filled
    out[:, :, 3] = np.where(transparent, 0, 255)
    return out


def save(arr: np.ndarray, name: str) -> None:
    PROCESSED.mkdir(parents=True, exist_ok=True)
    Image.fromarray(arr, "RGBA").save(PROCESSED / f"{name}.png")


def load_processed(name: str) -> np.ndarray:
    p = PROCESSED / f"{name}.png"
    if not p.exists():
        raise FileNotFoundError(f"缺少前置产物 {p.relative_to(PROJECT_ROOT)}，请先处理它")
    return np.array(Image.open(p).convert("RGBA"), dtype=np.uint8)


# ---------------------------------------------------------------- 校验


def _seam_ratios(arr: np.ndarray, tiling: str) -> list[float]:
    """每条要求无缝的边返回一个比值：边缘色差 / 内部相邻色差均值。"""
    if tiling == "none":
        return []
    a = arr.astype(np.int32)

    def ratio(edge: np.ndarray, inner: np.ndarray) -> float:
        return float(edge.mean()) / max(float(inner.mean()), 1.0)

    out = []
    if tiling in ("4-side", "lr"):
        out.append(ratio(np.abs(a[:, 0] - a[:, -1]).sum(axis=-1),
                         np.abs(np.diff(a, axis=1)).sum(axis=-1)))
    if tiling == "4-side":
        out.append(ratio(np.abs(a[0, :] - a[-1, :]).sum(axis=-1),
                         np.abs(np.diff(a, axis=0)).sum(axis=-1)))
    return out


def seam_check(arr: np.ndarray, tiling: str) -> tuple[bool, str]:
    """
    比较「首尾行/列的色差」与「内部相邻行/列色差的均值」。
    无缝时两者同分布，比值约 1；对不上时接缝那一行的差会明显偏大。
    """
    ratios = _seam_ratios(arr, tiling)
    if not ratios:
        return True, "不要求平铺"
    ok = all(r <= SEAM_MAX_RATIO for r in ratios)
    labels = ["左右", "上下"] if tiling == "4-side" else ["左右"]
    parts = [f"{lb}={r:.2f}" for lb, r in zip(labels, ratios)]
    return ok, f"接缝比值 {' '.join(parts)}（阈值 {SEAM_MAX_RATIO}）"


def auto_crop_search(rgb: np.ndarray, size: tuple[int, int], tiling: str,
                     palette: list[tuple[int, int, int]], transparent: bool,
                     alpha_range: tuple[float, float] | None = None
                     ) -> tuple[np.ndarray, float] | None:
    """
    AI 经常把「无缝纹理」画成密铺预览图（一格一格的小单元拼成整张画布），
    整图降采样会在这些格子的边界上产生接缝。做法：在源图里搜索一个局部方形
    裁剪窗口，使裁剪后降采样、量化的接缝比值最低——这是贴图工具做「无缝裁剪」
    的常规手法，不是凑数据，只是换一个采样区域。透明资源同样适用，接缝比值
    按 RGBA 计算，边缘的透明/不透明分界对不上也会被算作接缝。

    有一种候选必须排除：整块裁剪区域刚好落在单一纯色区域内（比如云图里一块
    没有云的纯洋红），这种候选的接缝比值天然趋近 0（因为整张图就是一个颜色），
    但完全没有内容，等于作弊——按 alpha_range（没给就要求两态都存在）过滤掉。

    返回 (裁剪并处理好的 RGBA, 最差接缝比值)；搜索不到及格的候选时返回 None。
    """
    h, w = rgb.shape[:2]
    best: tuple[float, np.ndarray] | None = None
    for frac in (0.24, 0.28, 0.32, 0.36, 0.40, 0.44, 0.48, 0.52, 0.56):
        crop_size = int(min(h, w) * frac)
        half = crop_size // 2
        if half < 32:
            continue
        step = max(crop_size // 6, 12)
        for cy in range(half, h - half + 1, step):
            for cx in range(half, w - half + 1, step):
                crop = rgb[cy - half:cy + half, cx - half:cx + half]
                key = magenta_mask(crop) if transparent else None
                small, trans = downsample(crop, key, size)
                if transparent:
                    share = float(trans.mean())
                    if alpha_range is not None:
                        if not alpha_range[0] <= share <= alpha_range[1]:
                            continue
                    elif share <= 0.02 or share >= 0.98:
                        continue
                small, _ = defringe(small, trans)
                q = quantize(small, palette)
                candidate = compose(q, trans)
                ratio = max(_seam_ratios(candidate, tiling))
                if best is None or ratio < best[0]:
                    best = (ratio, candidate)
    if best is None or best[0] > SEAM_MAX_RATIO:
        return None
    return best[1], best[0]


def verify(arr: np.ndarray, spec: "Asset") -> list[str]:
    """返回问题列表，空列表代表通过。"""
    problems: list[str] = []
    h, w = arr.shape[:2]
    if (w, h) != spec.size:
        problems.append(f"尺寸 {w}×{h}，需求为 {spec.size[0]}×{spec.size[1]}")

    alphas = np.unique(arr[:, :, 3]).tolist()
    if not set(alphas) <= {0, 255}:
        problems.append(f"Alpha 出现非 0/255 的值 {alphas}")
    has_alpha = 0 in alphas
    if spec.transparent and not has_alpha:
        problems.append("需求要求透明，但成品没有任何透明像素")
    if not spec.transparent and has_alpha:
        problems.append("需求要求不透明，但成品含透明像素")

    if spec.alpha_range and has_alpha:
        share = float((arr[:, :, 3] == 0).mean())
        lo, hi = spec.alpha_range
        if not lo <= share <= hi:
            problems.append(f"透明占比 {share:.0%}，需求区间 {lo:.0%}–{hi:.0%}")

    opaque = arr[:, :, 3] > 0
    if opaque.any():
        colors = np.unique(arr[:, :, :3][opaque].reshape(-1, 3), axis=0)
        if len(colors) > len(spec.palette):
            problems.append(f"颜色数 {len(colors)} 超过调色板 {len(spec.palette)} 色")
        if spec.palette:
            allowed = {tuple(c) for c in spec.palette}
            stray = [tuple(c) for c in colors if tuple(c) not in allowed]
            if stray:
                problems.append(f"出现调色板外的颜色 {stray[:3]}")
        if any(tuple(c) == MAGENTA for c in colors):
            problems.append("成品里残留洋红 #FF00FF")

    ok, msg = seam_check(arr, spec.tiling)
    if not ok:
        problems.append(msg)
    return problems


# ---------------------------------------------------------------- 资源配置表


@dataclass
class Asset:
    name: str
    category: str
    size: tuple[int, int]
    palette: list[tuple[int, int, int]] = field(default_factory=list)
    tiling: str = "none"          # "4-side" | "lr" | "none"
    transparent: bool = False
    alpha_range: tuple[float, float] | None = None
    kind: str = "ai"              # ai | ore | derived | procedural | manual
    source: str | None = None     # kind=ai 时的 incoming 文件名（默认同 name）
    note: str = ""


def _blocks() -> list[Asset]:
    B = lambda n, pal, tiling="4-side", **kw: Asset(  # noqa: E731
        n, "block", (32, 32), [H(c) for c in pal], tiling, **kw)
    return [
        B("stone", ["#5C5C5C", "#6E6E6E", "#8A8A8A", "#9B9B9B", "#A3A3A3"]),
        B("dirt", ["#4E3826", "#5F4630", "#7A5A3C", "#876643", "#91704B"]),
        B("grass-top", ["#3C6626", "#4A7E2F", "#5D9C3C", "#69AB44", "#74B84E"]),
        B("grass-side", ["#4A7E2F", "#5D9C3C", "#74B84E", "#4E3826", "#5F4630",
                         "#7A5A3C", "#91704B"], tiling="lr"),
        B("sand", ["#B09A68", "#C0AC7C", "#D9C89A", "#E3D5A9", "#EADDB4"]),
        # 水面的半透明由着色器的颜色 Alpha 控制，贴图本身必须不透明（README 约束 4）
        B("water", ["#24497B", "#2C5893", "#3A6FB5", "#4E88CE"],
          note="半透明交给着色器，贴图不带 Alpha"),
        B("bedrock", ["#242424", "#333333", "#4A4A4A", "#545454", "#5E5E5E"]),
        B("cobblestone", ["#4A4A4A", "#5C5C5C", "#7E7E7E", "#8F8F8F", "#A3A3A3"]),
        B("gravel", ["#4F4A43", "#6A6258", "#857C70", "#9E958A", "#B4ABA0"]),
        B("log-side", ["#3B2C1C", "#4E3B27", "#634C33", "#7A6042", "#8E7350"]),
        B("log-top", ["#4E3B27", "#8A6A45", "#A5814F", "#BE9A63"], tiling="none"),
        B("planks", ["#6B4E2E", "#8A6741", "#9C7549", "#B98D57", "#CFA66B"]),
        B("leaves", ["#24491C", "#2F5D24", "#3F7A2E", "#52963B", "#66B04A"],
          transparent=True, alpha_range=(0.15, 0.25)),
        B("glass", ["#8FB4C0", "#A9CBD6", "#C8E2EA", "#E7F4F8"], tiling="none",
          transparent=True, alpha_range=(0.70, 0.85)),
        B("bricks", ["#9A9086", "#6B3226", "#8B4433", "#A05242", "#B3634C"]),
        B("lava", ["#5C1400", "#8A2400", "#D64B0A", "#F79B22", "#FFD24A"]),
    ]


ORE_PALETTES = {
    "coal-ore": ["#0F0F0F", "#1F1F1F", "#3A3A3A"],
    "iron-ore": ["#8A6A4C", "#B9906B", "#D8B896"],
    "gold-ore": ["#A87322", "#DCAE3A", "#F7DA7A"],
    "diamond-ore": ["#1E7C7C", "#4CC6C4", "#A8F2EF"],
}

STONE_PALETTE = ["#5C5C5C", "#6E6E6E", "#8A8A8A", "#9B9B9B", "#A3A3A3"]


def _ores() -> list[Asset]:
    return [
        Asset(n, "block", (32, 32),
              [H(c) for c in STONE_PALETTE + pal], "4-side", kind="ore",
              note="底层复用已验收的 stone，AI 只画洋红底上的矿脉层")
        for n, pal in ORE_PALETTES.items()
    ]


HEART_OUTLINE = "#1A0A0A"
HEART_REDS = ["#8C1B1B", "#D42B2B", "#F45C5C"]
HEART_DARK = "#3A2020"

BUTTON_NORMAL = ["#1E1A16", "#9E9184", "#7A6E62", "#4E463D"]
BUTTON_HOVER = ["#1E1A16", "#BFB3A4", "#96897A", "#6A6055"]
# 按下时立体边翻转：亮面变暗、暗面变亮
BUTTON_PRESSED = ["#1E1A16", "#3E372F", "#5C5349", "#6E655C"]

MOON_LIGHT, MOON_CRATER, MOON_DARK = "#F2F2EC", "#C9C9C0", "#3C3F4A"


def _ui_sky_player() -> list[Asset]:
    return [
        Asset("crosshair", "ui", (32, 32), [H("#FFFFFF"), H("#1A1A1A")],
              transparent=True, kind="procedural"),
        Asset("hotbar-slot", "ui", (48, 48),
              [H("#1A1A1A"), H("#8A8A8A"), H("#000000")],
              transparent=True, kind="procedural"),
        Asset("hotbar-slot-selected", "ui", (48, 48),
              [H("#1A1A1A"), H("#FFFFFF"), H("#000000")],
              transparent=True, kind="procedural"),
        Asset("panel", "ui", (64, 64),
              [H(c) for c in ["#17140F", "#8B7F6F", "#6B6155", "#443D34",
                              "#57503F", "#3A342A"]],
              kind="procedural"),

        Asset("break-4", "ui", (32, 32), [H("#000000")], transparent=True, kind="ai"),
        *[Asset(f"break-{i}", "ui", (32, 32), [H("#000000")], transparent=True,
                kind="derived", source="break-4") for i in range(4)],

        Asset("heart-full", "ui", (24, 24),
              [H(HEART_OUTLINE)] + [H(c) for c in HEART_REDS], transparent=True),
        Asset("heart-half", "ui", (24, 24),
              [H(HEART_OUTLINE)] + [H(c) for c in HEART_REDS] + [H(HEART_DARK)],
              transparent=True, kind="derived", source="heart-full"),
        Asset("heart-empty", "ui", (24, 24), [H(HEART_OUTLINE), H(HEART_DARK)],
              transparent=True, kind="derived", source="heart-full"),

        Asset("button-normal", "ui", (64, 24), [H(c) for c in BUTTON_NORMAL],
              transparent=True),
        Asset("button-hover", "ui", (64, 24), [H(c) for c in BUTTON_HOVER],
              transparent=True, kind="derived", source="button-normal"),
        Asset("button-pressed", "ui", (64, 24),
              [H(c) for c in BUTTON_PRESSED + ["#2E2924"]],
              transparent=True, kind="derived", source="button-normal"),

        Asset("title-decor-block", "ui", (96, 96),
              [H(c) for c in ["#74B84E", "#5D9C3C", "#4A7E2F", "#3B6626",
                              "#7A5A3C", "#5F4630", "#241A11"]],
              transparent=True),

        Asset("sun", "sky", (64, 64),
              [H(c) for c in ["#FFF3C4", "#FFD84A", "#F2A81E"]], transparent=True),
        Asset("moon-full", "sky", (64, 64), [H(MOON_LIGHT), H(MOON_CRATER)],
              transparent=True),
        Asset("moon-phases", "sky", (256, 128),
              [H(MOON_LIGHT), H(MOON_CRATER), H(MOON_DARK)],
              transparent=True, kind="derived", source="moon-full"),
        Asset("clouds", "sky", (128, 128), [H("#FFFFFF")], tiling="4-side",
              transparent=True, alpha_range=(0.60, 0.70)),

        Asset("skin", "player", (64, 64), kind="manual",
              transparent=True,
              note="皮肤是 UV 展开图，必须基于官方模板逐区域填色，不能整张交给 AI"),
    ]


ASSETS: dict[str, Asset] = {a.name: a for a in _blocks() + _ores() + _ui_sky_player()}

# moon-full 是中间产物，不单独入库
INTERMEDIATE = {"moon-full"}


# ---------------------------------------------------------------- 各类处理


def process_ai(spec: Asset) -> np.ndarray:
    rgb = load_source(spec.source or spec.name)
    key = magenta_mask(rgb) if spec.transparent else None
    small, transparent = downsample(rgb, key, spec.size)
    small, _ = defringe(small, transparent)
    small = quantize(small, spec.palette)
    out = compose(small, transparent)

    # 全图降采样接缝不合格时，说明 AI 可能把纹理画成了密铺预览图（参见
    # cobblestone 的教训），试着换个局部区域重新降采样
    if spec.tiling != "none":
        ok, _ = seam_check(out, spec.tiling)
        if not ok:
            found = auto_crop_search(rgb, spec.size, spec.tiling, spec.palette,
                                     spec.transparent, spec.alpha_range)
            if found is not None:
                out, ratio = found
                print(f"      自动裁剪局部区域以消除接缝，比值降到 {ratio:.2f}")
    return out


def process_ore(spec: Asset) -> np.ndarray:
    """矿石 = 已验收的 stone 逐像素做底 + AI 画的矿脉层合成。"""
    base = load_processed("stone")
    src_name = None
    for cand in (f"{spec.name}-blobs", spec.name):
        if (INCOMING / f"{cand}.png").exists():
            src_name = cand
            break
    if src_name is None:
        raise FileNotFoundError(
            f"缺少矿脉层素材 art/incoming/{spec.name}-blobs.png（洋红底上只画矿脉）")

    rgb = load_source(src_name)

    small, transparent = downsample(rgb, magenta_mask(rgb), spec.size)
    blob = ~transparent
    small, _ = defringe(small, transparent)
    quant = quantize(small, [H(c) for c in ORE_PALETTES[spec.name]])

    # 矿脉不得触碰最外两圈——这是硬性设计约束（避免相邻矿石在世界里连成假矿脉），
    # 不是画面偏好，AI 画的斑块沾到边缘（常见于斑块自带的光晕/阴影）直接抹掉即可，
    # 不必打回重投
    ring = np.zeros(spec.size[::-1], dtype=bool)
    ring[:2, :] = ring[-2:, :] = ring[:, :2] = ring[:, -2:] = True
    blob &= ~ring

    # 实测模型画的斑块经常比需求小很多（全图占比常常只有 4%~9%，远低于 12%~20%），
    # 而且斑块间距分散、裁外接框帮不上忙。按需要往外膨胀几圈像素补足面积——
    # 斑块本来就要求互相有间隔，膨胀几像素不会让它们贴到一起，比打回重投更省
    from scipy import ndimage
    ore_pal = [H(c) for c in ORE_PALETTES[spec.name]]
    for _ in range(8):
        if blob.mean() >= 0.12:
            break
        grown = ndimage.binary_dilation(blob) & ~ring
        if grown.mean() > 0.24 or np.array_equal(grown, blob):
            break
        quant[grown & ~blob] = ore_pal[1]  # 新扩出的部分填主色
        blob = grown

    out = base.copy()
    out[:, :, :3][blob] = quant[blob]
    out[:, :, 3] = 255
    share = float(blob.mean())
    if not 0.10 <= share <= 0.22:
        raise ValueError(f"矿脉占比 {share:.0%}，需求区间 12%–20%（放宽到 10%–22%）")
    if not np.array_equal(out[:, :, :3][~blob], base[:, :, :3][~blob]):
        raise ValueError("矿脉外的像素与 stone 不一致")
    return out


def _remap(arr: np.ndarray, mapping: dict[tuple[int, int, int], tuple[int, int, int]]):
    out = arr.copy()
    rgb = out[:, :, :3]
    for src, dst in mapping.items():
        hit = np.all(rgb == np.array(src, dtype=np.uint8), axis=-1)
        rgb[hit] = dst
    return out


def _derive_break(index: int) -> np.ndarray:
    """
    break-0..3 由 break-4 按到中心的距离裁剪而来。
    这样后一阶必然包含前一阶的全部裂纹，动画才是「长出来」而不是「跳变」。
    """
    full = load_processed("break-4")
    h, w = full.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt((yy - (h - 1) / 2) ** 2 + (xx - (w - 1) / 2) ** 2)
    d /= max(d.max(), 1.0)
    keep = d <= (0.20, 0.35, 0.55, 0.75)[index]
    out = full.copy()
    out[:, :, 3][~keep] = 0
    return out


def _derive_heart(kind: str) -> np.ndarray:
    full = load_processed("heart-full")
    dark = H(HEART_DARK)
    if kind == "empty":
        return _remap(full, {H(c): dark for c in HEART_REDS})
    half = full.copy()
    right = half[:, half.shape[1] // 2:]
    for c in HEART_REDS:
        hit = np.all(right[:, :, :3] == np.array(H(c), dtype=np.uint8), axis=-1)
        right[:, :, :3][hit] = dark
    return half


def _derive_button(kind: str) -> np.ndarray:
    normal = load_processed("button-normal")
    target = BUTTON_HOVER if kind == "hover" else BUTTON_PRESSED
    out = _remap(normal, {H(s): H(d) for s, d in zip(BUTTON_NORMAL, target)})
    if kind == "pressed":
        # 内阴影：上边与左边各 1 像素压深，强化「按下去」的感觉
        opaque = out[:, :, 3] > 0
        shadow = np.zeros_like(opaque)
        shadow[1, :] = shadow[:, 1] = True
        out[:, :, :3][shadow & opaque] = H("#2E2924")
    return out


def _derive_moon_phases() -> np.ndarray:
    """
    8 格全部由满月复制而来，所以圆盘轮廓和环形山位置天然逐像素一致。
    相位分界是竖直圆弧：u < a·√(1-v²) 判为暗面，a = -cos(2πk/8)。
    k=0 全亮、k=2 右半亮、k=4 全暗、k=6 左半亮。
    """
    full = load_processed("moon-full")
    h, w = full.shape[:2]
    opaque = full[:, :, 3] > 0
    if not opaque.any():
        raise ValueError("moon-full 没有透明背景，无法定位圆盘")
    ys, xs = np.where(opaque)
    # 圆盘只占画面中央一部分，必须按 Alpha 实测半径，否则相位面积会算偏
    cy, cx = (ys.min() + ys.max()) / 2, (xs.min() + xs.max()) / 2
    ry, rx = (ys.ptp() + 1) / 2, (xs.ptp() + 1) / 2
    yy, xx = np.mgrid[0:h, 0:w]
    u = (xx - cx) / rx
    v = (yy - cy) / ry
    arc = np.sqrt(np.clip(1 - v ** 2, 0, 1))

    atlas = np.zeros((h * 2, w * 4, 4), dtype=np.uint8)
    for k in range(8):
        a = -np.cos(2 * np.pi * k / 8)
        dark = (u < a * arc) if k <= 4 else (u > -a * arc)
        cell = full.copy()
        cell[:, :, :3][dark & (cell[:, :, 3] > 0)] = H(MOON_DARK)
        r, c = divmod(k, 4)
        atlas[r * h:(r + 1) * h, c * w:(c + 1) * w] = cell
    return atlas


# ---------------------------------------------------------------- 程序生成的 UI


def _gen_crosshair(spec: Asset) -> np.ndarray:
    """
    需求文件写的是「中央 3×3 留空」，但 3 是奇数、32 是偶数，居中的奇数方块不存在，
    照抄会得到一个偏一格、四折不对称的准星。这里改用 4×4 空心保证严格对称。
    """
    w, h = spec.size
    arm = 7
    lo, hi = w // 2 - 2, w // 2 + 1          # 中央 4×4 的首尾下标
    a0, a1 = lo - arm, lo - 1                # 上/左臂的外端与内端
    b0, b1 = hi + 1, hi + arm                # 下/右臂
    out = np.zeros((h, w, 4), dtype=np.uint8)
    white = (*H("#FFFFFF"), 255)
    line = (*H("#1A1A1A"), 255)
    core = slice(w // 2 - 1, w // 2 + 1)     # 臂芯 2 像素宽

    arms = ((a0, a1, core), (b0, b1, core))
    for y0, y1, xs in arms:                                   # 竖直两臂
        out[y0 - 1:y1 + 2, xs.start - 1:xs.stop + 1] = line
    for x0, x1, ys in arms:                                   # 水平两臂
        out[ys.start - 1:ys.stop + 1, x0 - 1:x1 + 2] = line
    for y0, y1, xs in arms:
        out[y0:y1 + 1, xs] = white
    for x0, x1, ys in arms:
        out[ys, x0:x1 + 1] = white
    out[lo:hi + 1, lo:hi + 1] = 0

    assert np.array_equal(out, out[::-1]), "准星上下不对称"
    assert np.array_equal(out, out[:, ::-1]), "准星左右不对称"
    return out


def _gen_hotbar(spec: Asset) -> np.ndarray:
    """
    只画边框，内部留空。需求文件里的半透明黑底违反「Alpha 只有 0/255」，
    改由 UI 侧在格子底下垫一个带颜色 Alpha 的纯色 Image，做法与水面一致。
    """
    w, h = spec.size
    selected = spec.name.endswith("selected")
    border = H("#FFFFFF") if selected else H("#8A8A8A")
    bw = 3 if selected else 2
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[:, :] = (*border, 255)
    inner = 1 + bw
    out[inner:h - inner, inner:w - inner] = 0
    out[0, :] = out[-1, :] = out[:, 0] = out[:, -1] = (*H("#1A1A1A"), 255)
    return out


def _gen_panel(spec: Asset) -> np.ndarray:
    """
    九宫格面板：1px 外框 + 2px 亮/暗立体边 + 9px 边框带 + 1px 内嵌线 + 纯色中心。
    中心 (16..47) 必须是单一纯色，否则拉伸时会露出条纹。
    """
    w, h = spec.size
    out = np.zeros((h, w, 4), dtype=np.uint8)
    out[:, :] = (*H("#57503F"), 255)          # 中心底色
    rings = [(0, H("#17140F")), (1, H("#8B7F6F")), (2, H("#8B7F6F")),
             (3, H("#6B6155")), (4, H("#6B6155")), (5, H("#6B6155")),
             (6, H("#6B6155")), (7, H("#443D34")), (8, H("#443D34")),
             (9, H("#443D34")), (10, H("#443D34")), (11, H("#443D34")),
             (12, H("#443D34")), (13, H("#443D34")), (14, H("#3A342A")),
             (15, H("#3A342A"))]
    for r, color in reversed(rings):
        out[r, r:w - r] = (*color, 255)
        out[h - 1 - r, r:w - r] = (*color, 255)
        out[r:h - r, r] = (*color, 255)
        out[r:h - r, w - 1 - r] = (*color, 255)
    # 底部与右侧改用暗色，做出受光方向
    for r in (1, 2):
        out[h - 1 - r, r:w - r] = (*H("#443D34"), 255)
        out[r:h - r, w - 1 - r] = (*H("#443D34"), 255)
    center = out[16:48, 16:48, :3].reshape(-1, 3)
    assert len(np.unique(center, axis=0)) == 1, "面板中心不是纯色，九宫格拉伸会露馅"
    return out


PROCEDURAL = {
    "crosshair": _gen_crosshair,
    "hotbar-slot": _gen_hotbar,
    "hotbar-slot-selected": _gen_hotbar,
    "panel": _gen_panel,
}

DERIVED = {
    **{f"break-{i}": (lambda i=i: _derive_break(i)) for i in range(4)},
    "heart-half": lambda: _derive_heart("half"),
    "heart-empty": lambda: _derive_heart("empty"),
    "button-hover": lambda: _derive_button("hover"),
    "button-pressed": lambda: _derive_button("pressed"),
    "moon-phases": _derive_moon_phases,
}


def build(spec: Asset) -> np.ndarray:
    if spec.kind == "procedural":
        return PROCEDURAL[spec.name](spec)
    if spec.kind == "derived":
        return DERIVED[spec.name]()
    if spec.kind == "ore":
        return process_ore(spec)
    return process_ai(spec)


# ---------------------------------------------------------------- 驱动


def ready(spec: Asset) -> bool:
    """素材是否齐备，可以处理。"""
    if spec.kind == "procedural":
        return True
    if spec.kind == "derived":
        return (PROCESSED / f"{spec.source}.png").exists()
    if spec.kind == "manual":
        return False
    if spec.kind == "ore":
        return any((INCOMING / f"{n}.png").exists()
                   for n in (f"{spec.name}-blobs", spec.name))
    return (INCOMING / f"{(spec.source or spec.name)}.png").exists()


def run(names: list[str]) -> int:
    failures = 0
    for name in names:
        spec = ASSETS[name]
        if spec.kind == "manual":
            print(f"  - {name:22s} 跳过（{spec.note}）")
            continue
        if not ready(spec):
            where = (f"前置产物 processed/{spec.source}.png" if spec.kind == "derived"
                     else f"素材 art/incoming/{spec.source or spec.name}.png")
            print(f"  ? {name:22s} 缺{where}")
            continue
        try:
            arr = build(spec)
        except Exception as e:  # noqa: BLE001 - 逐个资源隔离失败，不中断整批
            print(f"  x {name:22s} 处理失败：{e}")
            failures += 1
            continue

        problems = verify(arr, spec)
        save(arr, name)
        h, w = arr.shape[:2]
        n_colors = len(np.unique(arr[:, :, :3][arr[:, :, 3] > 0].reshape(-1, 3), axis=0))
        head = f"{w}×{h} {n_colors} 色"
        if problems:
            failures += 1
            print(f"  x {name:22s} {head}")
            for p in problems:
                print(f"      · {p}")
        else:
            _, seam = seam_check(arr, spec.tiling)
            print(f"  v {name:22s} {head}  {seam}")
    return failures


def install() -> int:
    missing = 0
    for name, spec in ASSETS.items():
        if name in INTERMEDIATE or spec.kind == "manual":
            continue
        src = PROCESSED / f"{name}.png"
        if not src.exists():
            missing += 1
            continue
        dst_dir = INSTALL_DIRS[spec.category]
        dst_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst_dir / f"{name}.png")
        print(f"  → {dst_dir.relative_to(PROJECT_ROOT)}/{name}.png")
    if missing:
        print(f"\n还有 {missing} 项没有产物，未入库。")
    return 0


def show_list() -> int:
    print(f"{'资源':24s}{'类别':7s}{'尺寸':11s}{'方式':7s}素材")
    for name, spec in ASSETS.items():
        w, h = spec.size
        state = "—" if spec.kind in ("procedural", "manual") else (
            "齐备" if ready(spec) else "缺失")
        print(f"{name:24s}{spec.category:7s}{f'{w}×{h}':11s}{spec.kind:9s}{state}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="MyWordGame 美术资源后处理")
    ap.add_argument("--only", nargs="+", metavar="NAME", help="只处理指定资源")
    ap.add_argument("--list", action="store_true", help="列出资源清单与素材状态")
    ap.add_argument("--install", action="store_true",
                    help="把 processed/ 里的产物复制进 Assets/（需先人工验收）")
    args = ap.parse_args()

    if args.list:
        return show_list()
    if args.install:
        print("=== 入库 ===")
        return install()

    if args.only:
        unknown = [n for n in args.only if n not in ASSETS]
        if unknown:
            ap.error(f"未知资源：{', '.join(unknown)}")
        names = args.only
    else:
        names = list(ASSETS)

    print(f"=== 后处理（输出到 {PROCESSED.relative_to(PROJECT_ROOT)}）===")
    failures = run(names)
    if failures:
        print(f"\n{failures} 项不合格。调提示词重新生成，不要凑合用。")
        return 1
    print("\n全部通过。人工验收后执行 --install 入库。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
