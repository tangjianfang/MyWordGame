#!/usr/bin/env python3
"""图鉴卡牌边框生成器（补丁计划 Task 3c · 2026-08-20）。

背景：AI 批次生成的 3 张卡框（card-frame-common/rare/epic）经像素级检查
**全图 0% 透明**——中心不透不能用做叠加框（视觉模型复判同结论）。
卡框是规则几何形状，程序生成分层边框反而精确对称、中心保证透明，
本脚本按各需求文件（art/requests/codex/card-frame-*.md）的分层规格直接产出：

  64×64 RGBA，边框总厚 6px：
    1. 外描边 1px  #17140F
    2. 亮线   1px  上/左（底/右回落主色）
    3. 主带   3px  稀有度主色
    4. 内描边 1px  #17140F
    5. 中心 52×52  全透明（alpha=0，供卡面内容透出）

稀有度配色（严格照需求文件调色板）：
    common 暖灰   主 #6B6155  亮 #8B7F6F
    rare   水蓝   主 #3A6FB5  亮 #4E88CE
    epic   紫罗兰 主 #7B4FA6  亮 #9B6FC9

确定性生成（规则形状无需哈希抖动），写入 Assets/Art/Codex/（注册表
postprocess_art.py 第 65 行的规范路径）。正式美术若日后补上带透明中心的
AI 版，直接覆盖同名文件即可。
"""
from __future__ import annotations

import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("缺 Pillow：pip install Pillow")

PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
OUT_DIR = PROJECT_ROOT / "Assets" / "Art" / "Codex"

SIZE = 64
BORDER = 6                 # 1 外描边 + 1 亮线 + 3 主带 + 1 内描边
EDGE = (0x17, 0x14, 0x0F, 0xFF)        # #17140F 不透明描边
TRANSPARENT = (0, 0, 0, 0)

FRAMES = {
    "card-frame-common": ((0x6B, 0x61, 0x55), (0x8B, 0x7F, 0x6F)),  # 暖灰
    "card-frame-rare":   ((0x3A, 0x6F, 0xB5), (0x4E, 0x88, 0xCE)),  # 水蓝
    "card-frame-epic":   ((0x7B, 0x4F, 0xA6), (0x9B, 0x6F, 0xC9)),  # 紫罗兰
}


def build(main_rgb: tuple, light_rgb: tuple) -> Image.Image:
    main = (*main_rgb, 0xFF)
    light = (*light_rgb, 0xFF)
    im = Image.new("RGBA", (SIZE, SIZE), TRANSPARENT)
    px = im.load()
    for y in range(SIZE):
        for x in range(SIZE):
            d = min(x, y, SIZE - 1 - x, SIZE - 1 - y)  # 距边深度 0..5
            if d == 0 or d == BORDER - 1:
                px[x, y] = EDGE                        # 外描边 / 内描边
            elif d == 1:
                px[x, y] = light if (x == d or y == d) else main  # 亮线只上/左
            elif d in (2, 3, 4):
                px[x, y] = main                        # 主带 3px
            # d >= BORDER 保持透明（中心挖空）
    return im


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, (main_rgb, light_rgb) in FRAMES.items():
        out = OUT_DIR / f"{name}.png"
        build(main_rgb, light_rgb).save(out)
        print(f"v {out.relative_to(PROJECT_ROOT)}  {SIZE}x{SIZE} 边框{BORDER}px 中心透明")


if __name__ == "__main__":
    main()
