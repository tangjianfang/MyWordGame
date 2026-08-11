#!/usr/bin/env bash
# 一键流水线：跑全测试 → 接 URP → 注册场景 → Build Windows Standalone → 启动游戏。
#
# 默认从仓库根目录运行（脚本会 cd 到 git top-level）。
#
# 选项：
#   --skip-tests   跳过 dotnet test + Unity EditMode（已跑过想省时）
#   --skip-build   跳过 URP setup + BuildSetup + BuildPlayer + 启动（只跑测试）
#   --no-launch    build 完不启动 .exe
#   --clean        build 前 rm -rf Builds/

set -euo pipefail

# --- 默认参数 ---
SKIP_TESTS=0
SKIP_BUILD=0
NO_LAUNCH=0
CLEAN=0

# --- 路径常量（与 CLAUDE.md 一致） ---
UNITY_EXE='C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe'
# 在 bash 调用层要换成正斜杠形式（cmd 不需要，但 bash 用 MSYS2 路径解析）
UNITY_EXE_BASH='C:/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe'
DOTNET_ROOT_WIN='C:\Program Files\dotnet'
SOLUTION='tools/dotnet/MyWorld.Tools.sln'

LOG_DOTNET='harness.log'           # 与 CLAUDE.md 同名
LOG_EDITMODE='unity-tests.log'
LOG_EDITMODE_XML='unity-test-results.xml'
LOG_URP='urp-setup.log'             # 与现有约定一致
LOG_BUILDSETUP='unity-buildsetup.log'
LOG_BUILD='unity-build.log'

# --- 参数解析 ---
while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-tests) SKIP_TESTS=1; shift;;
        --skip-build) SKIP_BUILD=1; shift;;
        --no-launch)  NO_LAUNCH=1; shift;;
        --clean)      CLEAN=1; shift;;
        -h|--help)
            sed -n '2,15p' "$0"; exit 0;;
        *) echo "未知参数: $1（试试 --help）" >&2; exit 2;;
    esac
done

# --- 切到仓库根 ---
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

# --- Unity 批处理所需环境变量（Git Bash 下不 export 就报 path1 null） ---
export ProgramFiles='C:\Program Files'
export ProgramFiles_x86='C:\Program Files (x86)'
export ProgramW6432='C:\Program Files'
export APPDATA="${APPDATA:-C:\Users\tjf\AppData\Roaming}"
export LOCALAPPDATA="${LOCALAPPDATA:-C:\Users\tjf\AppData\Local}"
export DOTNET_ROOT="$DOTNET_ROOT_WIN"

# --- 输出辅助 ---
BOLD='\033[1m'; CYAN='\033[1;36m'; GREEN='\033[1;32m'; RED='\033[1;31m'; OFF='\033[0m'
step() { printf "${BOLD}${CYAN}==> %s${OFF}\n" "$1"; }
ok()   { printf "${BOLD}${GREEN}✓ %s${OFF}\n" "$1"; }
fail() { printf "${BOLD}${RED}✗ %s${OFF}\n" "$1" >&2; exit 1; }

# --- 0. 预检 ---
step "预检：检查 Unity 是否空闲"
if command -v tasklist >/dev/null 2>&1 && tasklist 2>/dev/null | grep -qi Unity.exe; then
    fail "另一个 Unity.exe 正在运行（先关掉再跑，CLAUDE.md 警告)"
fi
ok "无 Unity 实例占用"

if [[ ! -f "$UNITY_EXE_BASH" ]]; then
    fail "找不到 Unity.exe: $UNITY_EXE_BASH"
fi
if [[ ! -f "$SOLUTION" ]]; then
    fail "找不到解决方案: $SOLUTION（确认在仓库根目录运行）"
fi

# --- 1. dotnet test ---
if [[ $SKIP_TESTS -eq 0 ]]; then
    step "STEP 1/4: dotnet test (Core + EditMode via dotnet)"
    dotnet build "$SOLUTION" --nologo -v quiet 2>&1 | tee "$LOG_DOTNET.build.tmp" \
        || fail "dotnet build 失败（看 $LOG_DOTNET.build.tmp）"
    rm -f "$LOG_DOTNET.build.tmp"
    dotnet test "$SOLUTION" --nologo --no-build 2>&1 | tee "$LOG_DOTNET" \
        || fail "dotnet test 失败（看 $LOG_DOTNET）"
    if ! grep -q '通过!.*通过:.*214' "$LOG_DOTNET"; then
        fail "dotnet test 未报 214/214（看 $LOG_DOTNET）"
    fi
    ok "dotnet test 214/214"
fi

# --- 2. Unity EditMode ---
if [[ $SKIP_TESTS -eq 0 ]]; then
    step "STEP 2/4: Unity EditMode 测试"
    "$UNITY_EXE_BASH" \
        -batchmode -nographics -projectPath . \
        -runTests -testPlatform EditMode \
        -testResults "$LOG_EDITMODE_XML" -logFile "$LOG_EDITMODE" \
        || true   # 退出码不可信，依赖结果文件
    if [[ ! -f "$LOG_EDITMODE_XML" ]]; then
        fail "EditMode 结果文件未生成（看 $LOG_EDITMODE）"
    fi
    if ! grep -q 'result="Passed" total="214" passed="214" failed="0"' "$LOG_EDITMODE_XML"; then
        fail "EditMode 测试不全过（看 $LOG_EDITMODE_XML 与 $LOG_EDITMODE）"
    fi
    ok "Unity EditMode 214/214"
fi

# --- 3. URP 接入（幂等） ---
step "STEP 3/4: URP 管线接入（幂等）"
"$UNITY_EXE_BASH" -batchmode -nographics -projectPath . -quit \
    -executeMethod MyWorld.Unity.EditorTools.UrpSetup.Apply \
    -logFile "$LOG_URP" || true
if ! grep -q 'URP 已接入' "$LOG_URP"; then
    fail "URP 接入失败（看 $LOG_URP）"
fi
ok "URP 已就绪"

# --- 4. Build Windows Standalone ---
if [[ $SKIP_BUILD -eq 0 ]]; then
    step "STEP 4/4: Windows Standalone 构建 + 启动"

    if [[ $CLEAN -eq 1 ]]; then
        printf "  rm -rf Builds/ "
        rm -rf Builds/
    fi

    # 4a. 注册场景
    printf "  [a] BuildSetup.Apply 注册 Preview.unity 到 Scenes In Build\n"
    "$UNITY_EXE_BASH" -batchmode -nographics -projectPath . -quit \
        -executeMethod MyWorld.Unity.EditorTools.BuildSetup.Apply \
        -logFile "$LOG_BUILDSETUP" || true
    if ! grep -q 'Scenes In Build 已设置' "$LOG_BUILDSETUP"; then
        fail "BuildSetup.Apply 失败（看 $LOG_BUILDSETUP）"
    fi

    # 4b. 出 .exe
    printf "  [b] BuildPlayer.Build 出 Builds/Windows/MyWordGame.exe\n"
    "$UNITY_EXE_BASH" -batchmode -nographics -projectPath . -quit \
        -executeMethod MyWorld.Unity.EditorTools.BuildPlayer.Build \
        -logFile "$LOG_BUILD" || true
    if ! grep -q 'Build OK:' "$LOG_BUILD"; then
        fail "BuildPlayer.Build 失败（看 $LOG_BUILD）"
    fi
    if [[ ! -f 'Builds/Windows/MyWordGame.exe' ]]; then
        fail ".exe 未落地（看 $LOG_BUILD）"
    fi
    ok "Build OK: Builds/Windows/MyWordGame.exe"

    # 4c. 启动
    if [[ $NO_LAUNCH -eq 0 ]]; then
        printf "  [c] 启动 .exe\n"
        # 完整重定向 stdio，让 MSYS bash 不等待 .exe 退出——否则父脚本会卡在 launch 步骤
        # 看到 EOF 都不结束。Defender 首次扫描可能 5-30 秒，所以 detach 必须彻底。
        ./Builds/Windows/MyWordGame.exe </dev/null >launch.log 2>&1 &
        LAUNCH_PID=$!
        disown "$LAUNCH_PID" 2>/dev/null || true
        sleep 2
        if tasklist 2>/dev/null | grep -qi 'MyWordGame.exe'; then
            ok "MyWordGame.exe 已在后台运行 (pid=$LAUNCH_PID)"
        else
            printf "  ${BOLD}⚠ 进程未检测到，tail launch.log 看 stderr${OFF}\n"
        fi
    fi
fi

step "DONE"
printf "  ${BOLD}日志：${OFF} %s / %s / %s / %s / %s\n" \
    "$LOG_DOTNET" "$LOG_EDITMODE" "$LOG_URP" "$LOG_BUILDSETUP" "$LOG_BUILD"
printf "  ${BOLD}产物：${OFF} Builds/Windows/MyWordGame.exe\n"