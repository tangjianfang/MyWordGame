"""milestone-11 W3-4：生成特效帧贴图（程序占位）。

背景：m11 第 3 波氛围任务（天气/云/粒子）需要两张特效贴图——
fx-explosion 三帧爆炸（art/requests/effects/fx-explosion.md）与
magic-enchant-column 附魔光柱（art/requests/effects/magic-enchant-column.md）。
正式美术未出，按 m10/m11 批（gen_m10_ore_placeholders.py /
gen_m11_enchant_placeholders.py）同模式程序生成占位，保证：

- ParticlePool 运行时加载路径（Assets/StreamingAssets/fx/）恒有图可读——
  正式美术到位后同名替换即可（后处理产物名 fx-explosion-0/1/2 已与
  art/requests/effects/fx-explosion.md 的「拆 3 帧」后处理约定对齐）
- 尺寸 32×32、RGBA、**Alpha 只有 0/255 两态**（洋红键控等价：背景直接透明）
- 调色板逐色取自两份需求文档，不引入需求外颜色
- 确定性：同输入必得同输出，逐像素整数哈希，不持随机数对象

用法（仓库根目录）：python art/scripts/gen_m11_fx_placeholders.py
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FX_DIR = ROOT / "Assets" / "StreamingAssets" / "fx"
SIZE = 32

# fx-explosion.md 调色板（焰心白 → 烬暗，5 色）
FIRE_CORE = (0xFFF2C8, 0xF7DA7A, 0xF79B22, 0xD64B0A, 0x8A2400)
# magic-enchant-column.md 调色板（柱紫/柱心亮/光屑金，3 色）
COL_PURPLE = (0x9C6AE8, 0xF4EEFC, 0xDCAE3A)


def rgb(v: int) -> tuple[int, int, int]:
    return ((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF)


def hash32(x: int, y: int, salt: int) -> int:
    """确定性整数哈希（与 gen_m11_enchant_placeholders.py 同一实现）。"""
    n = (x * 374761393 + y * 668265263 + salt * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return (n ^ (n >> 16)) & 0xFFFFFFFF


def explosion_frame(idx: int) -> Image.Image:
    """三帧爆炸：0 起爆小球 / 1 全爆环 / 2 余烬烟团。火焰基线对齐底部（y=0 行），
    逐帧循环不跳动；帧 2 的余烬用哈希噪点撒（确定性），零星火星。"""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    cx = SIZE // 2
    # 各帧火球中心与半径（基线对齐：中心都压在底部附近）
    centers = {0: (cx, 7, 6), 1: (cx, 11, 14), 2: (cx, 8, 10)}
    ccx, ccy, r = centers[idx]

    for y in range(SIZE):
        for x in range(SIZE):
            dist = ((x - ccx) ** 2 + (y - ccy) ** 2) ** 0.5
            if idx == 2:
                # 余烬帧：半径内按哈希噪声散布，外围大半挖空成烟团缺口
                noise = hash32(x, y, 0xE3B2) % 100
                edge = dist + noise * 0.03
                if edge <= r * 0.5:
                    color = rgb(FIRE_CORE[3]) if noise % 5 == 0 else rgb(FIRE_CORE[4])
                elif edge <= r:
                    color = rgb(FIRE_CORE[4]) if noise % 3 else rgb(FIRE_CORE[3])
                else:
                    continue
            elif idx == 1:
                # 全爆帧：白热核心 → 黄 → 橙 → 红环
                if dist <= 3:
                    color = rgb(FIRE_CORE[0])
                elif dist <= 7:
                    color = rgb(FIRE_CORE[1])
                elif dist <= 11:
                    color = rgb(FIRE_CORE[2])
                elif dist <= r:
                    color = rgb(FIRE_CORE[3])
                else:
                    continue
            else:
                # 起爆帧：小火球白心黄身橙底红边
                if dist <= 2:
                    color = rgb(FIRE_CORE[0])
                elif dist <= 4:
                    color = rgb(FIRE_CORE[1])
                elif dist <= r - 1:
                    color = rgb(FIRE_CORE[2])
                elif dist <= r:
                    color = rgb(FIRE_CORE[3])
                else:
                    continue
            px[x, y] = color + (255,)
    return img


def enchant_column() -> Image.Image:
    """附魔光柱：竖直紫色光带贯穿画布 + 近白亮缝 + 两侧金色菱形光屑 + 底部紫晕。
    左右对称（贴图按柱高竖向拉伸采样，验收要求无断裂感 → 柱身整列恒色）。"""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    purple, core, gold = (rgb(c) for c in COL_PURPLE)

    # 柱身：x∈[12,19] 整列（左右对称 ±4），y 全高
    for y in range(SIZE):
        for x in range(12, 20):
            px[x, y] = purple + (255,)
    # 柱心亮缝：x∈[15,16]
    for y in range(SIZE):
        for x in (15, 16):
            px[x, y] = core + (255,)
    # 底部紫晕底盘：y∈[0,2] x∈[9,22]
    for y in range(0, 3):
        for x in range(9, 23):
            px[x, y] = purple + (255,)
    # 金色菱形光屑（2×2 错位分布，两侧对称各 2 颗）
    for gx, gy in ((9, 8), (21, 8), (10, 15), (20, 15), (9, 22), (21, 22)):
        for dy in (0, 1):
            for dx in (0, 1):
                px[gx + dx, gy + dy] = gold + (255,)
    return img


def main() -> None:
    FX_DIR.mkdir(parents=True, exist_ok=True)
    outputs = []
    for i in range(3):
        path = FX_DIR / f"fx-explosion-{i}.png"
        explosion_frame(i).save(path)
        outputs.append(path)
    path = FX_DIR / "magic-enchant-column.png"
    enchant_column().save(path)
    outputs.append(path)
    print(f"生成 {len(outputs)} 张特效占位贴图（32×32 RGBA，Alpha 仅 0/255）：")
    for p in outputs:
        print(f"  {p.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
