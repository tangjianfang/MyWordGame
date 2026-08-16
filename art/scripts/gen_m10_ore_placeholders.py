"""milestone-10 A1：生成夏季合金/机元两张矿石贴图（程序占位）。

背景：m10 新增 4 种矿石，金/粗铁直接复用已入库的 gold-ore.png / iron-ore.png，
夏季合金与机元还没有正式美术。为了让材质库与 BlockDefinitionFilesTests 的
贴图存在性断言先通过，这里按 B-14 矿石的两层合成思路程序生成占位图：

- 底层：逐像素复用 Assets/StreamingAssets/blocks/textures/stone.png（一个像素不改），
  保证与既有四张矿石的石头底纹完全一致
- 斑块层：确定性整数哈希布点，每斑块「暗边 + 主色 + 1 像素左上高光」三阶色

约束（对齐 art/requests/blocks/ores.md 的验收标准）：
- 32×32、RGBA、Alpha 全 255（矿石不透明）
- 斑块面积 12%–20%，4–7 个，互不相连（间隔 ≥2 像素石头）
- 斑块不触碰最外 2 圈像素

确定性：同输入必得同输出，不持有随机数对象（与 ValueNoise2D /
gen_m6_placeholders.py 同约定）。正式美术到位后按
art/requests/blocks/summer-alloy-ore.md / machine-essence-ore.md 替换即可，
本脚本只是留档生成过程，不在任何构建/测试链路里。

用法（仓库根目录）：python art/scripts/gen_m10_ore_placeholders.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEX_DIR = ROOT / "Assets" / "StreamingAssets" / "blocks" / "textures"
SIZE = 32

# 两张占位图的斑块调色板（深/主/亮三阶，与 B-14 既有四矿同结构）。
# 夏季合金取「盛夏草木」的翡翠绿——与钻石的青（#4CC6C4）、铁的暖褐、
# 金的明黄都能一眼区分；机元取科技紫罗兰——现役调色板里没人用紫。
ORES = {
    "summer-alloy-ore": {
        "deep": "#1E6B3A", "main": "#3FA35B", "light": "#8FE39A",
    },
    "machine-essence-ore": {
        "deep": "#5B2A8C", "main": "#8C4FD4", "light": "#C9A6F5",
    },
}


def hash32(*parts: int) -> int:
    """确定性整数哈希（与 Core 的 ValueNoise2D 同思路，不持有随机状态）。"""
    n = 2166136261
    for p in parts:
        n = ((n ^ (p & 0xFF)) * 16777619) & 0xFFFFFFFF
        n = ((n ^ ((p >> 8) & 0xFF)) * 16777619) & 0xFFFFFFFF
    n ^= n >> 13
    n = (n * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFFFFFF


def hex_rgb(s: str) -> tuple[int, int, int]:
    return (int(s[1:3], 16), int(s[3:5], 16), int(s[5:7], 16))


def gen_name_hash(name: str) -> int:
    """把贴图名也揉进盐里，两张图的斑块布局彼此不同。"""
    n = 2166136261
    for ch in name.encode("utf-8"):
        n = ((n ^ ch) * 16777619) & 0xFFFFFFFF
    return n


def place_blobs(name: str, count: int) -> list[tuple[int, int, int]]:
    """确定性布点：返回 (cx, cy, r) 列表。

    约束：中心离边 ≥ r+2（斑块不触碰最外 2 圈），两斑块中心距 ≥ r1+r2+2
    （斑块边缘之间至少留 2 像素石头，B-14 的「互不相连」）。候选由 hash 链
    依次产生，不满足就取下一个候选。
    """
    salt = gen_name_hash(name)
    blobs: list[tuple[int, int, int]] = []
    for i in range(count):
        for attempt in range(256):
            h = hash32(salt, i, attempt)
            cx = 4 + h % (SIZE - 8)
            cy = 4 + (h >> 8) % (SIZE - 8)
            r = 2 + (h >> 16) % 2  # 2 或 3 → 直径 4–6 像素，7 斑块合计落在 12%–20%
            if cx - r < 2 or cy - r < 2 or cx + r > SIZE - 3 or cy + r > SIZE - 3:
                continue
            if all((cx - bx) ** 2 + (cy - by) ** 2 >= (r + br + 2) ** 2
                   for bx, by, br in blobs):
                blobs.append((cx, cy, r))
                break
        else:
            raise RuntimeError(f"{name}: 256 个候选都放不下第 {i} 个斑块，布局参数有误")
    return blobs


def render(name: str, palette: dict[str, str]) -> Image.Image:
    stone = Image.open(TEX_DIR / "stone.png").convert("RGBA")
    img = stone.copy()
    px = img.load()
    deep, main, light = (hex_rgb(palette[k]) for k in ("deep", "main", "light"))

    for cx, cy, r in place_blobs(name, count=7):
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                d2 = dx * dx + dy * dy
                if d2 <= (r - 0.75) ** 2:
                    px[cx + dx, cy + dy] = main + (255,)      # 斑块主体
                elif d2 <= r * r + 0.5:
                    px[cx + dx, cy + dy] = deep + (255,)      # 暗边一圈
        # 1 像素高光，位置在斑块左上（与 B-14 的明暗约定一致）
        px[cx - max(r // 2, 1), cy - max(r // 2, 1)] = light + (255,)

    return img


def verify(name: str, img: Image.Image) -> None:
    """落盘前自检 B-14 的三条硬性验收：面积、外圈、不透明。"""
    stone = Image.open(TEX_DIR / "stone.png").convert("RGB")
    rgb = img.convert("RGB")
    blob = sum(1 for a, b in zip(rgb.tobytes(), stone.tobytes()) if a != b) // 3
    ratio = blob / (SIZE * SIZE)
    assert 0.12 <= ratio <= 0.20, f"{name}: 斑块面积 {ratio:.1%} 不在 12%–20%"

    for y in range(SIZE):
        for x in range(SIZE):
            if x < 2 or y < 2 or x >= SIZE - 2 or y >= SIZE - 2:
                assert rgb.getpixel((x, y)) == stone.getpixel((x, y)), \
                    f"{name}: 斑块触碰最外 2 圈像素 ({x},{y})"
    assert all(a == 255 for _, _, _, a in img.getdata()), f"{name}: 矿石贴图必须完全不透明"


def main() -> None:
    for name, palette in ORES.items():
        img = render(name, palette)
        verify(name, img)
        out = TEX_DIR / f"{name}.png"
        img.save(out)
        print(f"{out.relative_to(ROOT)}  已生成（32×32 RGBA，石头底 + 斑块占位）")


if __name__ == "__main__":
    main()
