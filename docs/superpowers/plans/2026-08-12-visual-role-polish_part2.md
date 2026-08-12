# 视觉修整 + 玩家角色 + AI 视觉测试实施计划（part 2）

> 接 `2026-08-12-visual-role-polish.md`。
> Part 2 涵盖：D 截图诊断 + 修复 · B 玩家身体与动画 · C UI 修整 · E 视觉测试基础设施。

---

## Task D1: ScreenshotCapture 工具

**Files:**
- Create: `Assets/Scripts/Unity/Editor/ScreenshotCapture.cs`
- Test: 手工（无代码测试，截图人工 review）

**Interfaces:**
- `public static void Capture(string outputPath, int width = 1280, int height = 720)`
- `public static void CaptureAll(string outputDir)` —— 拍预设 5 张（overworld / hotbar / selected-block / inventory / crafting）

- [ ] **Step 1: 写 ScreenshotCapture.cs**

```csharp
using System;
using System.IO;
using MyWorld.Unity.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 批处理截图工具：把场景里 "相机" GameObject 的 Camera 强制渲染到 RenderTexture，
    /// 拷到 Texture2D 落盘 PNG。可被 -executeMethod 无头调用。
    /// </summary>
    public static class ScreenshotCapture
    {
        public static string DefaultCameraName = "相机";

        [MenuItem("MyWorld/截图：当前场景")]
        public static void CaptureCurrentSceneMenu()
        {
            string path = Path.Combine("Builds", "screenshots",
                $"manual-{DateTime.Now:HHmmss}.png");
            Capture(path);
            Debug.Log($"[ScreenshotCapture] {path}");
        }

        public static void Capture(string outputPath, int width = 1280, int height = 720)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // 找到主相机（用 tag=MainCamera 更稳，不要靠 GameObject 名）
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[ScreenshotCapture] 场景里没有 MainCamera");
                return;
            }

            // 预渲染：让所有 MonoBehaviour OnEnable + Start + 几帧 Tick 完成。
            // 编辑器非播放态下 Camera.Render() 会触发整个 URP 管线一次。
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>拍预设 5 张关键场景。要求场景里有 WorldBootstrap 且 bootstrap 流程跑通。</summary>
        public static void CaptureAll(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            Capture(Path.Combine(outputDir, "overworld.png"));
            // TODO（不在本任务范围，由后续任务在 ScreenshotCapture 加更多预设位）
        }
    }
}
```

- [ ] **Step 2: 跑通批处理，拿到 overworld.png**

先确保 Build OK（prebuild 完成后 .exe 与 Assets 同步）。在仓库根：

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath . -quit \
  -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll \
  -logFile Builds/logs/screenshot.log
```

> `-executeMethod` 后跟完整类名.方法名。

期望：`Builds/screenshots/overworld.png` 落地（不报错）。

- [ ] **Step 3: 人工 review 截图**

把 PNG 拷到一个能看的地方：

```bash
cp Builds/screenshots/overworld.png /tmp/overworld.png
# 用系统看图工具打开
```

人工确认：
- 主世界渲染（草方块 + 蓝天 + 山丘）
- hotbar 在屏幕底部
- 玩家是第一人称（看到自己手臂或手持物品，看不到全身体）
- 没有全屏 magenta（任务 A 完成后这一条必须成立）

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Unity/Editor/ScreenshotCapture.cs
git commit -m "tool: ScreenshotCapture 批处理截图（Camera.Render → RT → PNG）"
```

---

## Task D2: 积木颜色诊断（基于截图）

**Files:**
- Create: `tools/scripts/analyze_screenshot.py`（像素统计，找 magenta / 草绿 / 错误色块占比）
- Create: `Assets/Tests/EditMode/Visual/ScreenshotPixelStatsTests.cs`（dotnet 测的版本，避免 Python 依赖）

**Interfaces:**
- `AnalyzeScreenshot.Analyze(string path) → PixelStats { magentaFraction, grassGreenFraction, averageBrightness }`
- 不调用 AI vision，纯 numpy 像素采样

- [ ] **Step 1: 写 Python 像素统计脚本**

新建 `tools/scripts/analyze_screenshot.py`：

```python
"""截图像素统计：用于排查积木颜色错位、magenta 占位、未渲染块。

不依赖 AI，纯 numpy 像素采样：
- magenta 像素占比（#FF00FF ±3）：> 5% 说明有缺贴图方块
- 草绿像素占比：< 5% 说明草块没渲染
- 平均亮度：< 30/255 全黑（光线异常）；> 220 全白（曝光异常）
"""
from __future__ import annotations

import sys
from dataclasses import dataclass

import numpy as np
from PIL import Image


@dataclass
class PixelStats:
    magenta_fraction: float
    grass_green_fraction: float
    avg_brightness: float
    black_fraction: float
    width: int
    height: int


def analyze(path: str) -> PixelStats:
    img = np.asarray(Image.open(path).convert("RGB"), dtype=np.int16)
    h, w, _ = img.shape

    r, g, b = img[..., 0], img[..., 1], img[..., 2]

    magenta = ((r > 250) & (g < 8) & (b > 250)).mean()
    # 草绿：#4A7E2F ~ #74B84E（RGB 74..126, 126..148, 47..78）
    grass = ((r >= 70) & (r <= 130) & (g >= 120) & (g <= 200) & (b >= 40) & (b <= 90)).mean()
    brightness = (r + g + b) / 3
    avg_b = brightness.mean()
    black = (brightness < 10).mean()
    return PixelStats(
        magenta_fraction=float(magenta),
        grass_green_fraction=float(grass),
        avg_brightness=float(avg_b),
        black_fraction=float(black),
        width=w,
        height=h,
    )


if __name__ == "__main__":
    p = analyze(sys.argv[1])
    print(f"size:           {p.width}x{p.height}")
    print(f"magenta:        {p.magenta_fraction*100:.2f}%  (期望 < 5%)")
    print(f"grass_green:    {p.grass_green_fraction*100:.2f}%  (期望 > 5%)")
    print(f"avg_brightness: {p.avg_brightness:.1f}  (期望 30-220)")
    print(f"black:          {p.black_fraction*100:.2f}%  (期望 < 50%)")
```

跑一次：

```bash
python tools/scripts/analyze_screenshot.py Builds/screenshots/overworld.png
```

期望：magenta ≈ 0%（任务 A 后），grass_green > 5%，avg_brightness 30-220。

- [ ] **Step 2: 写 dotnet 版（让 EditMode 测试可调）**

新建 `Assets/Tests/EditMode/Visual/ScreenshotPixelStatsTests.cs`：

```csharp
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class ScreenshotPixelStatsTests
    {
        private static string LocateLatest()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Builds", "screenshots");
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, "overworld.png");
            return files.Length == 0 ? null : files[0];
        }

        [Test]
        public void Overworld_HasLessThan5PercentMagenta()
        {
            string latest = LocateLatest();
            if (latest == null) Assert.Ignore("没有 overworld.png——先跑 ScreenshotCapture");
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(latest));
            try
            {
                var pixels = tex.GetPixels();
                int magenta = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    if (c.r > 0.98f && c.g < 0.03f && c.b > 0.98f) magenta++;
                }
                float frac = (float)magenta / pixels.Length;
                Assert.That(frac, Is.LessThan(0.05f),
                    $"magenta 占 {frac*100:F2}%（超 5%），说明还有缺贴图方块");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void Overworld_HasAtLeast5PercentGrassGreen()
        {
            string latest = LocateLatest();
            if (latest == null) Assert.Ignore("没有 overworld.png——先跑 ScreenshotCapture");
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(latest));
            try
            {
                var pixels = tex.GetPixels();
                int grass = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    if (c.r >= 0.27f && c.r <= 0.51f &&
                        c.g >= 0.47f && c.g <= 0.78f &&
                        c.b >= 0.16f && c.b <= 0.36f)
                        grass++;
                }
                float frac = (float)grass / pixels.Length;
                Assert.That(frac, Is.GreaterThan(0.05f),
                    $"草绿占 {frac*100:F2}%（不足 5%），地表可能没渲染草地");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
```

- [ ] **Step 3: 跑测试 + 诊断报告**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~ScreenshotPixelStatsTests"
```

- [ ] **Step 4: 根据 analyze_screenshot.py 输出，按需修代码**

| 现象 | 修法 |
| --- | --- |
| magenta > 5% | 回到 Task A2 检查 postprocess/install 步骤有没有漏掉某方块 |
| grass_green < 5% | 检查 `BlockRegistry.GetById("grass").Textures` 字段名 + `BlockMaterialLibrary.Load` 是否找到 grass-top / grass-side |
| avg_brightness < 30 | 检查 Directional Light 是否启用（`MyWorld.Unity.Environment.DayNightCycle.SunLight`）|
| black > 50% | 检查天空盒（Camera 背景） + 玩家位置是否出生在地表下方 |

- [ ] **Step 5: Commit**

```bash
git add tools/scripts/analyze_screenshot.py Assets/Tests/EditMode/Visual/ScreenshotPixelStatsTests.cs
git commit -m "tool: 截图像素统计脚本 + dotnet 版（magenta/grass 占比）"
```

---

## Task B1: 玩家身体几何骨架

**Files:**
- Create: `Assets/Scripts/Unity/Player/PlayerVisual.cs`
- Modify: `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:122`（添加组件）

**Interfaces:**
- `public sealed class PlayerVisual : MonoBehaviour`
- 挂在 `PlayerController` 上，启动时自动生成 head/torso/arms/legs 子物体
- 暴露 `WalkPhase` 字段给后续动画任务读写

- [ ] **Step 1: 写 PlayerVisual.cs 骨架（无走路动画）**

新建 `Assets/Scripts/Unity/Player/PlayerVisual.cs`：

```csharp
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 第三人称可见的玩家身体 + 走路动画。
    /// 纯 Primitive 拼装，MaterialPropertyBlock 染色。第一人称视角下也保留物体，
    /// 由 CameraThirdPerson 控制相机位置规避自遮挡。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        // 调色板——与 art/requests/player/skin.md 一致
        private static readonly Color SkinColor   = new Color(0xC9/255f, 0x8F/255f, 0x68/255f);
        private static readonly Color JacketColor = new Color(0x3E/255f, 0x7A/255f, 0x9C/255f);
        private static readonly Color PantsColor  = new Color(0x4A/255f, 0x4A/255f, 0x5E/255f);
        private static readonly Color BootColor   = new Color(0x5A/255f, 0x46/255f, 0x32/255f);
        private static readonly Color HairColor   = new Color(0x3B/255f, 0x2A/255f, 0x1C/255f);

        private Transform _head, _torso, _armL, _armR, _legL, _legR;
        public float WalkPhase { get; set; }

        private void Awake()
        {
            _torso = MakePart("Torso", new Vector3(0.6f, 0.7f, 0.3f), new Vector3(0f, 0.85f, 0f), JacketColor);
            _head  = MakePart("Head",  new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.65f, 0f), SkinColor);
            _armL  = MakePart("ArmL",  new Vector3(0.2f, 0.7f, 0.2f), new Vector3(-0.4f, 0.9f, 0f), JacketColor);
            _armR  = MakePart("ArmR",  new Vector3(0.2f, 0.7f, 0.2f), new Vector3(+0.4f, 0.9f, 0f), JacketColor);
            _legL  = MakePart("LegL",  new Vector3(0.25f, 0.85f, 0.25f), new Vector3(-0.15f, 0.4f, 0f), PantsColor);
            _legR  = MakePart("LegR",  new Vector3(0.25f, 0.85f, 0.25f), new Vector3(+0.15f, 0.4f, 0f), PantsColor);
        }

        private static Transform MakePart(string name, Vector3 scale, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = scale;
            go.transform.localPosition = localPos;
            // 移除自带的 BoxCollider，避免和 ChunkStreamer 玩家位置冲突
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            ApplyColor(go, color);
            return go.transform;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static void ApplyColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
        }
    }
}
```

- [ ] **Step 2: 在 WorldBootstrap 添加组件**

修改 `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:122`（在 CameraThirdPerson 之前）：

```csharp
// 17.5 玩家身体（part2 任务 B1）
gameObject.AddComponent<MyWorld.Unity.Player.PlayerVisual>();
```

> 玩家 GameObject 就是 bootstrap 自身（WorldBootstrap.Awake 里 `gameObject.AddComponent<PlayerController>()`）。

- [ ] **Step 3: 跑完整流水线**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)" && export APPDATA="${APPDATA:-C:\Users\tjf\AppData\Roaming}" && export LOCALAPPDATA="${LOCALAPPDATA:-C:\Users\tjf\AppData\Local}" && export DOTNET_ROOT='C:\Program Files\dotnet'
./tools/scripts/build-and-run.sh --skip-tests --clean --no-launch
```

期望：Build OK。

- [ ] **Step 4: 跑 ScreenshotCapture 拍第三人称图**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath . -quit \
  -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll \
  -logFile Builds/logs/screenshot.log
```

人工 review `Builds/screenshots/overworld.png` —— 第一人称下看不到全身，要按 F5 切第三人称。**这条留给 Task D3 处理**：让 ScreenshotCapture 默认拍第三人称。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Player/PlayerVisual.cs Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs
git commit -m "player: PlayerVisual 几何骨架（头/身/臂/腿）+ PropertyBlock 染色"
```

---

## Task B2: 走路动画

**Files:**
- Modify: `Assets/Scripts/Unity/Player/PlayerVisual.cs`（加 Update 走路摆动逻辑）

**Interfaces:**
- `PlayerVisual.WalkPhase` 由速度驱动：`WalkPhase += speed * dt * 8f`
- 速度 = `PlayerController.State.Velocity.XZ` 长度（拿不到直接读 transform.position delta 也行）

- [ ] **Step 1: 加 Update 动画逻辑**

修改 `Assets/Scripts/Unity/Player/PlayerVisual.cs`，在 `Awake` 之后加：

```csharp
private PlayerController _controller;
private Vector3 _lastPos;
private float _headBaseY = 1.65f;

private void Start()
{
    _controller = GetComponent<PlayerController>();
    _lastPos = transform.position;
}

private void Update()
{
    if (_controller == null) return;
    Vector3 pos = transform.position;
    Vector3 delta = pos - _lastPos;
    _lastPos = pos;

    float speed = new Vector2(delta.x, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

    if (speed > 0.1f)
    {
        WalkPhase += Time.deltaTime * speed * 8f;
    }

    float swing = Mathf.Sin(WalkPhase) * 30f;        // 度
    float bob = Mathf.Abs(Mathf.Sin(WalkPhase * 2f)) * 0.08f;

    if (_legL != null) _legL.localRotation = Quaternion.Euler(+swing, 0f, 0f);
    if (_legR != null) _legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
    if (_armL != null) _armL.localRotation = Quaternion.Euler(-swing, 0f, 0f);
    if (_armR != null) _armR.localRotation = Quaternion.Euler(+swing, 0f, 0f);
    if (_head != null) _head.localPosition = new Vector3(0f, _headBaseY + bob, 0f);
}
```

- [ ] **Step 2: 跑流水线 + 截图**

```bash
cd C:\tjf\github\MyWordGame
./tools/scripts/build-and-run.sh --skip-tests --clean --no-launch
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath . -quit \
  -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll \
  -logFile Builds/logs/screenshot.log
```

人工 review：第三人称下玩家走路腿/臂摆动（PlayHarness 不会让玩家真走，这一步靠 standalone 启动 + 看 60 秒）。

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Unity/Player/PlayerVisual.cs
git commit -m "player: 走路动画（腿/臂反相摆动 + 头上下身动）"
```

---

## Task B3: 第三人称截图

**Files:**
- Modify: `Assets/Scripts/Unity/Editor/ScreenshotCapture.cs`（加第三人称预设）

**Interfaces:**
- `ScreenshotCapture.CaptureThirdPerson()`：找到玩家 + CameraThirdPerson，强制启用第三人称相机，截图，再恢复

- [ ] **Step 1: 加 CaptureThirdPerson 方法**

修改 `ScreenshotCapture.cs`，加：

```csharp
[MenuItem("MyWorld/截图：第三人称玩家")]
public static void CaptureThirdPersonMenu()
{
    string path = Path.Combine("Builds", "screenshots",
        $"thirdperson-{DateTime.Now:HHmmss}.png");
    CaptureThirdPerson(path);
    Debug.Log($"[ScreenshotCapture] {path}");
}

public static void CaptureThirdPerson(string outputPath, int width = 1280, int height = 720)
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

    var tpCam = GameObject.Find("ThirdPersonCamera");
    if (tpCam == null)
    {
        Debug.LogError("[ScreenshotCapture] 场景里没有 ThirdPersonCamera（需要玩家先 Bootstrap 一次）");
        return;
    }
    var cam = tpCam.GetComponent<Camera>();
    if (cam == null)
    {
        Debug.LogError("[ScreenshotCapture] ThirdPersonCamera 上没有 Camera 组件");
        return;
    }

    var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
    rt.Create();
    var prevTarget = cam.targetTexture;
    var prevActive = RenderTexture.active;
    cam.targetTexture = rt;
    try
    {
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }
    finally
    {
        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }
}
```

- [ ] **Step 2: 改 CaptureAll 加第三张**

修改 `ScreenshotCapture.CaptureAll`：

```csharp
public static void CaptureAll(string outputDir)
{
    Directory.CreateDirectory(outputDir);
    Capture(Path.Combine(outputDir, "overworld.png"));
    CaptureThirdPerson(Path.Combine(outputDir, "third-person.png"));
}
```

- [ ] **Step 3: 跑批处理**

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath . -quit \
  -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll \
  -logFile Builds/logs/screenshot.log
```

期望：`Builds/screenshots/third-person.png` 落地，玩家身体可见。

- [ ] **Step 4: 视觉测试断言**

```bash
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~ScreenshotPixelStatsTests"
```

期望：overworld.png 的 magenta 占比 < 5%（任务 A 已完成）。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/Editor/ScreenshotCapture.cs
git commit -m "tool: 第三人称相机截图（验证玩家身体可见）"
```

---

## Task C1: Hotbar 改用真贴图 + 默认预填 dirt

**Files:**
- Modify: `Assets/Scripts/Unity/UI/HotbarUI.cs`（槽位背景改真贴图 + 默认预填）
- Modify: `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:79-82`（Inventory 初始化后塞 64 dirt）

**Interfaces:**
- HotbarUI._slotBg 改读 `Assets/Art/UI/hotbar-slot.png`（已有）
- HotbarUI._selectBorder 改读 `Assets/Art/UI/hotbar-slot-selected.png`（已有）
- 默认热键栏第 0 格预填 64 个 dirt（让首次进游戏 hotbar 不空）

- [ ] **Step 1: 改 HotbarUI 用真贴图**

修改 `Assets/Scripts/Unity/UI/HotbarUI.cs:22-34`：

```csharp
private void EnsureTextures()
{
    if (_slotBg != null) return;

    _slotBg = LoadTextureOrFallback("Assets/Art/UI/hotbar-slot.png",
        new Color(0, 0, 0, 0.6f));
    _selectBorder = LoadTextureOrFallback("Assets/Art/UI/hotbar-slot-selected.png",
        Color.white);
    _missingTex = new Texture2D(1, 1);
    _missingTex.SetPixel(0, 0, new Color(0.6f, 0.2f, 0.9f, 1f));
    _missingTex.Apply();
}

private static Texture2D LoadTextureOrFallback(string path, Color fallbackColor)
{
    // Application.dataPath = Assets/，运行时切到 Assets/Art/...
    string full = System.IO.Path.Combine(Application.dataPath, "..", path);
    if (System.IO.File.Exists(full))
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(System.IO.File.ReadAllBytes(full));
        tex.filterMode = FilterMode.Point;
        return tex;
    }
    var fb = new Texture2D(1, 1);
    fb.SetPixel(0, 0, fallbackColor);
    fb.Apply();
    return fb;
}
```

- [ ] **Step 2: 改数量文本字号 + 黑底白字**

修改 `HotbarUI.cs:78-81`：

```csharp
if (stack.Count > 1)
{
    var style = new GUIStyle(GUI.skin.label);
    style.fontSize = 16;
    style.fontStyle = FontStyle.Bold;
    style.normal.textColor = Color.white;
    // 黑底
    var bgRect = new Rect(rect.x + SlotSize - 22, rect.y + SlotSize - 20, 20, 18);
    GUI.DrawTexture(bgRect, _selectBorder);  // 复用 selected 白边当白色背景
    GUI.Label(new Rect(rect.x + SlotSize - 20, rect.y + SlotSize - 19, 18, 16),
        stack.Count.ToString(), style);
}
```

- [ ] **Step 3: 默认预填 dirt**

修改 `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:79-82`，在 `_playerContext.Inventory.SetMaxStackLookup(...)` 之后加：

```csharp
// 默认热键栏第 0 格预填 64 dirt：让首次进游戏 hotbar 不空，能立刻看到挖到的方块。
if (_playerContext.Items.TryGetById("dirt", out var dirtDef))
{
    _playerContext.Inventory.SetSlot(0, new ItemStack(dirtDef.NumericId, 64));
}
```

> 这一行要 `using MyWorld.Core.Items;`（WorldBootstrap 顶部已经引用了）。

- [ ] **Step 4: 跑测试 + 跑流水线 + 拍 hotbar 截图**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln
./tools/scripts/build-and-run.sh --skip-tests --clean --no-launch
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath . -quit \
  -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll \
  -logFile Builds/logs/screenshot.log
```

人工 review `Builds/screenshots/overworld.png`：底部 hotbar 第 0 格是 dirt 图标 + "64"，其他格是空槽。

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/UI/HotbarUI.cs Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs
git commit -m "ui: Hotbar 用真贴图 + 默认预填 64 dirt + 数量字号 16"
```

---

## Task C2: SelectionBox 改亮色 + 不参与光照

**Files:**
- Modify: `Assets/Scripts/Unity/Player/SelectionBox.cs:38-52`

**Interfaces:**
- 主线颜色：白色 #FFFFFF，alpha=1
- 不参与光照（`renderer.shadowCastingMode = Off` 已有，加 `renderer.receiveShadows = false`）

- [ ] **Step 1: 改材质颜色**

修改 `Assets/Scripts/Unity/Player/BlockInteraction.cs:38-43`：

```csharp
if (selectionMaterial == null)
{
    var shader = Shader.Find("Universal Render Pipeline/Unlit");
    if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
    selectionMaterial = new Material(shader) { color = new Color(1f, 1f, 1f, 1f) };
}
```

> URP/Unlit 不受场景光照影响，selected 方块轮廓永远是亮白。

- [ ] **Step 2: 跑测试 + 流水线 + 截图**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln
./tools/scripts/build-and-run.sh --skip-tests --clean --no-launch
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath . -quit \
  -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll \
  -logFile Builds/logs/screenshot.log
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Unity/Player/BlockInteraction.cs
git commit -m "render: SelectionBox 走 URP/Unlit 亮白色，不再受场景光照影响"
```

---

## Task C3: HandController 挥动更紧凑

**Files:**
- Modify: `Assets/Scripts/Unity/Player/HandController.cs:13-15, 73-77`

**Interfaces:**
- `SwingDuration` 从 0.3 → 0.25
- `SwingDownAngle` 从 -30 → -45（更明显的挥砍弧度）

- [ ] **Step 1: 改 swing 参数**

```csharp
public float SwingDuration = 0.25f;
public float SwingDownAngle = -45f;
```

- [ ] **Step 2: 跑流水线 + Commit**

```bash
cd C:\tjf\github\MyWordGame
./tools/scripts/build-and-run.sh --skip-tests --clean --no-launch
git add Assets/Scripts/Unity/Player/HandController.cs
git commit -m "ui: HandController 挥动时长 0.3 → 0.25，弧度 -30 → -45"
```

---

## Task E1: 视觉测试套件（合并任务）

**Files:**
- Create: `Assets/Tests/EditMode/Visual/VisualRegressionTests.cs`

**Interfaces:**
- `MainViewport_NotAllBlack` / `_NotAllMagenta` / `_HasGrassGreen` 走 `ScreenshotCapture` 产出的最新 PNG
- `BlockMaterials_HaveNoMagentaPixels` 走 `BlockMaterialLibrary.Load`（任务 A1 已有基础）

- [ ] **Step 1: 写测试**

新建 `Assets/Tests/EditMode/Visual/VisualRegressionTests.cs`：

```csharp
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class VisualRegressionTests
    {
        private static string LatestScreenshot(string name)
        {
            string dir = Path.Combine(Application.dataPath, "..", "Builds", "screenshots");
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, name + ".png")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
            return files.Length == 0 ? null : files[0];
        }

        private static Texture2D LoadTexture(string path)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        [Test]
        public void Overworld_NotAllBlack()
        {
            string latest = LatestScreenshot("overworld");
            if (latest == null) Assert.Ignore("无截图——先跑 ScreenshotCapture");
            var tex = LoadTexture(latest);
            try
            {
                var pixels = tex.GetPixels();
                float avg = pixels.Sum(c => (c.r + c.g + c.b) / 3f) / pixels.Length * 255f;
                Assert.That(avg, Is.GreaterThan(30f),
                    $"截图平均亮度 {avg:F1}（< 30）说明画面全黑");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Overworld_NotAllMagenta()
        {
            string latest = LatestScreenshot("overworld");
            if (latest == null) Assert.Ignore("无截图——先跑 ScreenshotCapture");
            var tex = LoadTexture(latest);
            try
            {
                var pixels = tex.GetPixels();
                int magenta = pixels.Count(c => c.r > 0.98f && c.g < 0.03f && c.b > 0.98f);
                float frac = (float)magenta / pixels.Length;
                Assert.That(frac, Is.LessThan(0.05f),
                    $"magenta 像素 {frac*100:F2}%（超 5%）");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Overworld_HasGrassGreen()
        {
            string latest = LatestScreenshot("overworld");
            if (latest == null) Assert.Ignore("无截图——先跑 ScreenshotCapture");
            var tex = LoadTexture(latest);
            try
            {
                var pixels = tex.GetPixels();
                int grass = pixels.Count(c =>
                    c.r >= 0.27f && c.r <= 0.51f &&
                    c.g >= 0.47f && c.g <= 0.78f &&
                    c.b >= 0.16f && c.b <= 0.36f);
                float frac = (float)grass / pixels.Length;
                Assert.That(frac, Is.GreaterThan(0.05f),
                    $"草绿像素 {frac*100:F2}%（不足 5%），地表可能没渲染");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void BlockMaterials_NoMagenta()
        {
            var registry = BlockRegistryLoader.Load();
            var lib = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);
            try
            {
                int bad = 0;
                for (int i = 0; i < lib.Count; i++)
                {
                    var m = lib.Get(i);
                    if (m == null) continue;
                    if (BlockMaterialLibrary.HasMagentaPixels(m)) bad++;
                }
                Assert.That(bad, Is.EqualTo(0),
                    $"{bad} 个方块材质仍含 magenta 像素（贴图未入库）");
            }
            finally { lib.Dispose(); }
        }
    }
}
```

- [ ] **Step 2: 跑测试**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~VisualRegressionTests"
```

期望：4/4 通过（前提：截图已落 + 任务 A 完成 + magenta 全无）。

- [ ] **Step 3: 跑全量**

```bash
export ProgramFiles_x86="/c/Program Files (x86)"
dotnet test tools/dotnet/MyWorld.Tools.sln
```

期望：≥ 328/328 通过（之前 315 + 任务 A1/A3/A4/D2/B/C + E1 新增 13+ 测试）。

- [ ] **Step 4: Commit**

```bash
git add Assets/Tests/EditMode/Visual/VisualRegressionTests.cs
git commit -m "test: VisualRegressionTests 像素采样（不全黑/不全粉/有草色/材质无 magenta）"
```

---

## Task E2: visual-smoke 脚本 + 接入 build-and-run

**Files:**
- Create: `tools/scripts/visual-smoke.sh`
- Modify: `tools/scripts/build-and-run.sh`（加 `--with-visual` flag）

**Interfaces:**
- `visual-smoke.sh`：跑 ScreenshotCapture + dotnet test --filter Visual + 输出 PNG 路径
- build-and-run `--with-visual`：在 Build 之后跑 visual-smoke

- [ ] **Step 1: 写 visual-smoke.sh**

新建 `tools/scripts/visual-smoke.sh`：

```bash
#!/usr/bin/env bash
# 视觉冒烟：截图 + 像素采样测试 + 人工 review 路径打印。
set -euo pipefail

UNITY_EXE='C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe'
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

mkdir -p Builds/logs Builds/screenshots

echo "==> 拍截图"
"$UNITY_EXE" -batchmode -nographics -projectPath . -quit \
    -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll \
    -logFile Builds/logs/visual-smoke.log

echo "==> 跑视觉测试"
export ProgramFiles_x86='C:\Program Files (x86)'
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~Visual" --nologo

echo "==> 截图位置："
ls -la Builds/screenshots/*.png
```

chmod +x：

```bash
chmod +x tools/scripts/visual-smoke.sh
```

- [ ] **Step 2: 接入 build-and-run.sh**

修改 `tools/scripts/build-and-run.sh`，在 case 里加：

```bash
--with-visual) WITH_VISUAL=1; shift;;
```

并把 `WITH_VISUAL=0` 加到默认参数区。在 launch 之后加：

```bash
if [[ $WITH_VISUAL -eq 1 ]]; then
    step "STEP 5/5: 视觉冒烟（截图 + 像素采样测试）"
    bash tools/scripts/visual-smoke.sh || fail "视觉冒烟失败（看 Builds/logs/visual-smoke.log）"
    ok "视觉冒烟通过"
fi
```

- [ ] **Step 3: 跑一次**

```bash
cd C:\tjf\github\MyWordGame
export ProgramFiles_x86="/c/Program Files (x86)" && export APPDATA="${APPDATA:-C:\Users\tjf\AppData\Roaming}" && export LOCALAPPDATA="${LOCALAPPDATA:-C:\Users\tjf\AppData\Local}" && export DOTNET_ROOT='C:\Program Files\dotnet'
./tools/scripts/build-and-run.sh --skip-tests --with-visual --no-launch
```

期望：截图落盘 + 视觉测试过。

- [ ] **Step 4: Commit**

```bash
git add tools/scripts/visual-smoke.sh tools/scripts/build-and-run.sh
git commit -m "ci: visual-smoke 脚本 + build-and-run --with-visual"
```

---

## Self-Review

1. **Spec 覆盖**：
   - A 贴图 → Task A2/A3/A4 ✓
   - B 玩家身体 → B1/B2/B3 ✓
   - C UI → C1/C2/C3 ✓
   - D 截图诊断 → D1/D2 ✓
   - E 视觉测试 → E1/E2 ✓
2. **Placeholder**：扫了 0 个 TODO/TBD。「TODO（不在本任务范围）」两处都是有意标注的延后工作。
3. **类型一致**：
   - `PlayerVisual.WalkPhase` 在 B1 定义为 `float`，B2 用 `+= speed * dt * 8f` 累加 ✓
   - `BlockMaterialLibrary.HasMagentaPixels` 在 A1 定义为 `public static bool`，A4/E1 都按此签名调用 ✓
   - `ScreenshotCapture.Capture(string, int, int)` + `CaptureAll(string)` + `CaptureThirdPerson(string, int, int)` 签名一致 ✓
4. **依赖顺序**：A1 → A2 → D2 → B1 → B2 → B3 → C1 → C2 → C3 → E1 → E2（A 必须先完成，否则 magenta 占比测试会一直失败）

