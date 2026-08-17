"""milestone-11 W2-2：生成附魔台三张贴图（程序占位）。

背景：m11 第 2 波注册附魔台方块（blocks/enchanting_table.json）+ 配套物品
（items/enchanting_table.json），正式美术未出。为了让 BlockDefinitionFilesTests
同款的贴图存在性守卫先通过、实机不显示 missing 棕块，按 m10 批
（gen_m10_ore_placeholders.py / gen_m10_item_placeholders.py）同模式程序生成：

- 方块 top（32×32 不透明）：深紫黑台面噪点底 + 中央青蓝菱形宝石 + 四角符文点，
  装饰全部离边 ≥4 像素 → 四边无缝（逐像素哈希噪点天然无缝，图案不跨边）
- 方块 side（32×32 不透明）：逐像素复用 planks.png 做木身（与既有木板同源），
  顶部 9 行压深紫黑台沿 + 一行金色符文点（左右无缝：条带整行恒定，照
  grass-side「顶条带」先例）
- 物品图标（16×16 RGBA）：书本 + 菱形宝石的 ASCII 形状图（m10 批同规格）

确定性：同输入必得同输出，不持有随机数对象（与 ValueNoise2D /
gen_m10_*.py 同约定）。正式美术到位后按
art/requests/blocks/enchanting-table-top.md /
art/requests/blocks/enchanting-table-side.md /
art/requests/items/enchanting-table.md
替换即可，本脚本只是留档生成过程，不在任何构建/测试链路里。

用法（仓库根目录）：python art/scripts/gen_m11_enchant_placeholders.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BLOCK_DIR = ROOT / "Assets" / "StreamingAssets" / "blocks" / "textures"
ITEM_DIR = ROOT / "Assets" / "StreamingAssets" / "items" / "textures"
BLOCK_SIZE = 32
ITEM_SIZE = 16

# 附魔台家族调色板（与 art/requests/blocks/enchanting-table.md 一致）：
# 台面深紫黑三阶 / 宝石青蓝三阶（青金石联动）/ 符文金
DEEP = ("#1F1430", "#32224D", "#4A3568")
GEM = ("#2C4E8C", "#3E6FBF", "#7FA8E8")
RUNE = "#DCAE3A"


def hash32(x: int, y: int, salt: int) -> int:
    """确定性整数哈希（与 gen_m10_item_placeholders.py 同一实现，不持随机状态）。"""
    n = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFFFFFF


def hex_rgb(s: str) -> tuple[int, int, int]:
    return (int(s[1:3], 16), int(s[3:5], 16), int(s[5:7], 16))


def shade(palette: tuple[str, str, str], x: int, y: int, salt: int) -> tuple[int, int, int]:
    """哈希在三阶色里取色：主色加权 ×2，暗亮各 ×1（噪点颗粒与既有方块同 1-2 像素级）。"""
    weighted = (palette[1], palette[1], palette[0], palette[2])
    return hex_rgb(weighted[hash32(x, y, salt) % len(weighted)])


def gen_top() -> Image.Image:
    """台面：深紫黑噪点 + 中央菱形宝石（离边 ≥6）+ 四角符文点（离边 ≥5）。"""
    img = Image.new("RGBA", (BLOCK_SIZE, BLOCK_SIZE))
    px = img.load()
    for y in range(BLOCK_SIZE):
        for x in range(BLOCK_SIZE):
            px[x, y] = shade(DEEP, x, y, salt=0x7AB1) + (255,)

    # 中央菱形宝石：|dx|+|dy| <= 5，外圈 1 像素暗边，中心 1 像素高光
    c = BLOCK_SIZE // 2
    light, main, deep = (hex_rgb(s) for s in (GEM[2], GEM[1], GEM[0]))
    for dy in range(-6, 7):
        for dx in range(-6, 7):
            ring = abs(dx) + abs(dy)
            if ring <= 5:
                px[c + dx, c + dy] = main + (255,)
            elif ring == 6:
                px[c + dx, c + dy] = deep + (255,)
    px[c, c] = light + (255,)

    # 四角符文点（金色 2×2，离边 5）
    gold = hex_rgb(RUNE) + (255,)
    for cx, cy in ((5, 5), (BLOCK_SIZE - 7, 5), (5, BLOCK_SIZE - 7), (BLOCK_SIZE - 7, BLOCK_SIZE - 7)):
        for dy in (0, 1):
            for dx in (0, 1):
                px[cx + dx, cy + dy] = gold
    return img


def gen_side() -> Image.Image:
    """侧面：planks.png 逐像素打底（木身与既有木板同源）+ 顶部 9 行台沿 + 金色符文行。"""
    planks = Image.open(BLOCK_DIR / "planks.png").convert("RGBA")
    img = planks.copy()
    px = img.load()
    for y in range(9):
        for x in range(BLOCK_SIZE):
            px[x, y] = shade(DEEP, x, y, salt=0x51DE) + (255,)
    # 第 9 行（台沿与木身的分界）撒金色符文点：间隔 4 的确定性哈希点
    gold = hex_rgb(RUNE) + (255,)
    for x in range(BLOCK_SIZE):
        if hash32(x, 0, salt=0xF00D) % 4 == 0:
            px[x, 9] = gold
    return img


# 物品图标：摊开的书 + 上方菱形宝石（m10 批同规格 ASCII 形状图，'.' 透明）
ITEM_SHAPE = [
    "................",
    ".......G........",
    "......GLG.......",
    ".....GLLLG......",
    "......GLG.......",
    ".......G........",
    "................",
    "..WWWWWWWWWWWW..",
    ".WPPPPPPPPPPPPD.",
    ".WPPPPPPPPPPPDD.",
    ".WPPPPPPPPPPPDD.",
    ".WPPPPPPPPPPPDD.",
    ".WPPPPPPPPPPPDD.",
    ".WDDDDDDDDDDDDD.",
    "..DDDDDDDDDDDD..",
    "................",
]

ITEM_PALETTE = {
    # 书页：暖白纸三阶
    "P": ["#E8E0CC", "#E8E0CC", "#D8CDB4", "#F4EEDC"],
    # 封皮：木板黄系（与 planks 同源偏暗）
    "W": ["#8A6741", "#8A6741", "#6F5430", "#9C7549"],
    "D": ["#6F5430", "#6F5430", "#5A4326", "#8A6741"],
    # 宝石：青蓝三阶
    "G": ["#2C4E8C", "#3E6FBF", "#3E6FBF", "#7FA8E8"],
    "L": ["#7FA8E8", "#7FA8E8", "#3E6FBF", "#3E6FBF"],
}


def gen_item() -> Image.Image:
    img = Image.new("RGBA", (ITEM_SIZE, ITEM_SIZE), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(ITEM_SHAPE):
        assert len(row) == ITEM_SIZE, f"形状图第 {y} 行必须 {ITEM_SIZE} 列"
        for x, ch in enumerate(row):
            if ch == ".":
                continue
            shades = ITEM_PALETTE[ch]
            color = hex_rgb(shades[hash32(x, y, salt=0x80C5) % len(shades)])
            px[x, y] = color + (255,)
    return img


def verify_no_magenta(img: Image.Image) -> None:
    """占位图禁止出现洋红（键控保留色）。"""
    w, h = img.size
    px = img.load()
    for y in range(h):
        for x in range(w):
            r, g, b, _ = px[x, y]
            assert not (r == 255 and g == 0 and b == 255), \
                f"占位图 ({x},{y}) 出现了键控保留色 #FF00FF"


def main() -> None:
    outputs = {
        BLOCK_DIR / "enchanting-table-top.png": gen_top(),
        BLOCK_DIR / "enchanting-table-side.png": gen_side(),
        ITEM_DIR / "enchanting_table.png": gen_item(),
    }
    for path, img in outputs.items():
        verify_no_magenta(img)
        img.save(path)
        print(f"{path.relative_to(ROOT)}  已生成（{img.width}×{img.height} RGBA 程序占位）")


if __name__ == "__main__":
    main()
