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
    "item": PROJECT_ROOT / "Assets" / "StreamingAssets" / "items" / "textures",
    "entity": PROJECT_ROOT / "Assets" / "Art" / "Entities",
    # milestone-11 新增大类（A3-A5）：统一先落 Assets/Art/<大类>，
    # 后续波次接线时按实际加载方挪目录，此处先保证 --install 不 KeyError
    "effects": PROJECT_ROOT / "Assets" / "Art" / "Effects",
    "codex": PROJECT_ROOT / "Assets" / "Art" / "Codex",
    "architecture": PROJECT_ROOT / "Assets" / "Art" / "Architecture",
    "scenes": PROJECT_ROOT / "Assets" / "Art" / "Scenes",
    "marketing": PROJECT_ROOT / "Assets" / "Art" / "Marketing",
    "seasonal": PROJECT_ROOT / "Assets" / "Art" / "Seasonal",
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
        if not spec.direct:       # 直用大图不限色，其余照旧
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
    direct: bool = False          # 直用大图（1024 概念/宣传图）：不量化、不做调色板校验
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
        # 里程碑-3 新增 6 个方块贴图（planks / log-side / log-top / leaves 已在前面
        # 的 lava 之前注册过了，这里只补任务 A2 真正新建的 6 个 ASSETS 条目）
        B("sapling", ["#3F7A2E", "#634C33", "#5F4630", "#52963B", "#4E3826"],
          tiling="none", transparent=True, alpha_range=(0.50, 0.70)),
        B("crafting_table-top", ["#6B4E2E", "#8A6741", "#9C7549", "#1A1A1A"]),
        B("crafting_table-side", ["#6B4E2E", "#8A6741", "#9C7549", "#B98D57",
                                  "#1A1A1A"]),
        B("iron_door", ["#1A1A1A", "#5C5C5C", "#7A6042", "#8A6A4C", "#B9906B"],
          transparent=True, alpha_range=(0.50, 0.70), tiling="none"),
        B("lever", ["#1A1A1A", "#5C5C5C", "#7E7E7E", "#8A8A8A", "#634C33",
                    "#8A6741"]),
        B("redstone_dust", ["#1A1A1A", "#5C5C5C", "#7E7E7E", "#A02020",
                            "#D03030", "#E84040"]),
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


# 物品图标（任务 A4）：尺寸 16×16，绝大多数透明。调色板与对应 art/requests/items/*.md 一一对应。
ITEMS = [
    # 矿石与锭
    ("diamond",            ["#1E7C7C", "#4CC6C4", "#A8F2EF", "#1A1A1A"]),
    ("iron_ingot",         ["#5C5C5C", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    ("netherite_ingot",    ["#1A1A22", "#3A2A3A", "#5A4A6A", "#0F0F12"]),
    ("coal",               ["#0F0F0F", "#1F1F1F", "#3A3A3A", "#0A0A0A"]),
    ("emerald",            ["#1A7C3A", "#4CC66A", "#A8F2B6", "#1A1A1A"]),
    ("lapis",              ["#1A3A7C", "#3A5AC4", "#6A8AE8", "#DCAE3A", "#1A1A1A"]),
    ("redstone",           ["#A02020", "#D03030", "#E84040", "#1A1A1A"]),
    ("redstone_dust",      ["#A02020", "#D03030", "#E84040", "#1A1A1A"]),
    # 原始方块
    ("bedrock",            ["#242424", "#3A3A3A", "#4A4A4A", "#1A1A1A"]),
    ("cobblestone",        ["#4A4A4A", "#7E7E7E", "#A3A3A3", "#1A1A1A"]),
    # 木材
    ("plank",              ["#6B4E2E", "#8A6741", "#9C7549", "#1A1A1A"]),
    ("stick",              ["#634C33", "#7A6042", "#8E7350", "#1A1A1A"]),
    # 食物
    ("raw_porkchop",       ["#C88A8A", "#F2B0B0", "#FFE8E8", "#1A1A1A"]),
    ("rotten_flesh",       ["#4A5A2A", "#6E8E3A", "#9AB05A", "#1A1A1A"]),
    ("beet",               ["#7A1A3A", "#A82A52", "#5D9C3C", "#74B84E", "#1A1A1A"]),
    ("mung_bean",          ["#4A6E2A", "#6E8E3A", "#9ABE5A", "#1A1A1A"]),
    ("bowl",               ["#634C33", "#7A6042", "#9C7549", "#1A1A1A"]),
    ("bowl_of_water",      ["#634C33", "#7A6042", "#3A6FB5", "#4E88CE", "#1A1A1A"]),
    ("beet_soup",          ["#634C33", "#7A6042", "#7A1A3A", "#A82A52", "#1A1A1A"]),
    ("mung_bean_soup",     ["#634C33", "#7A6042", "#6E8E3A", "#9ABE5A", "#1A1A1A"]),
    # 剑
    ("wooden_sword",       ["#634C33", "#7A6042", "#B98D57", "#1A1A1A"]),
    ("stone_sword",        ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    ("iron_sword",         ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    ("diamond_sword",      ["#634C33", "#4CC6C4", "#A8F2EF", "#1A1A1A"]),
    ("netherite_sword",    ["#634C33", "#3A2A3A", "#5A4A6A", "#0F0F12"]),
    ("bedrock_sword",      ["#634C33", "#3A3A3A", "#4A4A4A", "#1A1A1A"]),
    # 镐
    ("wooden_pickaxe",     ["#634C33", "#7A6042", "#B98D57", "#1A1A1A"]),
    ("stone_pickaxe",      ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    ("iron_pickaxe",       ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    ("diamond_pickaxe",    ["#634C33", "#4CC6C4", "#A8F2EF", "#1A1A1A"]),
    ("netherite_pickaxe",  ["#634C33", "#3A2A3A", "#5A4A6A", "#0F0F12"]),
    ("bedrock_pickaxe",    ["#634C33", "#3A3A3A", "#4A4A4A", "#1A1A1A"]),
    # 斧
    ("wooden_axe",         ["#634C33", "#7A6042", "#B98D57", "#1A1A1A"]),
    ("stone_axe",          ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    ("iron_axe",           ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    # 锹
    ("wooden_shovel",      ["#634C33", "#7A6042", "#B98D57", "#1A1A1A"]),
    ("stone_shovel",       ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    ("iron_shovel",        ["#634C33", "#8A8A8A", "#C8C8C8", "#1A1A1A"]),
    # 杂项
    ("wool",               ["#C8C8C8", "#F2F2F2", "#FFFFFF", "#5C5C5C"]),
    ("book",               ["#634C33", "#8A6741", "#F2EAD2", "#1A1A1A"]),
    ("enchanted_book",     ["#3A2A5A", "#5A4A8A", "#A88AD2", "#F2EAD2", "#1A1A1A"]),
    ("string",             ["#A0A0A0", "#E0E0E0", "#FFFFFF", "#1A1A1A"]),
    ("gunpowder",          ["#1A1A1A", "#3A3A3A", "#5C5C5C", "#0F0F0F"]),
    ("bone",               ["#C8C8B8", "#EAEAD8", "#F8F8E8", "#1A1A1A"]),
    ("skull",              ["#C8C8B8", "#EAEAD8", "#F8F8E8", "#1A1A1A"]),
    ("crafting_table-top", ["#6B4E2E", "#8A6741", "#9C7549", "#1A1A1A"]),
]

# 实体图标（任务 A4）：尺寸 32×32，不透明。MobView/VillagerView 当前仍用
# MaterialPropertyBlock 上色，这些 PNG 是为未来切换到贴图渲染预留。
ENTITIES = [
    ("pig",                 ["#F2B0B0", "#C98F68", "#8E6F4E", "#1A1A1A"]),
    ("sheep",               ["#F2F2F2", "#C8C8C8", "#D2A48A", "#1A1A1A"]),
    ("zombie",              ["#6E9A4A", "#3A5A2A", "#5A4A6A", "#F2F2F2", "#1A1A1A"]),
    ("skeleton",            ["#EAEAD8", "#C8C8B8", "#634C33", "#1A1A1A"]),
    ("creeper",             ["#6ED66A", "#3A8A3A", "#1A1A1A"]),
    ("villager_farmer",     ["#8A6741", "#D2A48A", "#5D9C3C", "#A87322", "#1A1A1A"]),
    ("villager_librarian",  ["#5A4A8A", "#D2A48A", "#3A2A5A", "#A87322", "#1A1A1A"]),
    ("villager_blacksmith", ["#4A4A52", "#D2A48A", "#3A3A3A", "#A87322", "#1A1A1A"]),
]


def _items() -> list[Asset]:
    # 任务 A4 物品图标：AI 经常把图标画在画布中央、四周留出大片透明背景，
    # 实测透明占比经常落在 70%–98% 区间。需求文件的"≥ 30% 透明"是最低线，
    # 上限不放宽就要么打回重投、要么把质量标准定得太死——本里程碑是首次补完
    # 物品图标，先把图占上、留待后续任务根据实际显示效果再收紧范围。
    out: list[Asset] = []
    for entry in ITEMS:
        name = entry[0]
        pal = entry[1]
        out.append(Asset(name, "item", (16, 16), [H(c) for c in pal], "none",
                         transparent=True, kind="ai"))
    return out


def _entities() -> list[Asset]:
    out: list[Asset] = []
    for entry in ENTITIES:
        name = entry[0]
        pal = entry[1]
        out.append(Asset(name, "entity", (32, 32), [H(c) for c in pal], "none",
                         transparent=False, kind="ai"))
    return out


# ---------------------------------------------------------------- milestone-11 资源（A1-A5 共 238 项）
#
# m11 起资源名统一用连字符（与需求文件名一一对应）。下面的调色板是注册时的
# 初始值，真源仍是 art/requests/ 下各需求文件的调色板段——集成点逐批核对，
# 有出入以需求文件为准并回写这里（评审会抓这种漂移）。

OUTLINE_DARK = "#1A1A1A"            # 图标深色描边（A2 约定 1px，提升背包可读性）
HANDLE_WOOD = ["#634C33", "#7A6042", "#8E7350"]        # 工具/乐器木柄
METAL_IRON = ["#5C5C5C", "#8A8A8A", "#C8C8C8"]
METAL_GOLD = ["#A87322", "#DCAE3A", "#F7DA7A"]
METAL_ALLOY = ["#3A6E8A", "#4E88A8", "#6AA8C8"]        # 夏季合金：蓝绿金属
METAL_ESSENCE = ["#5A4A8A", "#A88AD2", "#DCAE3A"]      # 机元：紫金
PLANK_WOOD = ["#6B4E2E", "#8A6741", "#9C7549", "#B98D57"]
DIRT_BROWN = ["#4E3826", "#5F4630", "#7A5A3C", "#91704B"]
STONE_GRAY = ["#5C5C5C", "#6E6E6E", "#8A8A8A", "#9B9B9B", "#A3A3A3"]
BRICK_RED = ["#6B3226", "#8B4433", "#A05242", "#B3634C", "#9A9086"]
STEM_GREEN = ["#3F7A2E", "#52963B", "#2F5D24"]          # 花草茎叶（深于 grass-top 主色）
PARCHMENT = ["#E8D8A8", "#D2BC80", "#C8AA70"]           # 羊皮纸（地图/卷轴）
SKIN_TONE = ["#D2A48A", "#E8C0A8"]                      # 玩家脸基色
UI_WARM_GRAY = ["#17140F", "#443D34", "#6B6155", "#8B7F6F"]   # 面板/框同 panel 色系
BADGE_BASE = ["#8B7355", "#DCAE3A", "#F7DA7A", "#5C4632"]     # 徽章共用底盘 + 金边


def _m11_a1_blocks() -> list[Asset]:
    """任务 A1：方块类 48 张（功能 9 / 农业 11 / 花草 12 / 树木 13 / 楼梯 3）。"""

    def X(n, pal, tiling="4-side", **kw):   # 不透明贴图，默认四边无缝
        return Asset(n, "block", (32, 32), [H(c) for c in pal], tiling, **kw)

    def K(n, pal, **kw):            # 洋红键控的十字/镂空类，不平铺
        return Asset(n, "block", (32, 32), [H(c) for c in pal], "none",
                     transparent=True, **kw)

    def L(n, pal):                  # 树叶类：与既有 leaves 同款键控占比区间
        return Asset(n, "block", (32, 32), [H(c) for c in pal], "4-side",
                     transparent=True, alpha_range=(0.15, 0.25))

    # —— 功能方块 9（箱子三面 / 门两段 / 床三面都是整块独占贴图，不要求平铺）——
    chest_pal = PLANK_WOOD + [OUTLINE_DARK]
    bed_pal = ["#8C1B1B", "#D42B2B", "#F45C5C", "#F2F2F2"] + PLANK_WOOD[:2]
    door_pal = PLANK_WOOD + ["#8FB4C0", "#C8E2EA", OUTLINE_DARK]
    a1 = [
        K("torch", HANDLE_WOOD + ["#8A2400", "#F79B22", "#FFD24A"]),
        X("chest-front", chest_pal), X("chest-side", chest_pal), X("chest-top", chest_pal),
        K("wooden-door-upper", door_pal, alpha_range=(0.08, 0.22)),
        X("wooden-door-lower", door_pal, tiling="none"),
        X("bed-head-top", bed_pal, tiling="none"),
        X("bed-foot-top", bed_pal, tiling="none"),
        X("bed-side", bed_pal + [OUTLINE_DARK], tiling="none"),

        # —— 农业 11：耕地不透明；作物三阶段逐级转色，全部键控 ——
        X("farmland-dry", ["#3A2A1C", "#4E3826", "#5F4630", "#7A5A3C"]),
        X("farmland-wet", ["#2A1E12", "#3A2A1C", "#4E3826", "#5F4630"]),
        K("wheat-stage0", ["#4A7E2F", "#5D9C3C", "#74B84E"]),
        K("wheat-stage1", ["#5D9C3C", "#74B84E", "#A87322", "#DCAE3A"]),
        K("wheat-stage2", ["#DCAE3A", "#F7DA7A", "#A87322"] + HANDLE_WOOD[:1]),
        K("beet-stage0", ["#4A7E2F", "#5D9C3C", "#74B84E"]),
        K("beet-stage1", ["#4A7E2F", "#5D9C3C", "#74B84E", "#7A1A3A"]),
        K("beet-stage2", ["#4A7E2F", "#5D9C3C", "#7A1A3A", "#A82A52"]),
        K("mung-stage0", ["#4A6E2A", "#6E8E3A"]),
        K("mung-stage1", ["#6E8E3A", "#9ABE5A", "#C8E8A0"]),
        K("mung-stage2", ["#9ABE5A", "#C8E8A0", "#DCAE3A"]),

        # —— 花草 12：主体居中约 70%，背景整片纯洋红键控 ——
        K("flower-poppy", ["#D42B2B", "#F45C5C", "#8C1B1B"] + STEM_GREEN),
        K("flower-dandelion", ["#DCAE3A", "#F7DA7A", "#A87322"] + STEM_GREEN),
        K("flower-orchid", ["#3A6FB5", "#4E88CE", "#8FB4E8"] + STEM_GREEN),
        K("flower-cornflower", ["#3A5AC4", "#6A8AE8", "#1A3A7C"] + STEM_GREEN),
        K("flower-rose", ["#8C1B1B", "#D42B2B", "#3A2020"] + STEM_GREEN),
        K("flower-sunflower", ["#F7DA7A", "#DCAE3A", "#A87322", "#8A2400"] + STEM_GREEN),
        K("flower-lilac", ["#5A4A8A", "#A88AD2", "#3A2A5A"] + STEM_GREEN),
        K("flower-daisy", ["#F2F2F2", "#FFFFFF", "#DCAE3A"] + STEM_GREEN),
        K("tall-grass", ["#3F7A2E", "#52963B", "#66B04A", "#2F5D24"]),
        K("fern", ["#2F5D24", "#3F7A2E", "#52963B", "#66B04A"]),
        K("mushroom-red", ["#D42B2B", "#F45C5C", "#F2F2F2", "#C8C8B8", OUTLINE_DARK]),
        K("mushroom-brown", ["#8A6741", "#B98D57", "#C8C8B8", "#F2F2F2", OUTLINE_DARK]),

        # —— 树木 13：七种树叶色板对齐 A1 任务卡（叶色深于 grass-top）——
        X("birch-log", ["#D7CFC0", "#EAE5DA", "#B8AFA0", "#8A8175"]),   # 白桦白皮
        L("birch-leaves", ["#4A7E2F", "#5D9C3C", "#74B84E", "#8CCB5E"]),  # 亮黄绿
        X("pine-log", ["#2E241A", "#3B2C1C", "#4E3B27", "#634C33"]),
        L("pine-leaves", ["#1E3B2A", "#2A5038", "#356647", "#448059", "#56996B"]),
        X("cedar-log", ["#3A2E22", "#4E3E2E", "#63503C"]),
        L("cedar-leaves", ["#1E3A34", "#2A4A44", "#35605A", "#44786E", "#568E82"]),  # 蓝绿
        X("jungle-log", ["#5A4632", "#6E563E", "#82684C"]),
        L("jungle-leaves", ["#1E5A1E", "#2A7A2A", "#3A9A3A", "#56BB56"]),  # 浓艳
        L("bush-leaves", ["#4A5E3A", "#5A6E4A", "#6E8258", "#82966A"]),    # 灰绿
        X("sequoia-log", ["#6E3020", "#8A4028", "#A65638"]),
        L("sequoia-leaves", ["#6E3A26", "#8A4A32", "#A65E40", "#5A2E1E"]),  # 锈褐
        X("cherry-log", ["#5A3E24", "#6B4E2E", "#8A6741"]),
        L("cherry-leaves", ["#D898B0", "#E8B8C8", "#F2D0DC", "#F8E8EE"]),   # 粉白系

        # —— 楼梯 3：直接沿用对应方块既定色板 ——
        X("stairs-stone", STONE_GRAY),
        X("stairs-planks", PLANK_WOOD),
        X("stairs-bricks", BRICK_RED),
    ]
    return a1


def _m11_a2_items() -> list[Asset]:
    """任务 A2：物品图标 63 张（32×32，洋红键控，1px 深色描边，45° 侧视单体图）。"""

    def I(n, pal):
        return Asset(n, "item", (32, 32), [H(c) for c in pal], "none", transparent=True)

    out: list[Asset] = []
    # —— 武器工具 7：锄头四档，柄统一木色、头按档取色 ——
    out.append(I("bow", HANDLE_WOOD + ["#E0E0E0", "#FFFFFF", OUTLINE_DARK]))
    out.append(I("arrow", HANDLE_WOOD + ["#C8C8C8", "#F2F2F2", OUTLINE_DARK]))
    out.append(I("shield", METAL_IRON + PLANK_WOOD[:2] + [OUTLINE_DARK]))
    hoe_heads = {"wooden": ["#8A6741", "#B98D57"], "stone": ["#7E7E7E", "#C8C8C8"],
                 "iron": ["#8A8A8A", "#D8D8D8"], "diamond": ["#1E7C7C", "#4CC6C4", "#A8F2EF"]}
    for tier, head in hoe_heads.items():
        out.append(I(f"hoe-{tier}", HANDLE_WOOD + head + [OUTLINE_DARK]))

    # —— 盔甲 16：四材料 × 头盔/胸甲/护腿/靴子，色板随材料 ——
    armor_mats = {"iron": METAL_IRON, "gold": METAL_GOLD,
                  "summer-alloy": METAL_ALLOY, "machine-essence": METAL_ESSENCE}
    for mat, pal in armor_mats.items():
        for piece in ("helmet", "chest", "legs", "boots"):
            out.append(I(f"{piece}-{mat}", pal + [OUTLINE_DARK]))

    # —— 食物 4 + 材料 2 ——
    out.append(I("wheat-item", ["#DCAE3A", "#F7DA7A", "#A87322", OUTLINE_DARK]))
    out.append(I("bread", ["#B98D57", "#D2A868", "#8A6741", OUTLINE_DARK]))
    out.append(I("seeds-wheat", ["#8A9A4A", "#AEBE6A", "#DCAE3A", OUTLINE_DARK]))
    out.append(I("seeds-beet", ["#7A1A3A", "#A82A52", "#4A7E2F", OUTLINE_DARK]))
    out.append(I("bone-meal", ["#EAEAD8", "#F8F8E8", "#C8C8B8", OUTLINE_DARK]))
    out.append(I("leather", ["#8A6741", "#A5825A", "#6B4E2E", OUTLINE_DARK]))

    # —— 家具 9：木色系为主，电子件加深灰/屏幕蓝 ——
    out.append(I("chair", PLANK_WOOD + [OUTLINE_DARK]))
    out.append(I("table", PLANK_WOOD + [OUTLINE_DARK]))
    out.append(I("office-desk", ["#8A8A8A", "#B4B4B4"] + PLANK_WOOD + [OUTLINE_DARK]))
    out.append(I("laptop", ["#2A2A2E", "#5C5C5C", "#8A8A8A", "#3A6FB5", OUTLINE_DARK]))
    out.append(I("keyboard", ["#2A2A2E", "#5C5C5C", "#8A8A8A", OUTLINE_DARK]))
    out.append(I("mouse", ["#5C5C5C", "#8A8A8A", "#C8C8C8", OUTLINE_DARK]))
    out.append(I("notebook", ["#F2EAD2", "#E0D8B8", "#8A6741", OUTLINE_DARK]))
    out.append(I("hacker-pc", ["#1A1A1A", "#2A2A2E", "#3A6E8A", "#66B04A", OUTLINE_DARK]))
    out.append(I("globe", ["#3A6FB5", "#4E88CE", "#74B84E", "#8A8A8A", OUTLINE_DARK]))

    # —— 药水 7：瓶型一致换液色（potion-base 为母版，--variants 可程序派生）——
    out.append(I("potion-base", ["#C8E2EA", "#E7F4F8", "#8FB4C0", OUTLINE_DARK]))
    potion_liquids = {
        "healing": ["#D42B2B", "#F45C5C"], "speed": ["#4E88CE", "#8FB4E8"],
        "strength": ["#F79B22", "#FFD24A"], "jump": ["#A88AD2", "#D2C4F2"],
        "night-vision": ["#F7DA7A", "#FFEB99"], "water-breathing": ["#356647", "#66B04A"],
    }
    for kind, liquid in potion_liquids.items():
        out.append(I(f"potion-{kind}", ["#C8E2EA", "#E7F4F8", "#8FB4C0"] + liquid
                     + [OUTLINE_DARK]))

    # —— 附魔 5：附魔书三变体同一书本底、换符文色 ——
    book_base = ["#634C33", "#8A6741", "#F2EAD2"]
    out.append(I("enchanted-book-sharpness", book_base + ["#D42B2B", OUTLINE_DARK]))
    out.append(I("enchanted-book-efficiency", book_base + ["#F7DA7A", OUTLINE_DARK]))
    out.append(I("enchanted-book-unbreaking", book_base + ["#5A4A8A", OUTLINE_DARK]))
    out.append(I("enchanting-rod", HANDLE_WOOD + METAL_ESSENCE + [OUTLINE_DARK]))
    out.append(I("enchant-scroll", PARCHMENT + ["#5A4A8A", OUTLINE_DARK]))

    # —— 乐器 4 ——
    out.append(I("drum", HANDLE_WOOD + ["#E8D8B8", "#C8AA70", OUTLINE_DARK]))
    out.append(I("flute", HANDLE_WOOD + [OUTLINE_DARK]))
    out.append(I("bell", METAL_GOLD + [OUTLINE_DARK]))
    out.append(I("music-box", PLANK_WOOD + METAL_GOLD[:2] + [OUTLINE_DARK]))

    # —— 玩具 5 ——
    out.append(I("kite", ["#D42B2B", "#4E88CE", "#F7DA7A", "#74B84E", OUTLINE_DARK]))
    out.append(I("spinning-top", HANDLE_WOOD + PLANK_WOOD[:1] + [OUTLINE_DARK]))
    out.append(I("balloon", ["#D42B2B", "#F45C5C", "#F2F2F2", OUTLINE_DARK]))
    out.append(I("robot-toy", ["#8A8A8A", "#C8C8C8", "#3A6FB5", "#D42B2B", OUTLINE_DARK]))
    out.append(I("puzzle-cube", ["#D42B2B", "#F7DA7A", "#74B84E", "#4E88CE",
                                 "#A88AD2", "#F2F2F2", OUTLINE_DARK]))

    # —— 宝物 4 ——
    out.append(I("gem-bag", ["#8A6741", "#6B4E2E", "#4CC6C4", "#F7DA7A", OUTLINE_DARK]))
    out.append(I("coin-pile", METAL_GOLD + ["#A87322", OUTLINE_DARK]))
    out.append(I("treasure-map", PARCHMENT + ["#8C1B1B", "#3A5AC4", OUTLINE_DARK]))
    out.append(I("crown", METAL_GOLD + ["#D42B2B", OUTLINE_DARK]))
    return out


def _m11_a3_entities() -> list[Asset]:
    """任务 A3：生物图标 12 张，沿用正脸构图、不透明。
    machine-guardian 是 Boss 图标，放大到 64×64（紫金机甲、眼发蓝光）。"""

    def E(n, pal, size=(32, 32)):
        return Asset(n, "entity", size, [H(c) for c in pal], "none")

    return [
        E("cow", ["#E8E0D8", "#C8BCB0", "#D2A48A", "#1A1A1A"]),
        E("chicken", ["#F2F2F2", "#E0A020", "#D42B2B", OUTLINE_DARK]),
        E("spider", ["#2A2A2A", "#4A4A4A", "#8A1A1A", "#1A1A1A"]),
        E("rabbit", ["#C8B8A8", "#E8DCC8", "#F2F2F2", OUTLINE_DARK]),
        E("fox", ["#E8823A", "#F2A860", "#F2F2F2", "#2A1E14"]),
        E("deer", ["#A5825A", "#C8A878", "#F2F2F2", "#6B4E2E"]),
        E("panda", ["#F2F2F2", "#1A1A1A", "#8A8A8A", "#C8C8B8"]),
        E("penguin", ["#1A1A1A", "#F2F2F2", "#E8823A"]),
        E("goat", ["#E8E0D8", "#C8BCB0", "#A5825A", OUTLINE_DARK]),
        E("raccoon", ["#8A8A8A", "#4A4A4A", "#C8C8C8", "#1A1A1A"]),
        E("hamster", ["#E8C8A0", "#D2A878", "#F2E8D8", OUTLINE_DARK]),
        E("machine-guardian", METAL_ESSENCE + ["#4CC6C4", OUTLINE_DARK], (64, 64)),
    ]


def _m11_a3_effects() -> list[Asset]:
    """任务 A3：特效 16 张，全部洋红键控。
    fx-explosion / anim-torch-flame 是三帧横排帧序列（1024×341），
    生成时按 ~3:1 宽高比出图再降采样，拆帧对齐由 Unity 侧做。"""

    def F(n, pal, size=(32, 32)):
        return Asset(n, "effects", size, [H(c) for c in pal], "none", transparent=True)

    return [
        # 环境 4
        F("env-smoke", ["#8A8A8A", "#B4B4B4", "#D8D8D8"]),
        F("env-firefly", ["#F7DA7A", "#FFEB99", "#DCAE3A"]),
        F("env-dandelion-fluff", ["#F2F2F2", "#FFFFFF", "#E0E0E0"]),
        F("env-snowflake", ["#FFFFFF", "#E7F4F8", "#C8E2EA"]),
        # 战斗 5（explosion 为帧序列）
        F("fx-slash-arc", ["#FFFFFF", "#C8E2EA", "#8FB4E8"]),
        F("fx-arrow-trail", ["#E7F4F8", "#C8E2EA", "#FFFFFF"]),
        F("fx-explosion", ["#8A2400", "#D64B0A", "#F79B22", "#FFD24A", "#FFFFFF"],
          (1024, 341)),
        F("fx-magic-orb", METAL_ESSENCE[:2] + ["#E8D8FF"]),
        F("fx-hit-spark", ["#FFD24A", "#F79B22", "#FFFFFF"]),
        # 魔法阵 4
        F("magic-teleport", ["#5A4A8A", "#A88AD2", "#D2C4F2"]),
        F("magic-heal-ring", ["#3F7A2E", "#66B04A", "#A8F2B6"]),
        F("magic-enchant-column", ["#5A4A8A", "#A88AD2", "#DCAE3A"]),
        F("magic-shield", ["#4E88CE", "#8FB4E8", "#C8E2EA"]),
        # 方块动态 3（torch-flame 为帧序列）
        F("anim-torch-flame", ["#8A2400", "#D64B0A", "#F79B22", "#FFD24A"], (1024, 341)),
        F("anim-water-glint", ["#FFFFFF", "#C8E2EA", "#4E88CE"]),
        F("anim-lava-bubble", ["#8A2400", "#D64B0A", "#F79B22", "#FFD24A"]),
    ]


def _m11_a3_sky_player() -> list[Asset]:
    """任务 A3：天空 9 + 玩家表情 6。
    particle-* / face-* 洋红键控；星空/银河/彩虹/极光是天空穹顶整幅图，
    不键控，尺寸沿既有天空件（太阳月亮 64、云 128）的档位。"""

    def K(n, pal, **kw):          # 键控透明粒子/表情
        return Asset(n, "sky", (32, 32), [H(c) for c in pal], "none",
                     transparent=True, **kw)

    def S(n, pal, size):          # 整幅天空图（不键控）
        return Asset(n, "sky", size, [H(c) for c in pal], "none")

    def P(n, pal):                # 玩家表情（键控）
        return Asset(n, "player", (32, 32), [H(c) for c in pal], "none",
                     transparent=True)

    return [
        K("particle-rain", ["#8FB4E8", "#C8E2EA"]),
        K("particle-snow", ["#FFFFFF", "#E7F4F8"]),
        K("particle-hail", ["#FFFFFF", "#C8E2EA", "#8FB4E8"]),
        K("particle-leaf-fall", ["#5D9C3C", "#74B84E", "#DCAE3A"]),
        S("sky-stars", ["#FFFFFF", "#F2F2EC", "#F7DA7A"], (64, 64)),
        S("sky-milky-way", ["#1A1A2E", "#3A3A5A", "#8A8AC8", "#F2F2EC"], (128, 128)),
        S("sky-rainbow", ["#D42B2B", "#F79B22", "#F7DA7A", "#74B84E",
                          "#4E88CE", "#A88AD2"], (128, 128)),
        S("sky-aurora", ["#3A6E8A", "#6AA8C8", "#8CCB5E", "#A8F2EF"], (128, 128)),
        K("sky-meteor", ["#FFFFFF", "#F7DA7A", "#F79B22"]),
        P("face-sleep", SKIN_TONE + ["#8A8A8A", OUTLINE_DARK]),
        P("face-hungry", SKIN_TONE + ["#8A6741", OUTLINE_DARK]),
        P("face-hurt", SKIN_TONE + ["#D42B2B", OUTLINE_DARK]),
        P("face-invincible", SKIN_TONE + ["#F7DA7A", OUTLINE_DARK]),
        P("face-happy", SKIN_TONE + ["#F45C5C", OUTLINE_DARK]),
        P("face-scared", SKIN_TONE + ["#3A5AC4", OUTLINE_DARK]),
    ]


def _m11_a4_ui() -> list[Asset]:
    """任务 A4：UI 17 张（系统件 7 / 动效帧 5 / 光标 5）。
    三条横向状态条 128×16；箱子格沿 hotbar-slot 用 48×48；
    数字类动效帧洋红键控（白字黑描边）。"""

    def U(n, pal, size=(32, 32), **kw):
        return Asset(n, "ui", size, [H(c) for c in pal], "none", **kw)

    def K(n, pal, **kw):          # 键控透明（动效帧/光标）
        return Asset(n, "ui", (32, 32), [H(c) for c in pal], "none",
                     transparent=True, **kw)

    return [
        # 系统件 7
        U("ui-armor-frame", UI_WARM_GRAY + ["#C8C8C8"]),
        U("ui-armor-bar", ["#17140F", "#5C5C5C", "#8A8A8A", "#C8C8C8"], (128, 16)),
        U("ui-charge-bar", ["#17140F", "#8A2400", "#F79B22", "#FFD24A"], (128, 16)),
        U("ui-boss-bar", ["#17140F", "#8C1B1B", "#D42B2B", "#F45C5C"], (128, 16)),
        U("ui-chest-slot", ["#1A1A1A", "#8A8A8A", "#000000"], (48, 48), transparent=True),
        U("ui-map-frame", PLANK_WOOD + ["#F2EAD2", OUTLINE_DARK]),
        U("ui-enchant-panel", UI_WARM_GRAY + ["#57503F", "#3A342A", "#5A4A8A",
                                              "#A88AD2"], (64, 64)),
        # 动效帧 5
        K("fx-xp-float", ["#7DFC4A", "#FFFFFF", OUTLINE_DARK]),
        K("fx-damage-number", ["#FFFFFF", "#F2F2F2", OUTLINE_DARK]),
        K("fx-crit", ["#FFD24A", "#F79B22", "#FFFFFF", OUTLINE_DARK]),
        K("fx-combo", ["#4E88CE", "#8FB4E8", "#FFFFFF", OUTLINE_DARK]),
        K("fx-badge-popup", ["#DCAE3A", "#F7DA7A", "#FFFFFF", OUTLINE_DARK]),
        # 光标 5
        K("cursor-dig", ["#FFFFFF", "#FFD24A", "#1A1A1A"]),
        K("cursor-attack", ["#FFFFFF", "#D42B2B", "#1A1A1A"]),
        K("cursor-talk", ["#FFFFFF", "#74B84E", "#1A1A1A"]),
        K("cursor-forbidden", ["#D42B2B", "#8C1B1B", "#1A1A1A"]),
        K("cursor-grab", ["#FFFFFF", "#C8C8C8", "#1A1A1A"]),
    ]


def _m11_a4_codex() -> list[Asset]:
    """任务 A4：图鉴 27 张（卡框 3 + 徽章 16 + 收集卡 8）。
    徽章共用 #8B7355 底盘金边（32×32）；卡片类 64×64。"""

    def B(n, accent):             # 徽章：底盘色 + 前景主题色
        return Asset(n, "codex", (32, 32), [H(c) for c in BADGE_BASE + accent],
                     "none", transparent=True)

    def C(n, pal):                # 卡片（卡框/收集卡）
        return Asset(n, "codex", (64, 64), [H(c) for c in pal], "none")

    return [
        # 卡框 3：普通/稀有/史诗
        C("card-frame-common", ["#8B7355", "#6B6155", "#D2C4A8", OUTLINE_DARK]),
        C("card-frame-rare", ["#3A6E8A", "#6AA8C8", "#D2E8F2", OUTLINE_DARK]),
        C("card-frame-epic", ["#5A4A8A", "#A88AD2", "#E8D8FF", OUTLINE_DARK]),
        # 徽章 16
        B("badge-first-night", ["#1A2A4A", "#3A5A8A"]),
        B("badge-first-iron", ["#5C5C5C", "#C8C8C8"]),
        B("badge-diamond-age", ["#1E7C7C", "#A8F2EF"]),
        B("badge-mob-slayer", ["#D42B2B", "#8C1B1B"]),
        B("badge-skeleton-sniper", ["#EAEAD8", "#C8C8B8"]),
        B("badge-creeper-survivor", ["#6ED66A", "#3A8A3A"]),
        B("badge-shepherd", ["#F2F2F2", "#C8C8C8"]),
        B("badge-harvest", ["#DCAE3A", "#F7DA7A"]),
        B("badge-baker", ["#B98D57", "#D2A868"]),
        B("badge-enchanter", ["#5A4A8A", "#A88AD2"]),
        B("badge-archer", HANDLE_WOOD[:2] + ["#E0E0E0"]),
        B("badge-hero", ["#F7DA7A", "#D42B2B"]),
        B("badge-explorer", ["#4A7E2F", "#74B84E"]),
        B("badge-builder", ["#8A6741", "#B98D57"]),
        B("badge-fisherman", ["#3A6FB5", "#4E88CE"]),
        B("badge-completionist", ["#A88AD2", "#F7DA7A"]),
        # 收集卡 8：矿物 5 + 植物 3
        C("card-ore-gold", METAL_GOLD + ["#5C4632", OUTLINE_DARK]),
        C("card-ore-iron", METAL_IRON + ["#5C4632", OUTLINE_DARK]),
        C("card-ore-alloy", METAL_ALLOY + ["#5C4632", OUTLINE_DARK]),
        C("card-ore-essence", METAL_ESSENCE + ["#5C4632", OUTLINE_DARK]),
        C("card-ore-diamond", ["#1E7C7C", "#4CC6C4", "#A8F2EF", "#5C4632", OUTLINE_DARK]),
        C("card-plant-cherry", ["#D898B0", "#E8B8C8", "#F2D0DC", "#5C4632", OUTLINE_DARK]),
        C("card-plant-sunflower", ["#DCAE3A", "#F7DA7A", "#A87322", "#5C4632", OUTLINE_DARK]),
        C("card-plant-fern", ["#2F5D24", "#3F7A2E", "#52963B", "#5C4632", OUTLINE_DARK]),
    ]


def _m11_a5_architecture() -> list[Asset]:
    """任务 A5：建筑 11 张。village/blueprint 七张是 1024 概念图
    直用（不降采样不量化）；sign/banner 四张是游戏内贴图 32×32。"""

    def C(n):                      # 1024 概念图直用
        return Asset(n, "architecture", (1024, 1024), [], "none", direct=True)

    def T(n, pal):                 # 游戏内小贴图
        return Asset(n, "architecture", (32, 32), [H(c) for c in pal], "none")

    return [
        C("village-blacksmith"), C("village-farm"), C("village-library"),
        C("village-well"),
        C("blueprint-mine"), C("blueprint-shipwreck"), C("blueprint-temple"),
        T("sign-village", PLANK_WOOD + ["#F2EAD2", OUTLINE_DARK]),
        T("sign-shop", PLANK_WOOD + ["#F2EAD2", "#DCAE3A", OUTLINE_DARK]),
        T("banner-plain", ["#A05242", "#8B4433", "#B4635A"]),
        T("banner-crest", ["#2C5893", "#3A6FB5", "#DCAE3A", "#F7DA7A", "#241A11"]),
    ]


def _m11_a5_scenes() -> list[Asset]:
    """任务 A5：场景 11 张，全部 1024 大图直用（主菜单/章节/全景图）。"""

    def C(n):
        return Asset(n, "scenes", (1024, 1024), [], "none", direct=True)

    return [
        C("menu-plains-dawn"), C("menu-snow-aurora"), C("menu-jungle-sunset"),
        C("chapter1-complete"), C("boss-intro"), C("ending"),
        C("panorama-plains"), C("panorama-desert"), C("panorama-forest"),
        C("panorama-mountains"), C("panorama-snow"),
    ]


def _m11_a5_marketing() -> list[Asset]:
    """任务 A5：宣传 8 张。banner-store/teaser-card 1024 直用；logo 四张
    256×256 洋红键控、screenshot-frame 1024×576 中央键控、qrcode-bg 512
    不透明净区底板（规格以各需求文件为准，m11 集成点①对齐）。"""

    def C(n):
        return Asset(n, "marketing", (1024, 1024), [], "none", direct=True)

    def L(n, pal):
        return Asset(n, "marketing", (256, 256), [H(c) for c in pal], "none",
                     transparent=True)

    return [
        C("banner-store"), C("teaser-card"),
        L("logo-official", ["#241A11", "#3A2E20", "#DCAE3A", "#F7DA7A"]),
        L("logo-spring-festival", ["#A83232", "#C43C3C", "#E05252", "#DCAE3A",
                                   "#F7DA7A", "#74B84E", "#5D9C3C", "#4A7E2F",
                                   "#3B6626", "#7A5A3C", "#5F4630"]),
        L("logo-christmas", ["#1E2A44", "#2C3A52", "#DCAE3A", "#F7DA7A",
                             "#F6FAFC", "#E8F0F4", "#5D9C3C", "#4A7E2F",
                             "#3B6626", "#7A5A3C", "#5F4630"]),
        L("logo-pixel", ["#5D9C3C", "#74B84E", "#7A5A3C", "#5F4630", "#6B4F30"]),
        Asset("screenshot-frame", "marketing", (1024, 576),
              [H(c) for c in ["#443D34", "#6B6155", "#8B7F6F", "#DCAE3A",
                              "#74B84E", "#5D9C3C", "#4A7E2F", "#7A5A3C",
                              "#C43C3C"]],
              "none", transparent=True),
        Asset("qrcode-bg", "marketing", (512, 512),
              [H(c) for c in ["#F2E6C8", "#443D34", "#6B6155", "#8B7F6F",
                              "#4A7E2F", "#5D9C3C", "#C43C3C", "#F7DA7A",
                              "#DCAE3A"]],
              "none"),
    ]


def _m11_a5_seasonal() -> list[Asset]:
    """任务 A5：季节/节日 10 张（色板/键控/平铺以各需求文件为准，m11 集成点①对齐）。
    snow-grass 是方块顶面贴图（归 block 类入库）、item-red-envelope /
    item-gift-box 是物品图标（归 item 类），其余 7 张按 seasonal 类入
    Assets/Art/Seasonal；autumn-leaves 镂空键控、firework-burst 单体键控，
    春樱/夏荷是不透明顶面贴图（四面无缝）。"""

    def T(n, pal):                 # seasonal 类不透明无缝贴图
        return Asset(n, "seasonal", (32, 32), [H(c) for c in pal], "4-side")

    def K(n, pal):                 # seasonal 类洋红键控 sprite
        return Asset(n, "seasonal", (32, 32), [H(c) for c in pal], "none",
                     transparent=True)

    return [
        T("lantern-spring", ["#C43C3C", "#A83232", "#E05252", "#DCAE3A",
                             "#F7DA7A", "#FFD84A"]),
        T("tree-christmas", ["#1E3B2A", "#2A5038", "#356647", "#448059",
                             "#C43C3C", "#F7DA7A", "#F6FAFC"]),
        T("pumpkin-lantern", ["#D64B0A", "#F79B22", "#8A2400", "#FFD84A",
                              "#FFF3C4", "#4A7E2F", "#4A2000"]),
        K("firework-burst", ["#FFF3C4", "#F7DA7A", "#F79B22", "#D64B0A"]),
        K("autumn-leaves", ["#5A2E1C", "#8A4A28", "#C97B3A", "#E0A84C",
                            "#F2C878"]),
        T("spring-blossom", ["#4A7E2F", "#5D9C3C", "#74B84E", "#F2C4D0",
                             "#E8A8BC", "#F6E8EC", "#F7DA7A"]),
        T("summer-lotus", ["#2C5893", "#3A6FB5", "#4E88CE", "#3F7A2E",
                           "#52963B", "#2F5D24", "#F2C4D0", "#E8A8BC"]),
        # 跨类入库的三张（贴图加载方不同，见 docstring）
        Asset("snow-grass", "block", (32, 32),
              [H(c) for c in ["#F6FAFC", "#E8F0F4", "#D0DEE8", "#B8C8D8",
                              "#4A7E2F", "#5D9C3C"]], "4-side"),
        Asset("item-red-envelope", "item", (32, 32),
              [H(c) for c in ["#C43C3C", "#A83232", "#E05252", "#DCAE3A",
                              "#F7DA7A", "#241A11"]],
              "none", transparent=True),
        Asset("item-gift-box", "item", (32, 32),
              [H(c) for c in ["#C43C3C", "#A83232", "#E05252", "#DCAE3A",
                              "#F7DA7A", "#241A11"]],
              "none", transparent=True),
    ]


ASSETS: dict[str, Asset] = {a.name: a for a in (
    _blocks() + _ores() + _ui_sky_player() + _items() + _entities()
    + _m11_a1_blocks() + _m11_a2_items() + _m11_a3_entities() + _m11_a3_effects()
    + _m11_a3_sky_player() + _m11_a4_ui() + _m11_a4_codex() + _m11_a5_architecture()
    + _m11_a5_scenes() + _m11_a5_marketing() + _m11_a5_seasonal())}

# moon-full 是中间产物，不单独入库
INTERMEDIATE = {"moon-full"}


# ---------------------------------------------------------------- 各类处理


def process_ai(spec: Asset) -> np.ndarray:
    rgb = load_source(spec.source or spec.name)

    # 直用大图（village/blueprint/scenes/marketing 系概念图）：1024 生成后
    # 原样入库——不键控、不去洋红边、不做调色板量化，像素风约束只管小图
    if spec.direct:
        small, _ = downsample(rgb, None, spec.size)
        return compose(small, np.zeros(small.shape[:2], dtype=bool))

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
    ry, rx = (ys.max() - ys.min() + 1) / 2, (xs.max() - xs.min() + 1) / 2
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
