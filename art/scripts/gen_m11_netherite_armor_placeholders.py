"""milestone-11 W3-3：补 4 张下界合金盔甲物品贴图（程序占位，消热键栏占位块）。

背景：W3-3 的 Boss 链落地时注册了 netherite 四件盔甲（items/netherite_{helmet,chest,
legs,boots}.json，numericId 1466-1469），texture 名在 items/textures/ 没有 PNG——
按 m6/m10 的程序占位快路径先补位（CLAUDE.md：新物品没有贴图会显示品红占位块，
这条路径保证贴图引用差集恒为空），正式美术到位后按
art/requests/items/netherite-armor-placeholders.md 替换。

本脚本产出 16×16、32 位 RGBA、硬边无抗锯齿的占位图，与既有物品图标同一规格：
ASCII 形状图 + 语义调色板，逐像素整数哈希在色阶间取色，确定性生成
（同输入必得同输出，不持有随机数对象——与 ValueNoise2D 同约定）。

四件共用四系盔甲的既成剪影（头盔圆顶+眼缝 / 胸甲+金核 / 护腿分叉 / 双靴），
netherite 色系取深紫灰（呼应 Boss 紫金机甲家族），金饰 G 与机元守卫图标需求
（art/requests/entities/machine-guardian.md）的金饰主 #DCAE3A 同源——
「Boss 掉的锭打的甲」一眼看出师承。正式美术到位后本脚本只留档生成过程，
不在任何构建/测试链路里。

用法（仓库根目录）：python art/scripts/gen_m11_netherite_armor_placeholders.py
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


# 头盔：圆顶 + 横贯金眼缝（呼应 Boss 图标的蓝眼缝剪影，材质换 netherite）
HELMET = [
    "................",
    "................",
    ".....LLLLLL.....",
    "....LSSSSSSL....",
    "...LSSSSSSSSL...",
    "...LSSSSSSSSL...",
    "..LSSSSSSSSSL...",
    "..LSSSSSSSSSL...",
    "..LSSSSSSSSSL...",
    "..LSBBBBBBBSL...",
    "..LSGGGGGGGSL...",
    "..LSBBBBBBBSL...",
    "..LSSSSSSSSSL...",
    "...LSSSSSSSL....",
    "...BBBBBBBBB....",
    "................",
]

# 胸甲：肩带 + 中央金核（四系胸甲剪影同款）
CHEST = [
    "................",
    "................",
    "..LL......LL....",
    ".LSSL....LSSL...",
    ".LSSLLLLLLSSL...",
    ".LSSSSSSSSSSL...",
    ".LSSSGGGGSSSL...",
    ".LSSGGGGGGSSL...",
    ".LSSSGGGGSSSL...",
    "..LSSSSSSSSL....",
    "..LSSSSSSSSL....",
    "..LSSSSSSSSL....",
    "..LSSSSSSSSL....",
    "..LBSSSSSSBL....",
    "...BSSSSSSB.....",
    "....BBBBBB......",
]

# 护腿：腰带 + 双腿分叉
LEGS = [
    "................",
    "................",
    "...LLLLLLLL.....",
    "..LSSSSSSSSL....",
    "..LSSSSSSSSL....",
    "..LSSSSSSSSL....",
    "..LSBBBBBBSL....",
    "..LSSSSSSSSL....",
    "..LSSS..SSSL....",
    "..LSSS..SSSL....",
    "..LSSS..SSSL....",
    "..LSSS..SSSL....",
    "..LSSS..SSSL....",
    "..LBBS..SSBL....",
    "...BBS..SSB.....",
    "...BB....BB.....",
]

# 双靴：左右各一只，靴口 + 靴头前伸
BOOTS = [
    "................",
    "................",
    "................",
    "..LLL.....LLL...",
    ".LSLL....LSLL...",
    ".LSLL....LSLL...",
    ".LSLL....LSLL...",
    ".LSLL....LSLL...",
    ".LSLLB...LSLLB..",
    ".LSSSB...LSSSB..",
    ".LSSSSB..LSSSSB.",
    ".BBBBB...BBBBB..",
    "................",
    "................",
    "................",
    "................",
]

# netherite 色阶：深紫灰主 / 受光 / 暗边（比机元 #7A5A9A 暗一档——Boss 家族的深色支），
# 金饰与 machine-guardian.md 的金饰主/金饰暗同源（掉落锭 → 打甲 的师承链）
PALETTE = {
    "S": ["#3F3742", "#3F3742", "#3F3742", "#332C38"],
    "L": ["#5C4F5C", "#5C4F5C", "#6A5C6A"],
    "B": ["#241F27", "#241F27", "#1B171E"],
    "G": ["#DCAE3A", "#DCAE3A", "#A87322"],
}


def main() -> None:
    ITEM_DIR.mkdir(parents=True, exist_ok=True)
    pieces = [
        ("helmet-netherite", HELMET, 43),
        ("chest-netherite", CHEST, 44),
        ("legs-netherite", LEGS, 45),
        ("boots-netherite", BOOTS, 46),
    ]
    for name, shape, salt in pieces:
        path = ITEM_DIR / f"{name}.png"
        render(shape, PALETTE, salt).save(path)
        print(f"写入 {path}")


if __name__ == "__main__":
    main()
