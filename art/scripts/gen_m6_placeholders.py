"""milestone-6 A3：补 14 张缺失物品贴图（程序占位，消 HotbarUI 品红块）。

背景：items/*.json 引用的 14 个 texture 名在 items/textures/ 没有 PNG，
实机热键栏/物品栏显示品红占位块。其中 dirt/log/beef/chicken 是引导任务链
前期必得品，必须先有图。

本脚本产出 16×16、32 位 RGBA、硬边无抗锯齿的占位图，与既有 46 张物品图标
（tools/postprocess_art.py 的 ITEMS 表）同一规格：

- dirt / log：直接复用 blocks/textures/ 的 32×32 方块贴图最近邻降采样，
  视觉与方块完全一致（log 方块没有 log.png，取 log-side.png——物品视角
  看「原木」认的是树皮侧面）
- 其余 12 张：ASCII 形状图 + 语义调色板，逐像素整数哈希在色阶间取色，
  确定性生成（同输入必得同输出，不持有随机数对象——与 ValueNoise2D 同约定）

正式美术到位后按 art/requests/items/m6-placeholder-batch.md 替换即可，
本脚本只是留档生成过程，不在任何构建/测试链路里。

用法（仓库根目录）：python art/scripts/gen_m6_placeholders.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ITEM_DIR = ROOT / "Assets" / "StreamingAssets" / "items" / "textures"
BLOCK_DIR = ROOT / "Assets" / "StreamingAssets" / "blocks" / "textures"

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


def downsample_block(src_name: str, dst_name: str) -> None:
    """方块贴图 32×32 → 16×16 最近邻降采样（视觉与方块一致）。"""
    src = (BLOCK_DIR / src_name).with_suffix(".png")
    img = Image.open(src).convert("RGB").resize((SIZE, SIZE), Image.NEAREST)
    out = Image.new("RGBA", (SIZE, SIZE))
    out.paste(img, (0, 0))
    out.save(ITEM_DIR / f"{dst_name}.png")


# ---------------------------------------------------------------------------
# 12 张程序生成图：形状图 + 语义调色板
# 色板尽量复用 art/README.md 全局调色板（泥土棕/树皮棕/水蓝/UI 暖灰等）
# ---------------------------------------------------------------------------

# 箭：灰矛头 + 木杆 + 白羽（斜置，右上为头）
ARROW = [
    "................",
    "...........HHHH.",
    "..........HHHHH.",
    ".........HH.SHH.",
    "........SS..SH..",
    ".......S........",
    "......S.........",
    ".....S..........",
    "....S...........",
    "...S............",
    "..S.............",
    ".S..............",
    "FFS.............",
    "FFF.............",
    ".F..............",
    "................",
]

# 生牛肉：红棕肉块 + 粉白脂肪纹
BEEF = [
    "................",
    "................",
    "................",
    "................",
    "....BBBBBBBB....",
    "...BBBBBBBBBB...",
    "..BBMMMMMMMMBB..",
    "..BMMBBBMMMMBB..",
    "..BMBBBBMMMmBB..",
    "..BBMMMMBBBmBB..",
    "..BBBMMMMMMBBB..",
    "...BBBBBBBBBB...",
    "....BBBBBBBB....",
    "................",
    "................",
    "................",
]

# 生鸡肉：整鸡侧影，浅粉胸 + 白色骨尖
CHICKEN = [
    "................",
    "................",
    "................",
    ".....CCCC.......",
    "....CCCCCC..b...",
    "...CCCCCCC.bb...",
    "...CCLLCCC.bb...",
    "..CCLLLLCCbb....",
    "..CCLLLLCCb.....",
    "..CCCLLCCC......",
    "...CCCCCC.......",
    "....CCCC........",
    "................",
    "................",
    "................",
    "................",
]

# 椅子：侧视，高背 + 座面 + 四腿（侧视见两腿）
CHAIR = [
    "................",
    ".DD.............",
    ".DD.............",
    ".DD.............",
    ".DD.............",
    ".DD.............",
    ".DDDD...........",
    ".DDDDDDDDDDD....",
    ".DDDDDDDDDDDD...",
    ".DD.......DD....",
    ".DD.......DD....",
    ".DD.......DD....",
    ".DD.......DD....",
    ".DD.......DD....",
    "................",
    "................",
]

# 地球仪：蓝海绿陆圆球 + 棕色支架底座
GLOBE = [
    "................",
    "......OOOO......",
    "....OOOOOOOO....",
    "...OOOGGOOGGO...",
    "..OOOGGGGGGOOO..",
    "..OOGOOGGOOGGO..",
    "..OOOGGGGGGGOO..",
    "..OOOGGOGGGOOO..",
    "...OOGGGGOOOO...",
    "....OOOOOOOO....",
    "......OPO.......",
    ".....OPPPO......",
    "....PPPPPPPP....",
    "................",
    "................",
    "................",
]

# 黑客电脑：立式机箱 + 青绿发光面板/LED
HACKER_PC = [
    "................",
    "..KKKKKKKKKK....",
    "..KPPPPPPPPK....",
    "..KPGGPPGGPK....",
    "..KPPPPPPPPK....",
    "..KKKKKKKKKK....",
    "..KPPPPPPPPK....",
    "..KPGPPPPGPK....",
    "..KPPPPPPPPK....",
    "..K.KKPPKK..K...",
    "..K.KPPPPK..K...",
    "..KKKPPPPKKKK...",
    "..KPPKKKKPPK....",
    "..KKKKKKKKKK....",
    "................",
    "................",
]

# 键盘：斜视键阵，深色底托 + 灰白键帽
KEYBOARD = [
    "................",
    "................",
    "................",
    "................",
    "..BBBBBBBBBBBB..",
    "..BKKKKKKKKKKB..",
    "..BKKKKKKKKKKB..",
    "..BKKKKKKKKKKB..",
    "..BKKKKKKKKKKB..",
    "..BBKKKKKKKKBB..",
    "...BBBBBBBBBB...",
    "................",
    "................",
    "................",
    "................",
    "................",
]

# 笔记本电脑：开合正视，深屏青码 + 灰色键盘底座
LAPTOP = [
    "................",
    "...LLLLLLLLLL...",
    "...LSSSSSSSSL...",
    "...LSCCCSSCCL...",
    "...LSSSSSSSSL...",
    "...LSCSSSCCCL...",
    "...LSSSSSSSSL...",
    "...LSSCCSSSSL...",
    "...LLLLLLLLLL...",
    "...BBBBBBBBBB...",
    "..BBBBBBBBBBBB..",
    "..BDDDDDDDDDDB..",
    "...BBBBBBBBBB...",
    "................",
    "................",
    "................",
]

# 鼠标：竖椭圆 + 顶缝与滚轮
MOUSE = [
    "................",
    "................",
    "......MMMM......",
    ".....MMMMMM.....",
    ".....MMWWMM.....",
    ".....MMWWMM.....",
    ".....MMMMMM.....",
    ".....MLMMMM.....",
    ".....MMMMMM.....",
    ".....MMMMMM.....",
    ".....MMMMMM.....",
    "......MMMM......",
    "................",
    "................",
    "................",
    "................",
]

# 笔记本（纸质）：紫色封面 + 左侧装订环（隔行成环）
NOTEBOOK = [
    "................",
    "...RRRRRRRRRR...",
    ".R.RRRRRRRRRRR..",
    "....CCCCCCCCCC..",
    ".R.CCCCCCCCCC...",
    "....CCCCCCCCCC..",
    ".R.CCCCCCCCCC...",
    "....CCCCCCCCCC..",
    ".R.CCCCCCCCCC...",
    "....CCCCCCCCCC..",
    ".R.CCCCCCCCCC...",
    "....CCCCCCCCCC..",
    ".R.CCCCCCCCCC...",
    "....CCCCCCCCCC..",
    "...RRRRRRRRRR...",
    "................",
]

# 办公桌：桌面板 + 左侧抽屉柜 + 右侧桌腿
OFFICE_DESK = [
    "................",
    "................",
    "................",
    ".TTTTTTTTTTTTTT.",
    ".TTTTTTTTTTTTTT.",
    ".PPPPPPPPP......",
    ".PDDDDDDDP......",
    ".PDDDDDDDP......",
    ".PDDDDDDDP......",
    ".PPPPPPPPP......",
    ".PP.....TT....TT",
    ".PP.....TT....TT",
    ".PP.....TT....TT",
    ".PP.....TT....TT",
    "................",
    "................",
]

# 桌子：正视，厚桌面 + 两条桌腿
TABLE = [
    "................",
    "................",
    "................",
    "................",
    ".TTTTTTTTTTTTTT.",
    ".TTDDDDDDDDDDTT.",
    "...TT.....TT....",
    "...TT.....TT....",
    "...TT.....TT....",
    "...TT.....TT....",
    "...TT.....TT....",
    "...TT.....TT....",
    "...TT.....TT....",
    "...TT.....TT....",
    "................",
    "................",
]

# 每项：(输出名, 形状图, {角色: [带权重色阶]}, 盐值)
PROCEDURAL = [
    ("arrow", ARROW, {
        "H": ["#6E6E6E", "#4A4A4A", "#4A4A4A"],      # 燧石矛头：石灰系
        "S": ["#7A6042", "#7A6042", "#8E7350"],       # 木杆：树皮棕
        "F": ["#F2F2F2", "#F2F2F2", "#D8D8D8"],       # 尾羽：白
    }, 1),
    ("beef", BEEF, {
        "B": ["#A03038", "#A03038", "#A03038", "#7A2028", "#C04850"],  # 牛肉红棕
        "M": ["#E8A8A0", "#E8A8A0", "#F2C8C0", "#C88878"],            # 脂肪纹粉白
        "m": ["#C88878", "#E8A8A0"],                                   # 细纹
    }, 2),
    ("chicken", CHICKEN, {
        "C": ["#E8B8B0", "#E8B8B0", "#E8B8B0", "#D8A098"],  # 鸡皮粉
        "L": ["#F8D0C8", "#F8D0C8", "#FFF0EA"],             # 胸部浅色
        "b": ["#EAEAD8", "#EAEAD8", "#F8F8E8"],             # 骨尖白
    }, 3),
    ("chair", CHAIR, {
        "D": ["#7A6042", "#7A6042", "#7A6042", "#634C33", "#9C7549"],  # 木椅：树皮棕
    }, 4),
    ("globe", GLOBE, {
        "O": ["#3A6FB5", "#3A6FB5", "#3A6FB5", "#2C5893", "#4E88CE"],  # 海洋：水蓝
        "G": ["#5D9C3C", "#5D9C3C", "#5D9C3C", "#4A7E2F", "#74B84E"],  # 陆地：草绿
        "P": ["#7A6042", "#7A6042", "#634C33"],                       # 支架：树皮棕
    }, 5),
    ("hacker_pc", HACKER_PC, {
        "K": ["#2A2A30", "#2A2A30", "#1A1A1E"],                  # 机箱深灰黑
        "P": ["#3E3E46", "#3E3E46", "#33333A"],                  # 面板灰
        "G": ["#4CC6C4", "#4CC6C4", "#A8F2EF", "#1E7C7C"],       # 发光：钻石青
    }, 6),
    ("keyboard", KEYBOARD, {
        "B": ["#4A4A52", "#4A4A52", "#3A3A42"],                  # 底托深灰
        "K": ["#8A8A8A", "#8A8A8A", "#8A8A8A", "#C8C8C8", "#6E6E6E"],  # 键帽石灰
    }, 7),
    ("laptop", LAPTOP, {
        "L": ["#5A5A64", "#5A5A64", "#4A4A52"],                  # 屏框灰
        "S": ["#14141A", "#14141A", "#1E1E26"],                  # 屏幕底黑
        "C": ["#4CC6C4", "#4CC6C4", "#A8F2EF", "#1E7C7C"],       # 屏上代码：钻石青
        "B": ["#8A8A8A", "#8A8A8A", "#8A8A8A", "#A3A3A3"],       # 底座石灰
        "D": ["#5C5C5C", "#5C5C5C", "#6E6E6E"],                  # 键盘区深灰
    }, 8),
    ("mouse", MOUSE, {
        "M": ["#D8D8D8", "#D8D8D8", "#D8D8D8", "#F2F2F2", "#C0C0C0"],  # 壳体白灰
        "W": ["#4A4A4A", "#4A4A4A", "#6E6E6E"],                  # 滚轮深灰
        "L": ["#A0A0A0", "#8A8A8A"],                             # 侧缝
    }, 9),
    ("notebook", NOTEBOOK, {
        "R": ["#5A4A8A", "#5A4A8A", "#5A4A8A", "#3A2A5A"],       # 封面紫
        "C": ["#F2EAD2", "#F2EAD2", "#F2EAD2", "#E0D8BC"],       # 纸页米白
    }, 10),
    ("office_desk", OFFICE_DESK, {
        "T": ["#9C7549", "#9C7549", "#9C7549", "#8A6741", "#B98D57"],  # 桌面木板黄
        "P": ["#7A6042", "#7A6042", "#634C33"],                  # 柜体树皮棕
        "D": ["#8A8A8A", "#8A8A8A", "#8A8A8A", "#6E6E6E", "#A3A3A3"],  # 抽屉面板石灰
    }, 11),
    ("table", TABLE, {
        "T": ["#9C7549", "#9C7549", "#9C7549", "#8A6741", "#B98D57"],  # 桌面木板黄
        "D": ["#634C33", "#7A6042"],                             # 桌腿暗面
    }, 12),
]


def main() -> None:
    ITEM_DIR.mkdir(parents=True, exist_ok=True)

    # dirt/log：方块贴图降采样（视觉与方块一致）
    downsample_block("dirt", "dirt")
    downsample_block("log-side", "log")
    print("dirt.png / log.png  <- blocks/textures 32->16 最近邻降采样")

    for name, shape, palette, salt in PROCEDURAL:
        img = render(shape, palette, salt)
        img.save(ITEM_DIR / f"{name}.png")
        print(f"{name}.png  程序生成 {img.size[0]}x{img.size[1]} RGBA")


if __name__ == "__main__":
    main()
