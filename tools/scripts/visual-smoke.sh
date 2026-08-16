#!/usr/bin/env bash
# 视觉冒烟：拍截图 + 跑 Unity EditMode 视觉回归测试。
#
# 设计要点（与 brief 一致）：
#   1. 不带 -nographics —— D1 已证本环境（RTX 2070 Super）下 -nographics 让 renderer 变 Null Device
#   2. 用 CaptureAllDefault 0-arg 重载 —— CaptureAll(string) 有 1 个必填参数，
#      Unity -executeMethod 不支持带参方法（D1 已加 0-arg 重载）
#   3. 视觉测试用 Unity EditMode 跑 —— 测试文件整段 #if UNITY_EDITOR 包裹，
#      dotnet 链编译时被排除，dotnet test --filter 命中空集
#   4. 加 XML 结果解析 —— 保证 EditMode 测试真跑了 + 全 pass 才 exit 0

set -euo pipefail

# --- 路径常量（与 build-and-run.sh 一致） ---
UNITY_EXE='C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe'
SOLUTION='tools/dotnet/MyWorld.Tools.sln'

LOG_SCREENSHOT='Builds/logs/visual-smoke-screenshot.log'
LOG_EDITMODE='Builds/logs/visual-smoke-editmode.log'
LOG_EDITMODE_XML='Builds/logs/visual-smoke-results.xml'

# --- 切到仓库根 ---
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

# --- Unity 批处理所需环境变量（Git Bash 下不 export 就报 path1 null） ---
export ProgramFiles='C:\Program Files'
export ProgramFiles_x86='C:\Program Files (x86)'
export ProgramW6432='C:\Program Files'
export APPDATA="${APPDATA:-C:\Users\tjf\AppData\Roaming}"
export LOCALAPPDATA="${LOCALAPPDATA:-C:\Users\tjf\AppData\Local}"
export DOTNET_ROOT='C:\Program Files\dotnet'

# --- 输出辅助（与 build-and-run.sh 同款，便于串到父脚本日志里读） ---
BOLD='\033[1m'; CYAN='\033[1;36m'; GREEN='\033[1;32m'; RED='\033[1;31m'; OFF='\033[0m'
step() { printf "${BOLD}${CYAN}==> %s${OFF}\n" "$1"; }
ok()   { printf "${BOLD}${GREEN}✓ %s${OFF}\n" "$1"; }
fail() { printf "${BOLD}${RED}✗ %s${OFF}\n" "$1" >&2; exit 1; }

# --- 预检 ---
step "预检：检查 Unity 是否空闲"
if command -v tasklist >/dev/null 2>&1 && tasklist 2>/dev/null | grep -qi Unity.exe; then
    fail "另一个 Unity.exe 正在运行（先关掉再跑，CLAUDE.md 警告）"
fi
ok "无 Unity 实例占用"

if [[ ! -f "$UNITY_EXE" ]]; then
    fail "找不到 Unity.exe: $UNITY_EXE"
fi
if [[ ! -f "$SOLUTION" ]]; then
    fail "找不到解决方案: $SOLUTION（确认在仓库根目录运行）"
fi

mkdir -p Builds/logs Builds/screenshots

# --- 1. 拍截图 ---
step "拍截图（CaptureAllDefault 0-arg，无 -nographics）"
# 关键 1：不带 -nographics（RTX 2070 Super 下会变 Null Device 拍空图，D1 fix 已证）
# 关键 2：用 CaptureAllDefault 0-arg 重载（CaptureAll(string) 有 1 必填参数，
#        Unity -executeMethod 不支持带参方法，D1 已加 CaptureAllDefault 0-arg 重载）
"$UNITY_EXE" -batchmode -projectPath . -quit \
    -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAllDefault \
    -logFile "$LOG_SCREENSHOT"

# 校验截图是否落地（overworld.png + third-person.png 由 CaptureAll 生成）
if [[ ! -f Builds/screenshots/overworld.png ]] || [[ ! -f Builds/screenshots/third-person.png ]]; then
    fail "截图未落地（看 $LOG_SCREENSHOT，期望 Builds/screenshots/overworld.png + third-person.png）"
fi
ok "截图已落地 Builds/screenshots/"

# --- 1b. 按生物群系截图（F3 / spec C6）---
step "按生物群系截图（CaptureBiomesDefault）"
LOG_BIOME_SHOT='Builds/logs/visual-smoke-biome-screenshot.log'
"$UNITY_EXE" -batchmode -projectPath . -quit \
    -executeMethod MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureBiomesDefault \
    -logFile "$LOG_BIOME_SHOT"

BIOME_COUNT=0
for b in Plains Desert Forest Mountains Snow; do
    if [[ -f "Builds/screenshots/biome-$b.png" ]]; then
        BIOME_COUNT=$((BIOME_COUNT+1))
    fi
done
if [[ "$BIOME_COUNT" -lt 5 ]]; then
    fail "群系截图不齐（$BIOME_COUNT/5，看 $LOG_BIOME_SHOT，期望 biome-{Plains,Desert,Forest,Mountains,Snow}.png）"
fi
ok "5 张群系截图已落地 Builds/screenshots/"

# --- 1c. UI 截图（--ui-shot standalone，可拍 IMGUI）---
# Camera.Render 拍不到 OnGUI 内容（IMGUI 事件驱动、不进相机管线，B1 已证），
# UI 截图必须走 standalone 的 ScreenCapture.CaptureScreenshot（抓完整 backbuffer 含 IMGUI）。
# 无 build 产物时优雅跳过——本步骤只在完整流水线（build-and-run.sh → 本脚本）后生效。
LOG_UI_SHOT='Builds/logs/visual-smoke-ui-shot.log'
if [[ -f Builds/Windows/MyWordGame.exe ]]; then
    step "UI 截图（--ui-shot standalone，可拍 IMGUI）"
    # 先清旧哨兵：下面的轮询靠 ui.done 判断完成，上一次运行的残留会让循环立即误判
    rm -f Builds/screenshots/ui.done
    # 分辨率参数只是意图声明：本 build 的 Fullscreen Window 模式按原生分辨率跑，
    # B2 实测截出的是 1920×1080（1280×720 不生效）。像素断言
    # （UiScreenshotPixelTests）的坐标按截图实际尺寸换算，分辨率无关，不受此影响
    ./Builds/Windows/MyWordGame.exe -screen-width 1280 -screen-height 720 \
        -screen-fullscreen 0 --ui-shot > "$LOG_UI_SHOT" 2>&1 &
    UI_SHOT_PID=$!
    # 轮询 ui.done（最多 60s；后台启动，超时杀进程而不是干等挂死）
    UI_DONE=0
    for i in $(seq 1 60); do
        if [[ -f Builds/screenshots/ui.done ]]; then UI_DONE=1; break; fi
        sleep 1
    done
    if [[ "$UI_DONE" != "1" ]]; then
        kill "$UI_SHOT_PID" 2>/dev/null || true
        fail "60s 内未见 ui.done（看 $LOG_UI_SHOT）"
    fi
    # exe 写完 ui.done 后会自行 Application.Quit；等它退干净再校验 PNG，宽限 10s 后兜底杀
    for i in $(seq 1 10); do
        if ! kill -0 "$UI_SHOT_PID" 2>/dev/null; then break; fi
        sleep 1
    done
    kill "$UI_SHOT_PID" 2>/dev/null || true
    for f in ui-hotbar.png ui-inventory.png ui-workbench.png ui-pause.png ui-help.png; do
        if [[ ! -f "Builds/screenshots/$f" ]]; then
            fail "UI 截图缺失: $f（看 $LOG_UI_SHOT）"
        fi
    done
    ok "5 张 UI 截图落地"
else
    step "跳过 UI 截图（无 build 产物——完整流水线 build-and-run.sh 里生效）"
fi

# --- 2. 跑 Unity EditMode 视觉回归测试 ---
step "跑 Unity EditMode 视觉回归测试（VisualRegressionTests + UiScreenshotPixelTests）"
# 视觉测试都在 #if UNITY_EDITOR 包裹里，dotnet 链会跳过滤掉；
# 必须用 Unity EditMode 跑（E1 已验证 VisualRegressionTests 4/4 全 pass）。
# Unity 2022 -testFilter 直接接类名（class 或 class.method，分号分隔多个），
# 不接 FullyQualifiedName~ 前缀。
# UiScreenshotPixelTests 读上面 1c 刚产出的 ui-*.png 做像素断言（产物缺失时 Ignore 不算失败）
"$UNITY_EXE" -batchmode -projectPath . \
    -runTests -testPlatform EditMode \
    -testFilter 'VisualRegressionTests;UiScreenshotPixelTests' \
    -testResults "$LOG_EDITMODE_XML" \
    -logFile "$LOG_EDITMODE" \
    || true   # Unity 批处理退出码不可信，必须看 XML

# --- 3. 校验 XML ---
if [[ ! -f "$LOG_EDITMODE_XML" ]]; then
    fail "EditMode 测试结果 XML 未生成（看 $LOG_EDITMODE）"
fi

# 提取 total / passed / failed 计数（XML 格式：test-run ... result="Passed" total="N" passed="M" failed="K"）
# 不用 grep -P（Git Bash 老版 MSYS 不支持 -P 也不在 UTF-8 locale），改用 sed
TOTAL=$(sed -n 's/.*test-run[^>]*total="\([0-9]\+\)".*/\1/p' "$LOG_EDITMODE_XML" | head -1)
TOTAL=${TOTAL:-0}
PASSED=$(sed -n 's/.*test-run[^>]*passed="\([0-9]\+\)".*/\1/p' "$LOG_EDITMODE_XML" | head -1)
PASSED=${PASSED:-0}
FAILED=$(sed -n 's/.*test-run[^>]*failed="\([0-9]\+\)".*/\1/p' "$LOG_EDITMODE_XML" | head -1)
FAILED=${FAILED:-0}
step "EditMode 视觉测试: total=$TOTAL passed=$PASSED failed=$FAILED"

if [[ "$FAILED" != "0" || "$TOTAL" == "0" ]]; then
    fail "视觉测试未通过（total=$TOTAL passed=$PASSED failed=$FAILED，看 $LOG_EDITMODE）"
fi
ok "EditMode 视觉测试 $PASSED/$TOTAL"

# --- 4. 输出 PNG 位置便于人工 review ---
step "截图位置（人工 review 用）："
ls -la Builds/screenshots/*.png

ok "视觉冒烟通过"