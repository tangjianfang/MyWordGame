# 视觉修整 + 玩家角色 + AI 视觉测试实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把里程碑-3 的"能跑通但视觉一片粉/空"收尾——补缺失贴图、加玩家身体几何与走路动画、修 hotbar/选区线框、补 AI 视觉回归基础设施，让游戏看上去像样且后续视觉回归有自动化校验手段。

**Architecture:** 沿用现有 art 流水线（`tools/generate_art.py` + `tools/postprocess_art.py` + `--install`）。Unity 侧把 `BlockMaterialLibrary` 的 magenta 缺图占位降级为「测试可见的硬错误」（不要静默吞），加 `ScreenshotCapture` 工具 + `VisualRegressionTests` 像素采样（不走 vision judge）。玩家身体是 Primitive 拼装 + MaterialPropertyBlock 染色 + 基于速度的简单腿/臂摆动。

**Tech Stack:** Unity 2022.3.62f3c1 + URP 14.0.11 + Python 3（art 流水线）+ dotnet 9.0 测试 + NUnit。AI 文生图走 MiniMax image-01（与 `tools/generate_art.py` 同源）。

## Global Constraints

- 仓库内所有文档/注释/测试断言消息一律中文；新增内容保持一致
- Core 零 Unity 依赖、零警告（`MyWorld.Core.csproj` TreatWarningsAsErrors=true）
- 永远编辑 `Assets/Scripts/Core/**` 和 `Assets/Tests/EditMode/**`，`tools/dotnet/` 下只有 csproj 和 Preview 的 Program.cs
- `.editorconfig` LF 行尾、C# 4 空格缩进、JSON 2 空格
- `tools/dotnet/MyWorld.Tools.sln` 必须显式指定；根目录的 `.sln/.csproj` 已 gitignore
- dotnet 在 Git Bash 下需要 export `ProgramFiles(x86)` / `APPDATA` / `LOCALAPPDATA` / `DOTNET_ROOT`
- Unity 批处理退出码不可信，必须看日志和结果文件
- `global.json` rollForward=latestFeature 锁定 SDK 9.0.x，不要改
- 贴图生成一律走 `tools/generate_art.py`（提示词来源 `art/requests/<dir>/<name>.md`），不要在脚本里另抄
- `art/README.md` 全局调色板方块严格使用
- 视觉测试断言走像素采样，不调 vision judge
- 玩家模型用程序生成的 Primitive（Cube/Capsule），不引入外部 .blend/.fbx

---

## File Structure

| 文件 | 职责 | 任务 |
| --- | --- | --- |
| `Assets/Scripts/Unity/Editor/ScreenshotCapture.cs` | 批处理 / 菜单项截图，主相机 → RenderTexture → PNG | D1 |
| `Assets/Scripts/Unity/Player/PlayerVisual.cs` | 玩家身体几何 + 走路动画 + head-bob + 跳跃姿态 | B1-B3 |
| `Assets/Scripts/Unity/Rendering/BlockMaterialLibrary.cs` | 暴露 magenta 检测，给视觉测试用 | A1 |
| `Assets/Tests/EditMode/Visual/VisualRegressionTests.cs` | 像素采样断言（不全黑/不全粉/有草色/hotbar 不空/材质无 magenta） | E1-E3 |
| `Assets/StreamingAssets/blocks/textures/*.png` | 10 个里程碑-3 方块贴图 | A2 |
| `Assets/Art/Player/skin.png` | 64×64 MC 风格皮肤图（占位） | A3 |
| `Assets/Art/Items/*.png` | 30+ 物品图标 | A4 |
| `Assets/Art/Entities/{pig,sheep,zombie,skeleton,creeper,villager-farmer,villager-librarian,villager-blacksmith}.png` | mob + villager | A5 |
| `tools/postprocess_art.py` | `_blocks/_ui_sky_player/_items/_entities` 注册新 ASSETS | A2-A5 |
| `art/requests/blocks/{planks,log,leaves,sapling,crafting_table,iron_door,lever,redstone_dust}.md` | 8 方块 AI 提示词 | A2 |
| `art/requests/items/*.md` | 物品 AI 提示词 | A4 |
| `art/requests/entities/*.md` | mob + villager AI 提示词 | A5 |
| `Assets/Scripts/Unity/UI/HotbarUI.cs` | 槽位背景用真贴图 + 默认预填 dirt | C1 |
| `Assets/Scripts/Unity/Player/SelectionBox.cs` | 改亮色 + 不参与光照 | C2 |
| `Assets/Scripts/Unity/Player/HandController.cs` | 挥动曲线更紧凑、时长缩短 | C3 |
| `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs` | 给 PlayerController.AddComponent<PlayerVisual>() + 默认预填 dirt | B4 + C1 |

---

# Part 1 · 贴图补完（任务 A）

## Task A1: BlockMaterialLibrary 暴露 magenta 检测 + 硬错误

**Files:**
- Modify: `Assets/Scripts/Unity/Rendering/BlockMaterialLibrary.cs:38-62`
- Test: `Assets/Tests/EditMode/Visual/BlockMaterialLibraryTests.cs`（新建）

**Interfaces:**
- Produces: `bool HasMagentaPixels(Material m)` — 返回材质主纹理含 #FF00FF 像素
- Produces: `static int CountMissingSlots(BlockMaterialLibrary lib)` — 返回指向 _missing 占位的槽位数（构造时记下）

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/Visual/BlockMaterialLibraryTests.cs`：

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class BlockMaterialLibraryTests
    {
        [Test]
        public void MissingMaterial_PureMagentaPixels()
        {
            // 创建一个内部 magenta 占位材质，断言 HasMagentaPixels 返回 true
            // 用 reflection 访问 CreateMissingMaterial 不行（private static），
            // 改方案：直接 new 一个 4×4 magenta 贴图 + URP/Lit 材质，断言为 true。
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.magenta;
            tex.SetPixels(pixels);
            tex.Apply();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                mainTexture = tex,
            };
            try
            {
                Assert.That(BlockMaterialLibrary.HasMagentaPixels(mat), Is.True,
                    "magenta 占位材质必须被识别出来，让视觉测试有钩子报错");
            }
            finally
            {
                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void GrassTopMaterial_NoMagentaPixels()
        {
            // 走真实 BlockRegistryLoader.Load() + BlockMaterialLibrary.Load()，
            // 断言任何槽位的材质都不含 magenta（之前因为 grass-top 等基础贴图都齐全，应过）。
            // 这一条是「现有基础方块贴图无 magenta」的回归保险。
            var registry = BlockRegistryLoader.Load();
            var lib = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);
            try
            {
                for (int i = 0; i < lib.Count; i++)
                {
                    var m = lib.Get(i);
                    if (m == null) continue;
                    Assert.That(BlockMaterialLibrary.HasMagentaPixels(m), Is.False,
                        $"slot {i} 的材质含 magenta 像素（贴图缺失）");
                }
            }
            finally
            {
                lib.Dispose();
            }
        }
    }
}
```

- [ ] **Step 2: 跑测试，确认失败**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockMaterialLibraryTests"
```

期望：CS0103 `BlockMaterialLibrary.HasMagentaPixels` 不存在 → 编译失败（任务要求实现前测试先编译报错，证明测试真在测 API）。

- [ ] **Step 3: 在 BlockMaterialLibrary 加 HasMagentaPixels 静态方法**

修改 `Assets/Scripts/Unity/Rendering/BlockMaterialLibrary.cs`，在 `Dispose()` 之前加：

```csharp
/// <summary>检测材质主纹理是否含 #FF00FF 像素。视觉测试用，让缺贴图方块一眼可见。</summary>
public static bool HasMagentaPixels(Material material)
{
    if (material == null || material.mainTexture == null) return false;
    var tex = material.mainTexture as Texture2D;
    if (tex == null || !tex.isReadable) return false;
    var pixels = tex.GetPixels();
    for (int i = 0; i < pixels.Length; i++)
    {
        var c = pixels[i];
        // #FF00FF == (1, 0, 1)，允许 ±2/255 抖动
        if (c.r > 0.98f && c.g < 0.02f && c.b > 0.98f) return true;
    }
    return false;
}
```

- [ ] **Step 4: 跑测试，确认通过**

```bash
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockMaterialLibraryTests"
```

期望：2/2 通过。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Rendering/BlockMaterialLibrary.cs Assets/Tests/EditMode/Visual/BlockMaterialLibraryTests.cs
git commit -m "render: 暴露 HasMagentaPixels 给视觉测试，magenta 占位材质必须可检测"
```

---

## Task A2: 8 个里程碑-3 方块贴图补完

**Files:**
- Create: `art/requests/blocks/planks.md` `log.md` `leaves.md` `sapling.md` `crafting_table.md` `iron_door.md` `lever.md` `redstone_dust.md`
- Create: `Assets/StreamingAssets/blocks/textures/{planks,log-side,log-top,leaves,sapling,crafting_table-top,crafting_table-side,iron_door,lever,redstone_dust}.png`
- Modify: `tools/postprocess_art.py:348-373`（`_blocks()` 加 8 个 entries）

**Interfaces:**
- 落盘 PNG 必须 32×32，四边无缝（leaves 仅左右），调色板严格遵循 `art/README.md`
- `BlockMaterialLibrary.Load()` 之后所有 15 个方块槽位都不应再指向 `_missing`

- [ ] **Step 1: 写 8 个 art/requests/blocks/*.md**

参考 `art/requests/blocks/cobblestone.md` 的格式（用途 / 规格表 / 调色板 / 视觉描述 / AI 提示词 / 后处理 / 验收标准）。

调色板严格从 `art/README.md` 全局调色板里取：

| 文件 | 调色板 |
| --- | --- |
| planks.md | 木板黄 `#8A6741` `#9C7549` `#B98D57` + 暗 `#6B4E2E` + 亮 `#CFA66B` |
| log.md | 树皮棕 `#4E3B27` `#634C33` `#7A6042` + 木心黄棕 `#8A6A45` `#A5814F` `#BE9A63`（同 log-top 用） |
| leaves.md | 叶绿（深冷）`#24491C` `#2F5D24` `#3F7A2E` `#52963B` `#66B04A`（alpha 15-25%） |
| sapling.md | 苗色：`#3F7A2E` + 木棍色 `#634C33` + 土色 `#5F4630`（16×16 实际 32×32 但 alpha） |
| crafting_table.md | 顶面：木板黄 + 工具格描黑 `#1A1A1A`；侧面：木板黄 + 锯齿斜线 |
| iron_door.md | 铁褐 `#7A6042` `#8A6A4C` `#B9906B` + 铆钉 `#1A1A1A`（alpha 视情况） |
| lever.md | 拉杆石头底 `#7E7E7E` + 木柄 `#634C33` |
| redstone_dust.md | 红石红 `#A02020` `#D03030` `#E84040`（撒在石头底上） |

AI 提示词段落示例（planks.md）：

```
A seamless tileable pixel art texture of wooden planks, top-down flat view,
1024x1024, designed to be downscaled to 32x32 pixel art.

Style: retro voxel game block texture, flat shading, no perspective, even
illumination, no global light direction.

Content: 4 horizontal wooden planks stacked vertically. Each plank is separated
by a 1-pixel dark groove. Each plank shows subtle wood grain (a few short 1-pixel
highlights) running horizontally. No knots, no nails, no text, no logos.

Color palette strictly limited to: #6B4E2E, #8A6741, #9C7549, #B98D57, #CFA66B.
No blue tint, no green tint.

The left and right edges must be visually continuous with each other
(wraparound seamless). Top and bottom must also be seamless.

No anti-aliasing, no gradients, no soft shadows. Hard pixel edges.
```

- [ ] **Step 2: 在 postprocess_art.py `_blocks()` 注册 8 个 ASSETS**

修改 `tools/postprocess_art.py:348-373`，在 `lava` 之后追加：

```python
B("planks", ["#6B4E2E", "#8A6741", "#9C7549", "#B98D57", "#CFA66B"]),
B("log-side", ["#3B2C1C", "#4E3B27", "#634C33", "#7A6042", "#8E7350"]),
B("log-top", ["#4E3B27", "#8A6A45", "#A5814F", "#BE9A63"], tiling="none"),
B("leaves", ["#24491C", "#2F5D24", "#3F7A2E", "#52963B", "#66B04A"],
  transparent=True, alpha_range=(0.15, 0.25)),
B("sapling", ["#3F7A2E", "#634C33", "#5F4630"], transparent=True,
  alpha_range=(0.50, 0.70)),
# crafting_table 在 JSON 里是 top + side 两个贴图；postprocess 只处理单图，
# 所以这一项标记为 source="crafting_table-top"，side 通过 emit 派生或复制 top 后修改
B("crafting_table-top", ["#6B4E2E", "#8A6741", "#9C7549", "#1A1A1A"],
  tiling="4-side"),
B("crafting_table-side", ["#6B4E2E", "#8A6741", "#9C7549", "#B98D57", "#1A1A1A"],
  tiling="4-side"),
B("iron_door", ["#5C5C5C", "#7A6042", "#8A6A4C", "#B9906B", "#1A1A1A"],
  transparent=True, alpha_range=(0.85, 0.95)),
B("lever", ["#5C5C5C", "#7E7E7E", "#8A8A8A", "#634C33", "#1A1A1A"],
  tiling="4-side"),
B("redstone_dust", ["#7E7E7E", "#A02020", "#D03030", "#E84040", "#1A1A1A"],
  tiling="4-side"),
```

- [ ] **Step 3: 跑生成 + 后处理 + install**

```bash
cd C:\tjf\github\MyWordGame
$env:MINIMAX_API_KEY = "你的key"   # PowerShell；bash 用 export MINIMAX_API_KEY=...

python tools/generate_art.py --all --force
# 若提示词没通过解析会立刻报错，停在 precheck；按错误调整

python tools/postprocess_art.py --all
# 这一步会做：最近邻降采样 → 去洋红边 → 调色板量化 → 平铺自检
# 任何一项不合格退出码非 0；按 art/incoming/processed/<name>.log 调

python tools/postprocess_art.py --install
# 按 INSTALL_DIRS 把处理好的图复制到 Assets/StreamingAssets/blocks/textures/ 等
```

期望：8 个新 PNG 都落 `Assets/StreamingAssets/blocks/textures/`，旧 7 个不动。

- [ ] **Step 4: 跑方块材质测试，确认所有槽位都不再含 magenta**

```bash
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockMaterialLibraryTests"
```

期望：2/2 通过（GrassTopMaterial_NoMagentaPixels 现在覆盖到所有 15 个方块）。

- [ ] **Step 5: 跑 BlockDefinitionFilesTests 确认方块 JSON 没改坏**

```bash
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockDefinitionFilesTests"
```

期望：全过。注意：JSON 里的 `textures.top/side/all` 字段引用的是 texture name（不含 `.png`），新加的 PNG 落盘后 `BlockMaterialLibrary.Load()` 会按名字找到。

- [ ] **Step 6: Commit**

```bash
git add art/requests/blocks/ tools/postprocess_art.py Assets/StreamingAssets/blocks/textures/ Assets/StreamingAssets/blocks/textures/*.meta
git commit -m "art: 里程碑-3 8 方块贴图（planks/log/leaves/sapling/crafting_table/iron_door/lever/redstone_dust）+ ASSETS 注册"
```

---

## Task A3: 玩家皮肤 skin.png 占位

**Files:**
- Create: `Assets/Art/Player/skin.png`（64×64 MC 布局占位）
- Test: `Assets/Tests/EditMode/Visual/PlayerSkinTextureTests.cs`

**Interfaces:**
- 64×64 PNG，Alpha 0/255 二态，第一层（head/torso/arms/legs）全不透明，第二层（帽/外套）全透明
- 调色板取自 `art/requests/player/skin.md` 的 11 色

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/Visual/PlayerSkinTextureTests.cs`：

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class PlayerSkinTextureTests
    {
        [Test]
        public void Skin_ExistsAndIs64x64()
        {
            string path = Path.Combine(Application.streamingAssetsPath,
                "..", "..", "Art", "Player", "skin.png");
            path = Path.GetFullPath(path);
            Assert.That(File.Exists(path), Is.True, $"皮肤图缺失: {path}");
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            try
            {
                Assert.That(tex.width, Is.EqualTo(64));
                Assert.That(tex.height, Is.EqualTo(64));
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void Skin_FirstLayer_FullyOpaque()
        {
            // 头区 (0,0)-(32,16) 必须全不透明（不能破洞）
            string path = Path.Combine(Application.streamingAssetsPath,
                "..", "..", "Art", "Player", "skin.png");
            path = Path.GetFullPath(path);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            try
            {
                var pixels = tex.GetPixels(0, 0, 32, 16);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Assert.That(pixels[i].a, Is.GreaterThan(0.99f),
                        $"头区像素 alpha={pixels[i].a:F2}，必须全不透明");
                }
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
```

- [ ] **Step 2: 跑测试，确认失败**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~PlayerSkinTextureTests"
```

期望：File.Exists 失败（skin.png 还没生成）。

- [ ] **Step 3: 用程序生成 skin.png 占位**

由于 AI 皮肤生成 + MC 布局手工填色流程需要手工（spec 写明 skin 不走 AI 整图），先写一个 Python 脚本占位：

新建 `tools/scripts/gen_skin_placeholder.py`：

```python
"""占位玩家皮肤：64×64 MC 布局 + skin.md 调色板。

AI 整图生成 skin 会错位（spec §3.1 解释）。本占位只保证：
- 64×64 + 32 位 PNG
- 第一层 6 区域全不透明
- 第二层 6 区域全透明
- 调色板内颜色
真正的皮肤（鼻子/眼/头发渐变）留待后续手工或 AI 调色参考生成后填。
"""
import struct
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Art" / "Player" / "skin.png"
OUT.parent.mkdir(parents=True, exist_ok=True)

W = H = 64

# MC skin 布局（spec §3.1）
HEAD     = (0, 0, 32, 16)        # 头
TORSO    = (16, 16, 24, 16)      # 躯干
ARM_R    = (40, 16, 16, 16)      # 右臂
ARM_L    = (32, 48, 16, 16)      # 左臂
LEG_R    = (0, 16, 16, 16)       # 右腿
LEG_L    = (16, 48, 16, 16)      # 左腿
# 第二层（帽/外套）全部留空
HAT      = (32, 0, 32, 16)
TORSO_2  = (16, 32, 24, 16)
ARM_R_2  = (40, 32, 16, 16)
ARM_L_2  = (48, 48, 16, 16)
LEG_R_2  = (0, 32, 16, 16)
LEG_L_2  = (0, 48, 16, 16)

FIRST_LAYER = [HEAD, TORSO, ARM_R, ARM_L, LEG_R, LEG_L]

# skin.md 调色板
SKIN = (0xC9, 0x8F, 0x68)
JACKET = (0x3E, 0x7A, 0x9C)
PANTS = (0x4A, 0x4A, 0x5E)
HAIR = (0x3B, 0x2A, 0x1C)
EYE_WHITE = (0xF2, 0xF2, 0xF2)
EYE_PUPIL = (0x2A, 0x3A, 0x6B)
BOOT = (0x5A, 0x46, 0x32)

def fill(buf, region, color, alpha=255):
    x0, y0, w, h = region
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            i = (y * W + x) * 4
            buf[i] = color[0]
            buf[i + 1] = color[1]
            buf[i + 2] = color[2]
            buf[i + 3] = alpha

# 头：肤色 + 头发的 8×4 顶部
raw = bytearray()
for y in range(H):
    raw.append(0)  # PNG filter byte
    for x in range(W):
        raw.extend([0, 0, 0, 0])  # 默认透明

fill(raw, HEAD, SKIN)
# 头发动顶部：头区域 y=0..3 全染发色
for y in range(0, 4):
    for x in range(0, 32):
        i = (y * W + x) * 4 + 1  # +1 跳过 filter byte
        raw[i] = HAIR[0]; raw[i+1] = HAIR[1]; raw[i+2] = HAIR[2]; raw[i+3] = 255
# 眼睛：头区相对 (8,8) 起 1×2，左眼 (8,8)(8,9)，右眼 (10,8)(10,9)
for (ex, ey) in [(8, 8), (10, 8), (8, 9), (10, 9)]:
    i = (ey * W + ex) * 4 + 1
    raw[i] = EYE_PUPIL[0]; raw[i+1] = EYE_PUPIL[1]; raw[i+2] = EYE_PUPIL[2]; raw[i+3] = 255

# 躯干：上衣蓝
fill(raw, TORSO, JACKET)
# 右臂：上衣蓝
fill(raw, ARM_R, JACKET)
# 左臂：上衣蓝
fill(raw, ARM_L, JACKET)
# 右腿：裤子灰
fill(raw, LEG_R, PANTS)
# 左腿：裤子灰
fill(raw, LEG_L, PANTS)

# 第二层：全部留空（已默认透明）

# 写 PNG（32 位 RGBA）
def chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

ihdr = struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0)  # 8 bit, RGBA
idat = zlib.compress(bytes(raw))
png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", idat) + chunk(b"IEND", b"")

OUT.write_bytes(png)
print(f"OK: {OUT}")
```

跑：

```bash
python tools/scripts/gen_skin_placeholder.py
```

- [ ] **Step 4: 跑测试，确认通过**

```bash
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~PlayerSkinTextureTests"
```

期望：2/2 通过。

- [ ] **Step 5: Commit**

```bash
git add tools/scripts/gen_skin_placeholder.py Assets/Art/Player/skin.png Assets/Art/Player/skin.png.meta Assets/Tests/EditMode/Visual/PlayerSkinTextureTests.cs
git commit -m "art: 玩家皮肤 skin.png 占位（64×64 MC 布局 + skin.md 调色板）"
```

> 注：真正的皮肤（眼睛高光、头发走向、上衣衣襟竖线）留后续手工填色或 AI 取色参考后再替换。

---

## Task A4: 物品图标补完（约 30 张）

**Files:**
- Create: `art/requests/items/*.md`（约 30 个物品的提示词，按物品 id）
- Create: `Assets/Art/Items/<id>.png`（运行时由 HotbarUI 读 `StreamingAssets/items/textures/<texture>.png`）

**Interfaces:**
- 物品 JSON 的 `texture` 字段值 = PNG 文件名（不含扩展名）
- HotbarUI 已实现读 `StreamingAssets/items/textures/<texture>.png`，所以 PNG 必须落 `Assets/StreamingAssets/items/textures/`（**不是** `Assets/Art/Items/`——HotbarUI 代码注释里指错了）

- [ ] **Step 1: 改 HotbarUI 让它支持 Assets/Art/Items 与 StreamingAssets/items/textures 双查找**

修改 `Assets/Scripts/Unity/UI/HotbarUI.cs:42-44`：

```csharp
// 优先查 StreamingAssets/items/textures（运行时 Player 数据），
// 退到 Assets/Art/Items（编辑器/打包时 fallback）。
string[] candidates = {
    System.IO.Path.Combine(Application.streamingAssetsPath, "items", "textures", def.Texture + ".png"),
    System.IO.Path.Combine(Application.dataPath, "Art", "Items", def.Texture + ".png"),
};
string resolved = null;
foreach (var c in candidates) if (System.IO.File.Exists(c)) { resolved = c; break; }
if (resolved != null)
{
    var bytes = System.IO.File.ReadAllBytes(resolved);
    t = new Texture2D(2, 2);
    t.LoadImage(bytes);
}
else
{
    t = _missingTex;
}
```

- [ ] **Step 2: 写 30+ 个 art/requests/items/*.md**

列出当前里程碑-3 全部物品：

```bash
ls Assets/StreamingAssets/items/*.json | xargs -I {} basename {} .json
```

按每个 id 写一份 .md，调色板从 art/README.md 全局 + 物品本身的视觉定位取。例如 `diamond.md`：

```markdown
# I-05 钻石

## 用途

热键栏 + 合成界面里的钻石图标。蓝色宝石形状，高对比度。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 16 × 16 像素 |
| 生成尺寸 | 512 × 512（再降采样） |
| 平铺 | 否 |
| Alpha | **有**（仅 0 / 255） |

## 调色板

| 用途 | HEX |
| --- | --- |
| 宝石暗 | `#1E7C7C` |
| 宝石主 | `#4CC6C4` |
| 宝石亮 | `#A8F2EF` |
| 描边 | `#1A1A1A` |

## AI 提示词

```
A single diamond gem icon for an inventory slot, 512x512 pixel art designed
to be downscaled to 16x16. Centered, with a 1-pixel black outline, classic
beveled gem shape (octagonal diamond cut) with 3 visible facets. Transparent
background (pure magenta #FF00FF keyout color).

Color palette strictly: #1E7C7C, #4CC6C4, #A8F2EF, #1A1A1A only.

No text, no shadow on background, no glow. Hard pixel edges, no anti-aliasing.
```

## 后处理

降采样到 16×16 + 洋红键控为透明 + 调色板量化。

## 验收

- 16×16 PNG，32 位 RGBA
- 主体可见区域占图 ≥ 60%
- 透明像素 ≥ 30%（背景）
```

其他 29+ 物品按相同模板写（plank、stick、cobblestone、iron_ingot、diamond、netherite_ingot、coal、raw_porkchop、wool、rotten_flesh、beet、mung_bean、bowl、bedrock、beet_soup、mung_bean_soup、bowl_of_water、wooden_sword、stone_sword、iron_sword、diamond_sword、netherite_sword、bedrock_sword、wooden_pickaxe、stone_pickaxe、iron_pickaxe、diamond_pickaxe、netherite_pickaxe、bedrock_pickaxe、wooden_axe、stone_axe、iron_axe、wooden_shovel、stone_shovel、iron_shovel、emerald、book、enchanted_book、lapis、redstone、string、gunpowder、bone、skull、crafting_table、redstone_dust）。

- [ ] **Step 3: 在 postprocess_art.py `_ui_sky_player` 之后加 `_items() + _entities()` 注册 ASSETS**

修改 `tools/postprocess_art.py`，在 `_ui_sky_player()` 后追加：

```python
ITEMS = [
    # (name, category, size, palette, transparent, alpha_range)
    ("plank", (16, 16), ["#6B4E2E", "#8A6741", "#9C7549", "#B98D57"], True, (0.0, 0.05)),
    ("stick", (16, 16), ["#634C33", "#7A6042", "#8E7350"], True, (0.4, 0.6)),
    # ... 30+ entries
]

ENTITIES = [
    ("pig", (32, 32), ["#F2B0B0", "#C98F68", "#8E6F4E"], False, None),
    # ...
]

def _items() -> list[Asset]:
    return [
        Asset(n, "item", sz, [H(c) for c in pal], "none",
              transparent=t, alpha_range=a, kind="ai")
        for (n, sz, pal, t, a) in ITEMS
    ]

def _entities() -> list[Asset]:
    return [
        Asset(n, "entity", sz, [H(c) for c in pal], "none",
              transparent=t, alpha_range=a, kind="ai")
        for (n, sz, pal, t, a) in ENTITIES
    ]

# 替换 ASSETS 行：
ASSETS: dict[str, Asset] = {a.name: a for a in
                             _blocks() + _ores() + _ui_sky_player() + _items() + _entities()}
```

`ITEMS` + `ENTITIES` 的全部条目见 `art/requests/items/*.md` 与 `art/requests/entities/*.md`（每份需求文件里都列了具体调色板）。

- [ ] **Step 4: 跑生成 + 后处理 + install**

```bash
cd C:\tjf\github\MyWordGame
$env:MINIMAX_API_KEY = "你的key"
python tools/generate_art.py --all --force
python tools/postprocess_art.py --all
python tools/postprocess_art.py --install
```

期望：`Assets/StreamingAssets/items/textures/<id>.png` 30+ 张，`Assets/Art/Entities/<id>.png` 5 张 mob + 3 张 villager。

- [ ] **Step 5: 加 ItemsLoadedInRegistry 测试**

新建 `Assets/Tests/EditMode/Items/ItemTextureFileTests.cs`：

```csharp
using System.IO;
using MyWorld.Core.Items;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class ItemTextureFileTests
    {
        [Test]
        public void EveryItem_TextureFile_ExistsOrIsMissingPlaceholder()
        {
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            var itemDocs = Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText);
            var db = ItemDatabase.FromJson(itemDocs);

            string texDir = Path.Combine(Application.streamingAssetsPath, "items", "textures");
            int missing = 0;
            foreach (var kv in db.ById)
            {
                string path = Path.Combine(texDir, kv.Value.Texture + ".png");
                if (!File.Exists(path))
                {
                    missing++;
                    TestContext.Out.WriteLine($"  缺贴图：{kv.Key} -> {kv.Value.Texture}");
                }
            }
            Assert.That(missing, Is.EqualTo(0),
                "所有物品的 texture 字段都应有对应 PNG，否则 HotbarUI 显示 missingTex 棕色");
        }
    }
}
```

> `using System.Linq;` 别忘了加。

- [ ] **Step 6: 跑测试 + 全量测试**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~ItemTextureFileTests"
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：ItemTextureFileTests 通过；全量 ≥ 320 通过（多了 BlockMaterialLibraryTests/PlayerSkin/ItemTexture 三个）。

- [ ] **Step 7: Commit**

```bash
git add art/requests/items/ tools/postprocess_art.py Assets/StreamingAssets/items/textures/ Assets/StreamingAssets/items/textures/*.meta Assets/Art/Items/ Assets/Art/Items/*.meta Assets/Art/Entities/ Assets/Art/Entities/*.meta Assets/Tests/EditMode/Items/ItemTextureFileTests.cs Assets/Scripts/Unity/UI/HotbarUI.cs
git commit -m "art: 30+ 物品图标 + 8 mob/villager 图标 + HotbarUI 双路径查找"
```

---

# Part 2 · 截图诊断 + 玩家 + UI 修整 + 视觉测试

（写在 part2.md，避免单文件过长）

