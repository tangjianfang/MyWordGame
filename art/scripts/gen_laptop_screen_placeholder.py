#!/usr/bin/env python3
"""
笔记本/黑客电脑屏幕贴图占位生成器（av W2-11）。

确定性哈希生成 32×32 像素占位（深蓝底 + 绿色代码字符竖条），写入
Assets/StreamingAssets/blocks/textures/laptop-screen.png。

正式美术未到位前给实机一个像样的占位——VideoScreenSystem 加载后会把
贴图槽的 mainTexture 换成 VideoPlayer 的 RenderTexture（mp4 缺失时回退此占位）。

模式照 art/scripts/gen_m6_placeholders.py 的 hash_pixel：每像素颜色由坐标
整数哈希确定，重跑产物一致、跨平台一致。
"""
from __future__ import annotations

import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("缺 Pillow：pip install Pillow")

PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
OUT_PATH = PROJECT_ROOT / "Assets" / "StreamingAssets" / "blocks" / "textures" / "laptop-screen.png"

# 调色板（来自 art/requests/items/laptop-screen.md）
NAVY = (0x0B, 0x1E, 0x3A)     # 深蓝底
GREEN_MAIN = (0x3F, 0xBB, 0x5A)
GREEN_LIGHT = (0x7F, 0xE8, 0x9A)
GREEN_BRIGHT = (0xC8, 0xFA, 0xCC)


def hash_pixel(x: int, y: int) -> int:
    """确定性整数哈希（项目铁律：不持有随机数对象）。"""
    h = (x * 374761393 + y * 668265263) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h = (h ^ (h >> 16)) & 0xFFFFFFFF
    return h


def main() -> int:
    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    size = 32
    img = Image.new("RGB", (size, size), NAVY)
    pixels = img.load()

    # 4 列绿色字符竖条（每条宽度 4 px，中间空 4 px）
    for col in range(4):
        x0 = 4 + col * 7
        for y in range(size):
            h = hash_pixel(x0, y)
            if h % 7 < 4:  # 4/7 的行亮绿色，模拟字符
                # 字符亮度按列决定
                if h % 3 == 0:
                    pixels[x0, y] = GREEN_BRIGHT
                elif h % 3 == 1:
                    pixels[x0, y] = GREEN_LIGHT
                else:
                    pixels[x0, y] = GREEN_MAIN

    img.save(OUT_PATH, "PNG")
    print(f"[av] 占位贴图写入 {OUT_PATH}")
    return 0


if __name__ == "__main__":
    sys.exit(main())