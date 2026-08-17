"""milestone-10 C1：补 6 张装备物品贴图（程序占位，消热键栏品红块）。

背景：m10 C1 三新属性落地时从零建了六件装备——金剑/金镐（A3 fix1 勘误：
spec §3「既有金装备」不实，金系装备本 task 才建）、夏季合金剑/镐（移速 +5%）、
机元剑/镐（生命上限 +2）。它们的 texture 名在 items/textures/ 没有 PNG，
做出来拿在手里会显示品红占位块（m6 A3 / m10 A1 fix1 同款教训）。

本脚本产出 16×16、32 位 RGBA、硬边无抗锯齿的占位图，与既有物品图标同一规格：
ASCII 形状图 + 语义调色板，逐像素整数哈希在色阶间取色，确定性生成
（同输入必得同输出，不持有随机数对象——与 ValueNoise2D 同约定）。

剑/镐共用一套剪影（对角挥出的刃 / 拱形镐头 + 斜柄），材料按各自矿石掉落物
图标（gen_m10_item_placeholders.py）的色系上色，四系并排一眼分队：
金亮黄（#E8C04A 系）、夏季合金青蓝（#4A9AB8 系）、机元紫灰（#7A5A9A 系）；
握柄一律木棕 #634C33（与既有六系剑/镐的柄色一致）。
正式美术到位后按 art/requests/items/m10-gear-placeholders.md 替换即可，
本脚本只是留档生成过程，不在任何构建/测试链路里。

用法（仓库根目录）：python art/scripts/gen_m10_gear_placeholders.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ITEM_DIR = ROOT / "Assets" / "StreamingAssets" / "items" / "textures"

SIZE = 16


def hash_pixel(x: int, y: int, salt: int) -> int:
    """确定性整数哈希（与 Core 的 ValueNoise2D 同思路，不持有随机状态）。"""
    n = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFFFFFF


def hex_rgb(s: str) -> tuple[int, int, int]:
    return (int(s[1:3], 16), int(s[3:5], 16), int(s[5:7], 16))


def render(shape: list[str], palette: dict[str, list[str]], salt: int) -> Image.Image:
    """ASCII 形状图 → PNG。形状图每字符一个像素，'.' 为透明。

    palette 的 key 是形状图里的角色字符，value 是该角色的色阶列表（带重复
    即加权：主色重复多次，明暗阶各一份），哈希在色阶里取。
    """
    assert len(shape) == SIZE, f"形状图必须 {SIZE} 行，实际 {len(shape)}"
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(shape):
        assert len(row) == SIZE, f"第 {y} 行必须 {SIZE} 列，实际 {len(row)}"
        for x, ch in enumerate(row):
            if ch == ".":
                continue
            shades = palette[ch]
            color = hex_rgb(shades[hash_pixel(x, y, salt) % len(shades)])
            px[x, y] = color + (255,)
    return img


# 剑：对角挥出的刃（左上受光 L / 刃身 S / 右下暗边 B），护手 G + 木柄 H
SWORD = [
    "................",
    "............LSB.",
    "...........LSSB.",
    "..........LSSB..",
    ".........LSSB...",
    "........LSSB....",
    ".......LSSB.....",
    "..G...LSSB......",
    "...G.LSSB.......",
    "....GLSB........",
    "...GGGS.........",
    "..HHGG..........",
    ".HHH............",
    ".HH.............",
    "................",
    "................",
]

# 镐：拱形镐头（顶部受光 L / 主色 S / 暗边 B）+ 斜向木柄 H
PICKAXE = [
    "................",
    "......BBBBBB....",
    "....BBSLLSSBB...",
    "...BSS.....SSB..",
    "..BS....HH..SB..",
    "..BS...HH...SB..",
    ".BS...HH....SB..",
    ".B...HH......B..",
    "....HH..........",
    "...HH...........",
    "..HH............",
    ".HH.............",
    "................",
    "................",
    "................",
    "................",
]

# 木柄：与既有六系剑/镐一致（postprocess_art.py ITEMS 表首色）
HANDLE = ["#634C33", "#7A6042"]

# 每系：(刃/镐头主色 S, 受光 L, 暗边 B, 盐值基数)——色系对齐矿石掉落物图标
MATERIALS = [
    ("gold",           ["#E8C04A", "#E8C04A", "#E8C04A", "#C09A34"],
                       ["#F7E08A", "#F7E08A", "#FFEFA8"],
                       ["#C09A34", "#C09A34", "#A8842E"], 31),
    ("summer_alloy",   ["#4A9AB8", "#4A9AB8", "#4A9AB8", "#35758E"],
                       ["#78C2DC", "#78C2DC", "#9AD8EA"],
                       ["#35758E", "#35758E", "#2A5F75"], 32),
    ("machine_essence", ["#7A5A9A", "#7A5A9A", "#7A5A9A", "#5E4478"],
                        ["#9C82BC", "#9C82BC", "#B9A2D4"],
                        ["#5E4478", "#5E4478", "#4A3560"], 33),
]


def main() -> None:
    ITEM_DIR.mkdir(parents=True, exist_ok=True)
    for name, main_shades, light_shades, dark_shades, base_salt in MATERIALS:
        for kind, shape, salt in (("sword", SWORD, base_salt), ("pickaxe", PICKAXE, base_salt + 3)):
            palette = {"S": main_shades, "L": light_shades, "B": dark_shades,
                       "G": dark_shades, "H": HANDLE}
            img = render(shape, palette, salt)
            img.save(ITEM_DIR / f"{name}_{kind}.png")
            print(f"{name}_{kind}.png  程序生成 {img.size[0]}x{img.size[1]} RGBA")


if __name__ == "__main__":
    main()
