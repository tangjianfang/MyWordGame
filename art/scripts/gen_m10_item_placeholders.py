"""milestone-10 A1 fix1：补 4 张材料物品贴图（程序占位，消热键栏品红块）。

背景：m10 A1 新增的 raw_gold / raw_iron / summer_alloy / machine_essence 四个
物品 JSON 的 texture 名在 items/textures/ 没有 PNG，挖到矿石掉进背包会显示
品红占位块（m6 A3 同款教训——那次是 14 张）。

本脚本产出 16×16、32 位 RGBA、硬边无抗锯齿的占位图，与既有 60 张物品图标
（gen_m6_placeholders.py / tools/postprocess_art.py 的 ITEMS 表）同一规格：
ASCII 形状图 + 语义调色板，逐像素整数哈希在色阶间取色，确定性生成
（同输入必得同输出，不持有随机数对象——与 ValueNoise2D 同约定）。

四张底色按评审指定：raw_gold 亮金 #E8C04A / raw_iron 褐灰 #8A7A6A /
summer_alloy 青蓝 #4A9AB8 / machine_essence 紫灰 #7A5A9A，
各自派生暗/亮两阶做明暗。正式美术到位后按
art/requests/items/m10-material-placeholders.md 替换即可，本脚本只是留档
生成过程，不在任何构建/测试链路里。

用法（仓库根目录）：python art/scripts/gen_m10_item_placeholders.py
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


# 粗金：主矿块（左上高光 L、右下暗边 D）+ 两粒碎金
RAW_GOLD = [
    "................",
    "................",
    "....GGGG........",
    "...GGLLGG.......",
    "..GGLLLGGG......",
    "..GGLLGGGG......",
    "..GGGGGGGGG.....",
    "..GGGGGGGGGG.G..",
    ".DGGGGGGGGGGGG..",
    ".DDGGGGGGGGG....",
    "..DDGGGGGGG.....",
    "...DDDGGG.......",
    "..G..DD.........",
    ".GG.............",
    "................",
    "................",
]

# 粗铁：同族矿块造型但错开一格、碎粒更少（色差为主，剪影微差辅助辨认）
RAW_IRON = [
    "................",
    "................",
    "................",
    "....IIII........",
    "...IILLII.......",
    "..IILLLIII......",
    "..IILIIIII..I...",
    "..IIIIIIIIII....",
    ".IIIIIIIIIII.I..",
    ".DDIIIIIIIII....",
    "..DDIIIIIII.....",
    "...DDIIII.......",
    "....DDII........",
    "...I............",
    "................",
    "................",
]

# 夏季合金：梯形合金锭，顶面亮、正身主色、底缘暗
SUMMER_ALLOY = [
    "................",
    "................",
    "................",
    "................",
    "...LLLLLLLLL....",
    "..LLLLLLLLLLL...",
    "..SSSSSSSSSSS...",
    "..SSSSSSSSSSS...",
    "..SSSSSSSSSSS...",
    "..DSSSSSSSSSD...",
    "..DDSSSSSSSDD...",
    "...DDDDDDDDD....",
    "................",
    "................",
    "................",
    "................",
]

# 机元：菱形发光核心 + 上下电路引脚，核内高光
MACHINE_ESSENCE = [
    "................",
    ".......E........",
    ".......E........",
    "......EEE.......",
    ".....EELLE......",
    "....EELLLEE.....",
    "...EEELLLEEE....",
    "..EEEELLLEEEE...",
    "..EEEELLLEEEE...",
    "...EEELLLEEE....",
    "....EELLLEE.....",
    ".....EELLE......",
    "......EEE.......",
    ".......E........",
    ".......E........",
    "................",
]

# 每项：(输出名, 形状图, {角色: [带权重色阶]}, 盐值)
PROCEDURAL = [
    ("raw_gold", RAW_GOLD, {
        "G": ["#E8C04A", "#E8C04A", "#E8C04A", "#C09A34"],   # 亮金主色
        "L": ["#F7E08A", "#F7E08A", "#FFEFA8"],              # 左上高光
        "D": ["#C09A34", "#C09A34", "#A8842E"],              # 右下暗边
    }, 21),
    ("raw_iron", RAW_IRON, {
        "I": ["#8A7A6A", "#8A7A6A", "#8A7A6A", "#6E6154"],   # 褐灰主色
        "L": ["#A69684", "#A69684", "#B8AA9A"],              # 左上高光
        "D": ["#6E6154", "#6E6154", "#5A4F45"],              # 右下暗边
    }, 22),
    ("summer_alloy", SUMMER_ALLOY, {
        "S": ["#4A9AB8", "#4A9AB8", "#4A9AB8", "#35758E"],   # 青蓝锭身
        "L": ["#78C2DC", "#78C2DC", "#9AD8EA"],              # 顶面受光
        "D": ["#35758E", "#35758E", "#2A5F75"],              # 底缘暗面
    }, 23),
    ("machine_essence", MACHINE_ESSENCE, {
        "E": ["#7A5A9A", "#7A5A9A", "#7A5A9A", "#5E4478"],   # 紫灰核心
        "L": ["#9C82BC", "#9C82BC", "#B9A2D4"],              # 核心辉光
    }, 24),
]


def main() -> None:
    ITEM_DIR.mkdir(parents=True, exist_ok=True)
    for name, shape, palette, salt in PROCEDURAL:
        img = render(shape, palette, salt)
        img.save(ITEM_DIR / f"{name}.png")
        print(f"{name}.png  程序生成 {img.size[0]}x{img.size[1]} RGBA")


if __name__ == "__main__":
    main()
