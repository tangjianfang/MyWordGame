"""评审 08 F2：生成剪刀（shears）的程序占位贴图。

背景：评审修复批在 items/ 增加 shears（1621）。正式美术未出，按
gen_m13_w3_musket_placeholders.py 同模式程序生成占位：
- 运行时加载路径（Assets/StreamingAssets/items/textures/）恒有图可读
- 尺寸 32×32、RGBA、Alpha 只有 0/255 两态（背景直接透明）
- 调色板逐色取自 art/requests/items/shears.md，不引入需求外颜色
- 确定性：逐像素整数哈希，不持随机数对象

用法（仓库根目录）：python art/scripts/gen_shears_placeholder.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEXTURE_DIR = ROOT / "Assets" / "StreamingAssets" / "items" / "textures"
SIZE = 32

# ─── 剪刀调色板（art/requests/items/shears.md） ─────────────────────────
IRON = 0x8A8A8A           # 刀身铁灰
IRON_LIGHT = 0xB6B6B6     # 刀身高光
IRON_DARK = 0x4E4E4E      # 刀身暗面
BRASS = 0xC49B3A          # 中央铆钉黄铜
BRASS_HIGHLIGHT = 0xE8C46A
OUTLINE = 0x1A1A1A


def rgb(v: int) -> tuple[int, int, int]:
    return ((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF)


def hash32(x: int, y: int, salt: int) -> int:
    """确定性整数哈希（与 gen_m13_w3_musket_placeholders.py 同实现）。"""
    n = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFFFFFF


def draw_shears() -> Image.Image:
    """剪刀图标：X 交叉双刀（左上-右下 / 右上-左下），中心黄铜铆钉，
    底部两个指孔环收尾（简化为一个横杆）。刀刃从角到中心约 60% 画幅。"""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    cx, cy = SIZE // 2, SIZE // 2  # 16,16

    for y in range(SIZE):
        for x in range(SIZE):
            dx = x - cx
            dy = y - cy

            # 1) 两条交叉刀身：|dx| 与 |dy| 都在 4..12 之间且 |dx|≈|dy|（带宽 3）——X 形
            adx, ady = abs(dx), abs(dy)
            if 4 <= adx <= 13 and 4 <= ady <= 13 and abs(adx - ady) <= 1:
                # 刀刃亮侧：靠外端（adx 或 ady 大的那侧靠外）
                near_tip = max(adx, ady) >= 11
                near_hub = max(adx, ady) <= 6
                if near_tip:
                    color = IRON_LIGHT
                elif near_hub:
                    color = IRON_DARK
                else:
                    color = IRON
                px[x, y] = (*rgb(color), 255)
                continue

            # 2) 中央铆钉：黄铜 4×4 圆角块
            if adx <= 2 and ady <= 2:
                color = BRASS_HIGHLIGHT if (adx + ady) <= 1 else BRASS
                px[x, y] = (*rgb(color), 255)
                continue

            # 3) 指孔环（底部）：y∈[24..28]，两段横杆 x∈[8..13] 与 [18..23]
            if 24 <= y <= 27 and (8 <= x <= 13 or 18 <= x <= 23):
                px[x, y] = (*rgb(IRON_DARK), 255)
                continue

            # 4) 描边：刀身外沿一圈黑（|adx-ady|==2 的环带）
            if 4 <= adx <= 14 and 4 <= ady <= 14 and abs(adx - ady) == 2:
                px[x, y] = (*rgb(OUTLINE), 255)
                continue

            # 其它留透明
            px[x, y] = (0, 0, 0, 0)

    return img


def _write_with_meta(img: Image.Image, name: str) -> None:
    png_path = TEXTURE_DIR / f"{name}.png"
    meta_path = TEXTURE_DIR / f"{name}.png.meta"
    img.save(png_path, "PNG", optimize=True)

    base = 0x53484541  # "SHEA"
    salt = 0x12345678
    guid_int = (base ^ salt) & 0xFFFFFFFF
    guid_hex = (f"{guid_int:08x}" + f"{hash32(guid_int, 0, 0) & 0xFFFFFFFF:08x}"
                + f"{hash32(0, guid_int, 0) & 0xFFFFFFFF:08x}"
                + f"{hash32(0, 0, guid_int) & 0xFFFFFFFF:08x}")
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


def main() -> None:
    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    print("生成 shears.png …")
    _write_with_meta(draw_shears(), "shears")
    print("完成。")


if __name__ == "__main__":
    main()
