"""milestone-10 C2：补 9 张物品贴图（程序占位，消热键栏品红块）。

背景：m10 C2 落地进阶合成链——粗金熔炼出金锭，四系剑/镐可「同材料 2 件升 1 件」
（加成翻倍，产物是独立物品 *_plus，displayName「XX+1」）。这些物品的 texture 名
在 items/textures/ 没有 PNG，做出来会显示品红占位块（m6 A3 / m10 A1 fix1 /
C1 同款教训）。

本脚本产出 16×16、32 位 RGBA、硬边无抗锯齿的占位图，与既有物品图标同一规格。
剑/镐剪影与配色整份沿用 gen_m10_gear_placeholders.py（C1 批），差异只有两点：

1. 升级件在**左上角叠一枚 3×3 浅金「+」徽记**——热键栏里图标 16px 太小，
   名字（「铁剑+1」）又只在合成界面悬停才可见，徽记是孩子分辨「升过级」的唯一线索
   （两套剪影的左上 3×3 恰好全透明，徽记不压武器本体）
2. 铁系升级件需要铁色板（C1 只建了金/合金/机元三系），灰阶对齐既有铁系图标；
   另产出金锭图标（粗金熔炼产物，梯形锭剪影 + 金色系）

确定性生成（同输入必得同输出，不持有随机数对象——与 ValueNoise2D 同约定）。
正式美术到位后按 art/requests/items/m10-c2-upgrade-placeholders.md 替换即可，
本脚本只是留档生成过程，不在任何构建/测试链路里。

用法（仓库根目录）：python art/scripts/gen_m10_upgrade_placeholders.py
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


# 剑：对角挥出的刃（左上受光 L / 刃身 S / 右下暗边 B），护手 G + 木柄 H（与 C1 批同图）
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

# 镐：拱形镐头（顶部受光 L / 主色 S / 暗边 B）+ 斜向木柄 H（与 C1 批同图）
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

# 金锭：梯形锭块（顶面受光 L / 侧面主色 S / 底与右暗边 B），对齐既有铁锭的观感
INGOT = [
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "....LLLLLLL.....",
    "...LSSSSSSSB....",
    "..LSSSSSSSSBB...",
    "..SSSSSSSSSSB...",
    "..BBBBBBBBBBB...",
    "................",
    "................",
    "................",
]

# 升级徽记：3×3 浅金「+」，叠在两套剪影都透明的左上角（见模块注释第 1 条）
BADGE = [
    "#.#",
    "###",
    "#.#",
]
BADGE_COLOR = "#FFE9A0"

# 木柄：与既有六系剑/镐一致（postprocess_art.py ITEMS 表首色）
HANDLE = ["#634C33", "#7A6042"]

# 每系：(刃/镐头主色 S, 受光 L, 暗边 B, 盐值基数)——金/合金/机元沿用 C1 批，
# 铁系本批新增（灰阶对齐既有铁剑/铁镐图标）
MATERIALS = [
    ("iron",           ["#D8D8D8", "#D8D8D8", "#D8D8D8", "#B8B8BC"],
                       ["#F5F5F5", "#F5F5F5", "#FFFFFF"],
                       ["#A0A0A8", "#A0A0A8", "#88888E"], 41),
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


def stamp_badge(img: Image.Image) -> Image.Image:
    """在左上角 (0,0)-(2,2) 叠 3×3 浅金「+」徽记（该区域两套剪影均透明）。"""
    px = img.load()
    color = hex_rgb(BADGE_COLOR)
    for y, row in enumerate(BADGE):
        for x, ch in enumerate(row):
            if ch == "#":
                px[x, y] = color + (255,)
    return img


def main() -> None:
    ITEM_DIR.mkdir(parents=True, exist_ok=True)

    # 八件升级装备：C1 剪影 + 各系色板 + 换盐值（与基底同盐会同纹路，+3 错开）+ 徽记
    for name, main_shades, light_shades, dark_shades, base_salt in MATERIALS:
        for kind, shape, salt in (("sword", SWORD, base_salt + 5), ("pickaxe", PICKAXE, base_salt + 8)):
            palette = {"S": main_shades, "L": light_shades, "B": dark_shades,
                       "G": dark_shades, "H": HANDLE}
            img = stamp_badge(render(shape, palette, salt))
            img.save(ITEM_DIR / f"{name}_{kind}_plus.png")
            print(f"{name}_{kind}_plus.png  程序生成 {img.size[0]}x{img.size[1]} RGBA（含+徽记）")

    # 金锭：金色系锭块（盐值与金剑/金镐都错开，避免纹理雷同）
    gold_main = ["#E8C04A", "#E8C04A", "#E8C04A", "#C09A34"]
    gold_light = ["#F7E08A", "#F7E08A", "#FFEFA8"]
    gold_dark = ["#C09A34", "#C09A34", "#A8842E"]
    ingot = render(INGOT, {"S": gold_main, "L": gold_light, "B": gold_dark}, 47)
    ingot.save(ITEM_DIR / "gold_ingot.png")
    print(f"gold_ingot.png  程序生成 {ingot.size[0]}x{ingot.size[1]} RGBA")


if __name__ == "__main__":
    main()
