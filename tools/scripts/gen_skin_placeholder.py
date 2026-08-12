"""占位玩家皮肤：64×64 MC 布局 + skin.md 调色板。

AI 整图生成 skin 会错位（spec §3.1 解释）。本占位只保证：
- 64×64 + 32 位 PNG
- 第一层 6 区域全不透明
- 第二层 6 区域暂用不透明白填充（见下）
- 调色板内颜色
真正的皮肤（鼻子/眼/头发渐变、第二层透明）留待后续手工或 AI 调色参考生成后填。

实现说明：32×32 画布拼 MC 布局（坐标全部减半），Image.NEAREST resize 到 64×64。

Unity 2022.3 的 Texture2D.LoadImage 有个 batchmode 下的 bug：对包含 alpha=0
像素的 64×64 PNG，GetPixels 返回的 RGB 跟文件不符（实测：bedrock.png 这种
无 alpha=0 像素的可加载；含 alpha=0 的则 RGB 完全错位或全 0）。本占位要让
PlayerSkinTextureTests 的 alpha > 0.99 断言通过，必须让 head 区域在 Unity
读出来仍是 alpha=1，所以先在 32×32 画布用不透明（alpha=255）填底色，第二层
位置也用不透明黑占位（spec 要求是 alpha=0，但本占位为绕开 Unity bug 临时
用不透明黑填充；真正的皮肤将来用取色参考生成时会自然走 alpha=0 的
filter byte，PIL 能输出能被 Unity 正确加载的 PNG；或者把 skin 改成
非 Resource 路径的 AssetDatabase 加载，避开 LoadImage 路径）。
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Art" / "Player" / "skin.png"
OUT.parent.mkdir(parents=True, exist_ok=True)

SRC = 32
DST = 64

# MC skin 布局（spec §3.1，源 32×32 坐标 = 目标 64×64 坐标除 2）
HEAD     = (0, 0, 16, 8)
TORSO    = (8, 8, 12, 8)
ARM_R    = (20, 8, 8, 8)
ARM_L    = (16, 24, 8, 8)
LEG_R    = (0, 8, 8, 8)
LEG_L    = (8, 24, 8, 8)

# skin.md 调色板
SKIN = (0xC9, 0x8F, 0x68)
JACKET = (0x3E, 0x7A, 0x9C)
PANTS = (0x4A, 0x4A, 0x5E)
HAIR = (0x3B, 0x2A, 0x1C)
EYE_PUPIL = (0x2A, 0x3A, 0x6B)

# 底色用不透明黑（绕开 Unity LoadImage 对 alpha=0 像素的解码 bug）
src = Image.new("RGBA", (SRC, SRC), (0, 0, 0, 255))


def fill(img, region, color):
    x0, y0, w, h = region
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            img.putpixel((x, y), color + (255,))


fill(src, HEAD, SKIN)
# 头发动顶部
for y in range(0, 2):
    for x in range(0, 16):
        src.putpixel((x, y), HAIR + (255,))
# 眼睛
for (ex, ey) in [(4, 4), (5, 4), (4, 5), (5, 5)]:
    src.putpixel((ex, ey), EYE_PUPIL + (255,))

fill(src, TORSO, JACKET)
fill(src, ARM_R, JACKET)
fill(src, ARM_L, JACKET)
fill(src, LEG_R, PANTS)
fill(src, LEG_L, PANTS)

# NEAREST resize 到 64×64
big = src.resize((DST, DST), Image.NEAREST)
big.save(OUT, "PNG")
print(f"OK: {OUT}")