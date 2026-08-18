"""milestone-13 W3：生成火枪 + 子弹的程序占位贴图。

背景：m13 W3 在 items/ 增加 musket（1607）和 bullet（1608）两件新物品。正式美术未出，
按 m10/m11 批（gen_m10_item_placeholders.py / gen_m10_gear_placeholders.py 等）同模式
程序生成占位，保证：

- 物品注册表 / RecipeDatabase / BlockInteraction.TryFireMusket / HotbarUI 等运行时
  加载路径（Assets/StreamingAssets/items/textures/）恒有图可读，正式美术到位后同名
  替换即可
- 尺寸 32×32、RGBA、**Alpha 只有 0/255 两态**（洋红键控等价：背景直接透明）
- 调色板逐色取自 art/requests/items/musket.md / bullet.md，不引入需求外颜色
- 确定性：同输入必得同输出，逐像素整数哈希，不持随机数对象

用法（仓库根目录）：python art/scripts/gen_m13_w3_musket_placeholders.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEXTURE_DIR = ROOT / "Assets" / "StreamingAssets" / "items" / "textures"
SIZE = 32

# ─── musket 调色板（art/requests/items/musket.md） ──────────────────────
# 火枪枪身（深木）+ 铁枪管（深灰）+ 黄铜击锤 + 浅木枪托
MUSKET_DARK_WOOD = 0x3A2418
MUSKET_WOOD = 0x6B4423
MUSKET_LIGHT_WOOD = 0x8B5A2B
MUSKET_IRON_DARK = 0x2A2A2A
MUSKET_IRON = 0x4A4A4A
MUSKET_IRON_LIGHT = 0x6E6E6E
MUSKET_BRASS = 0xC49B3A
MUSKET_BRASS_HIGHLIGHT = 0xE8C46A
MUSKET_OUTLINE = 0x1A1A1A

# ─── bullet 调色板（art/requests/items/bullet.md） ──────────────────────
# 黄铜圆头弹 + 铁黑色弹壳
BULLET_BRASS = 0xC49B3A
BULLET_BRASS_HIGHLIGHT = 0xE8C46A
BULLET_BRASS_DARK = 0x8A6B26
BULLET_IRON = 0x4A4A4A
BULLET_IRON_DARK = 0x2A2A2A
BULLET_OUTLINE = 0x1A1A1A


def rgb(v: int) -> tuple[int, int, int]:
    return ((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF)


def hash32(x: int, y: int, salt: int) -> int:
    """确定性整数哈希（与 gen_m11_enchant_placeholders.py / gen_m11_fx_placeholders.py 同实现）。"""
    n = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFFFFFF


def _write_with_meta(img: Image.Image, name: str) -> None:
    """落盘 PNG + 写一份最小 meta（m10 批同款：fileFormatVersion + guid + DefaultImporter）。
    guid 用确定性派生：固定根（每张贴图一个）→ 同一脚本跑两遍产物 guid 相同（方便回归）。"""
    png_path = TEXTURE_DIR / f"{name}.png"
    meta_path = TEXTURE_DIR / f"{name}.png.meta"
    img.save(png_path, "PNG", optimize=True)

    # guid 派生：取固定根（脚本级常量）的低 32 位 hex
    base = {
        "musket": 0x4D55534B,   # "MUSK"
        "bullet": 0x42554C4C,   # "BULL"
    }[name]
    salt = 0x12345678
    guid_int = (base ^ salt) & 0xFFFFFFFF
    # 标准 Unity GUID 形式 32 hex
    guid_hex = f"{guid_int:08x}" + f"{hash32(guid_int, 0, 0) & 0xFFFFFFFF:08x}" + f"{hash32(0, guid_int, 0) & 0xFFFFFFFF:08x}" + f"{hash32(0, 0, guid_int) & 0xFFFFFFFF:08x}"
    meta = (
        "fileFormatVersion: 2\n"
        f"guid: {guid_hex}\n"
        "TextureImporter:\n"
        "  internalIDToNameTable: []\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 13\n"
        "  mipmaps:\n"
        "    mipMapMode: 0\n"
        "    enableMipMap: 0\n"
        "  textureFormat: -1\n"
        "  maxTextureSize: 32\n"
        "  textureSettings:\n"
        "    serializedVersion: 2\n"
        "    filterMode: 0\n"
        "    aniso: 0\n"
        "    mipBias: 0\n"
        "  spriteSheet:\n"
        "    sprites: []\n"
        "  spritePackingTag:\n"
        "  userData:\n"
        "  assetBundleName:\n"
        "  assetBundleVariant:\n"
    )
    meta_path.write_text(meta, encoding="utf-8")
    print(f"  {png_path.relative_to(ROOT)} + .meta")


def draw_musket() -> Image.Image:
    """火枪图标：横置，左木枪托 → 中段击锤 + 扳机护圈 → 右铁枪管（管口略出边）。
    主体水平占 80%，居中；垂直方向枪管 + 枪托两段挤在中线（compact）。"""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    cy = SIZE // 2  # 16

    for y in range(SIZE):
        for x in range(SIZE):
            dx = x - 2
            dy = y - cy

            # 1) 枪管（铁黑）：右半段 [20..30] 整片铁色，y∈[14..17]（4 px 高）
            if 20 <= x <= 30 and 14 <= y <= 17:
                # 管口（最右两列）亮一档
                if x >= 29:
                    color = rgb(MUSKET_IRON_LIGHT)
                elif 20 <= x <= 22:
                    color = rgb(MUSKET_IRON_DARK)
                else:
                    color = rgb(MUSKET_IRON)
                px[x, y] = (*color, 255)
                continue

            # 2) 击锤（黄铜）：[18..20] 中段小方块
            if 18 <= x <= 19 and 10 <= y <= 13:
                color = rgb(MUSKET_BRASS_HIGHLIGHT if y == 10 else MUSKET_BRASS)
                px[x, y] = (*color, 255)
                continue

            # 3) 扳机护圈 + 扳机（铁）：y∈[17..21] x∈[15..18] 弯钩
            # 简化：18..19 列底色铁，y=21 一行弯钩下沿
            if 15 <= x <= 19 and 17 <= y <= 18:
                px[x, y] = (*rgb(MUSKET_IRON), 255)
                continue
            if 15 <= x <= 18 and 19 <= y <= 21:
                # 护圈：环轮廓（dx=15,18；dy=21） = 描边
                if x in (15, 18) or y == 21:
                    px[x, y] = (*rgb(MUSKET_OUTLINE), 255)
                elif 16 <= x <= 17 and 20 <= y <= 20:
                    # 内部空（透明）
                    pass
                continue

            # 4) 枪托（木）：[3..15] x ∈ 横向长条，y∈[12..20]（9 px 高，木色三档）
            if 3 <= x <= 15 and 12 <= y <= 20:
                if y == 12:
                    color = rgb(MUSKET_LIGHT_WOOD)
                elif y in (19, 20):
                    color = rgb(MUSKET_DARK_WOOD)
                else:
                    color = rgb(MUSKET_WOOD)
                # 枪托曲线（握把处 y∈[16..20] 略向右鼓）：x 起点动态
                if y >= 16 and x >= 3 and x <= 7:
                    # 握把弧：x=3..7, y=16..20，弧外描边
                    color = rgb(MUSKET_DARK_WOOD if y == 20 else MUSKET_WOOD)
                px[x, y] = (*color, 255)
                continue

            # 5) 描边：所有非透明格的「最外层」框出黑边——简化版只在木段 / 枪管顶底加
            if 3 <= x <= 30 and (y == 12 or y == 20) and 14 <= y <= 17:
                # 枪管描边
                if 20 <= x <= 30:
                    px[x, y] = (*rgb(MUSKET_OUTLINE), 255)
                # 枪托描边
                if 3 <= x <= 15:
                    px[x, y] = (*rgb(MUSKET_OUTLINE), 255)
                continue
            if (3 <= x <= 15 or 20 <= x <= 30) and 12 <= y <= 20:
                # 左/右竖边
                if x == 3 or x == 30 or (x == 15 and not (16 <= y <= 21)):
                    px[x, y] = (*rgb(MUSKET_OUTLINE), 255)
                continue

            # 其它格留透明
            px[x, y] = (0, 0, 0, 0)

    return img


def draw_bullet() -> Image.Image:
    """子弹图标：竖立单颗子弹，弹头（圆头，黄铜）在上、弹壳（铁黑）在下。
    主体宽 8 px，居中；高约 22 px（占画幅 70%）。"""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()

    cx = SIZE // 2  # 16
    body_left = cx - 4   # 12
    body_right = cx + 4  # 20（不含）
    tip_top = 5
    shell_top = 13
    shell_bottom = 26

    for y in range(SIZE):
        for x in range(SIZE):
            # 1) 弹头：y∈[5..13] 圆弧收缩到 x∈[cx-1, cx+1]
            if tip_top <= y <= 12:
                # 半圆剖面（圆心 (cx, 12)，半径 5）：判定 (x-cx)² + (y-12)² ≤ 25
                dx = x - cx
                dy = y - 12
                r2 = dx * dx + dy * dy
                if r2 <= 25:
                    # 弹头亮 / 主色
                    if y <= 6:
                        color = rgb(BULLET_BRASS_HIGHLIGHT)
                    elif y >= 11:
                        color = rgb(BULLET_BRASS_DARK)
                    else:
                        color = rgb(BULLET_BRASS)
                    px[x, y] = (*color, 255)
                    continue

            # 2) 弹壳：y∈[13..26] 矩形 x∈[12..20]
            if shell_top <= y <= shell_bottom and body_left <= x < body_right:
                # 弹壳主体铁色，y=15 一行亮一档（金属反光），y=24 一行暗（壳底）
                if y == 15:
                    color = rgb(BULLET_BRASS_HIGHLIGHT)
                elif y == shell_bottom or y == shell_bottom - 1:
                    color = rgb(BULLET_IRON_DARK)
                else:
                    color = rgb(BULLET_IRON)
                # 弹壳底边 y=26 收窄成圆（壳底弧）
                if y >= shell_bottom - 1:
                    dx = abs(x - cx)
                    if dx > 3:
                        continue  # 收窄外不画
                px[x, y] = (*color, 255)
                continue

            # 3) 描边：弹头 + 弹壳左右各 1 px 黑
            # 弹头弧外围
            if tip_top - 1 <= y <= 13 and body_left - 1 <= x < body_right + 1:
                dx = x - cx
                dy = y - 12
                if tip_top - 1 <= y <= 12 and dx * dx + dy * dy > 25 and dx * dx + dy * dy <= 36:
                    px[x, y] = (*rgb(BULLET_OUTLINE), 255)
                    continue
            # 弹壳左右竖边
            if shell_top <= y <= shell_bottom:
                if x == body_left - 1 or x == body_right:
                    px[x, y] = (*rgb(BULLET_OUTLINE), 255)
                    continue
            # 弹壳底弧
            if y == shell_bottom + 1 and body_left <= x < body_right:
                dx = abs(x - cx)
                if dx <= 3:
                    px[x, y] = (*rgb(BULLET_OUTLINE), 255)
                    continue

            # 其它留透明
            px[x, y] = (0, 0, 0, 0)

    return img


def main() -> None:
    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)

    print("生成 musket.png …")
    _write_with_meta(draw_musket(), "musket")

    print("生成 bullet.png …")
    _write_with_meta(draw_bullet(), "bullet")

    print("完成。")


if __name__ == "__main__":
    main()