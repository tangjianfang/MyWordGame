"""评审 08 F3：生成钓鱼三件（fishing_rod / raw_fish / cooked_fish）的程序占位贴图。

照 gen_shears_placeholder.py 同模式：确定性像素画、32×32、Alpha 0/255 两态、
调色板取自 art/requests/items/fishing.md。用法：python art/scripts/gen_fishing_placeholders.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEXTURE_DIR = ROOT / "Assets" / "StreamingAssets" / "items" / "textures"
SIZE = 32

# ─── 调色板（art/requests/items/fishing.md） ────────────────────────────
ROD_WOOD = 0x6B4423
ROD_WOOD_DARK = 0x3A2418
LINE_GRAY = 0xB6B6B6
HOOK_IRON = 0x8A8A8A
FISH_BLUE = 0x4A7FB5
FISH_BLUE_DARK = 0x2E5A8A
FISH_BELLY = 0xA8C8E8
FISH_BROWN = 0x9A6B3F
FISH_BROWN_DARK = 0x6B4423
STEAM = 0xE8E8E8
OUTLINE = 0x1A1A1A


def rgb(v: int) -> tuple[int, int, int]:
    return ((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF)


def hash32(x: int, y: int, salt: int) -> int:
    n = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFFFFFF


def blank():
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def draw_rod() -> Image.Image:
    """钓鱼竿：左下到右上的棕竿斜线 + 右侧垂下的灰线 + 铁钩。"""
    img = blank()
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            # 竿：对角线 (3,28)→(24,4)，带宽 2
            t = (27 - y)
            if abs(x - (3 + t)) <= 1 and 4 <= y <= 28:
                px[x, y] = (*rgb(ROD_WOOD_DARK if (x - 3 - t) == 1 or y == 28 else ROD_WOOD), 255)
                continue
            # 线：x=25 竖线 y∈[5..20]
            if x == 25 and 5 <= y <= 20:
                px[x, y] = (*rgb(LINE_GRAY), 255)
                continue
            # 钩：y=21 横 2 像素 + 竖回勾
            if (x in (24, 25) and y == 21) or (x == 24 and 22 <= y <= 23) or (x == 25 and y == 24):
                px[x, y] = (*rgb(HOOK_IRON), 255)
                continue
    return img


def _fish(body: int, dark: int, belly: int) -> Image.Image:
    """鱼形主体：椭圆身 + 三角尾 + 眼点，横向居中（生鱼/熟鱼共用形状）。"""
    img = blank()
    px = img.load()
    cx, cy = 15, 16
    for y in range(SIZE):
        for x in range(SIZE):
            dx = (x - cx) / 10.0   # 身体椭圆半宽 10
            dy = (y - cy) / 5.0    # 半高 5
            r2 = dx * dx + dy * dy
            if r2 <= 1.0:
                # 腹部（下 1/3）浅色、背深色
                color = belly if dy > 0.35 else (dark if dy < -0.45 else body)
                px[x, y] = (*rgb(color), 255)
                continue
            # 尾鳍：x∈[24..29]，向右张开的三角
            if 24 <= x <= 29:
                spread = (x - 24)
                if abs(y - cy) <= spread + 1 and abs(y - cy) >= spread - 2:
                    px[x, y] = (*rgb(dark), 255)
                    continue
    # 眼点
    px[10, 14] = (*rgb(OUTLINE), 255)
    return img


def draw_raw_fish() -> Image.Image:
    return _fish(FISH_BLUE, FISH_BLUE_DARK, FISH_BELLY)


def draw_cooked_fish() -> Image.Image:
    img = _fish(FISH_BROWN, FISH_BROWN_DARK, FISH_BROWN)
    px = img.load()
    # 热气：三条 2 像素宽竖波浪 above the fish
    for sx in (9, 15, 21):
        for k in range(5):
            y = 6 + k * 3
            x = sx + (1 if (k % 2) == 0 else 0)
            if 0 <= x < SIZE:
                px[x, y] = (*rgb(STEAM), 255)
    return img


GUID_ROOT = {
    "fishing_rod": 0x46534852,  # "FSHR"
    "raw_fish": 0x52415746,     # "RAWF"
    "cooked_fish": 0x4F4F4B46,  # "OOKF"
}


def _write_with_meta(img: Image.Image, name: str) -> None:
    png_path = TEXTURE_DIR / f"{name}.png"
    meta_path = TEXTURE_DIR / f"{name}.png.meta"
    img.save(png_path, "PNG", optimize=True)
    guid_int = (GUID_ROOT[name] ^ 0x12345678) & 0xFFFFFFFF
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
    _write_with_meta(draw_rod(), "fishing_rod")
    _write_with_meta(draw_raw_fish(), "raw_fish")
    _write_with_meta(draw_cooked_fish(), "cooked_fish")
    print("完成。")


if __name__ == "__main__":
    main()
