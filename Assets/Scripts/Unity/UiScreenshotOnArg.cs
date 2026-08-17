using System.IO;
using MyWorld.Unity.UI;
using UnityEngine;

namespace MyWorld.Unity
{
    /// <summary>
    /// m6 B1：UI 截图验证组件。IMGUI 不进 Camera.Render（visual-smoke 的老盲区，
    /// <c>HotbarUI</c>/<c>CraftingInventoryUi</c> 等 OnGUI 内容拍不到），
    /// 但 <see cref="ScreenCapture.CaptureScreenshot"/> 抓完整 backbuffer，含 IMGUI。
    /// <para>
    /// standalone 跑 <c>MyWordGame.exe --ui-shot -screen-width 1280 -screen-height 720</c>：
    /// 自动截 主菜单 / hotbar / 背包 / 工作台 / 暂停菜单 / 帮助菜单 六张 PNG 到
    /// <c>Builds/screenshots/</c>，写 <c>ui.done</c> 哨兵文件后退出。由
    /// <see cref="Bootstrap.WorldBootstrap"/> 在 Awake 末尾按启动参数条件挂载——
    /// 无参数时零开销（启动读一次 args 即返回）。
    /// </para>
    /// <para>
    /// m6 终审修 M5：ui-help.png 让帮助菜单的视觉项（按键表双栏 + 进度区）也进
    /// 机器侧验证，不再只靠父子实机（m6 时是第 4 张，m8 B3 插入暂停菜单后退居第 5）。
    /// </para>
    /// <para>
    /// m8 B3：工作台之后加拍第 4 张 ui-pause.png（Esc 暂停菜单，居中三按钮面板）——
    /// 经 <see cref="UI.PauseMenuUi.SetOpen(bool)"> 与 Esc 按键同一条路径打开，
    /// 顺带把「timeScale=0 下 IMGUI 照常绘制 / 截图流程不被真暂停卡住」也验了。
    /// </para>
    /// <para>
    /// av W2-12：第 0 张新增 ui-title.png（主菜单视频背景 + 开始 / 退出按钮），
    /// 第 1 张才是原 hotbar；原 5 张顺延为 2–6，总 6 张。
    /// </para>
    /// </summary>
    public sealed class UiScreenshotOnArg : MonoBehaviour
    {
        /// <summary>截图前等待的稳定帧数：让流式加载把出生点周围区块建好、IMGUI 首帧布局完成。</summary>
        private const int WarmupFrames = 60;

        /// <summary>每次截图后等待的帧数：CaptureScreenshot 异步编码落盘，立刻拍下一张会拿到旧画面。</summary>
        private const int CaptureSettleFrames = 20;

        /// <summary>等待最后一张 PNG 落盘的帧数上限（兜底，超时也照常写 done 退出）。</summary>
        private const int FlushTimeoutFrames = 300;

        private int _frames;
        private int _phase;

        /// <summary>
        /// 纯函数：命令行参数里是否带 <c>--ui-shot</c>（精确匹配）。null 安全。
        /// 抽成静态便于 EditMode 直接测，不依赖真实进程启动参数。
        /// </summary>
        public static bool ShouldCapture(string[] args) =>
            args != null && System.Array.IndexOf(args, "--ui-shot") >= 0;

        private void Update()
        {
            _frames++;
            switch (_phase)
            {
                case 0: // av W2-12：等 60 帧稳定后先截主菜单（mp4 背景 + 开始 / 退出按钮）
                    if (_frames >= WarmupFrames)
                    {
                        Capture("ui-title.png");
                        NextPhase();
                    }
                    break;

                case 1: // 关主菜单（恢复输入锁 / 指针），截 hotbar
                    if (_frames >= CaptureSettleFrames)
                    {
                        FindTitleScreenUi()?.StartGame();
                        Capture("ui-hotbar.png");
                        NextPhase();
                    }
                    break;

                case 2: // 等 PNG 落盘，再开背包截图
                    if (_frames >= CaptureSettleFrames)
                    {
                        FindInventoryUi()?.SetOpen(true);
                        Capture("ui-inventory.png");
                        NextPhase();
                    }
                    break;

                case 3: // 关背包、开工作台，截图
                    if (_frames >= CaptureSettleFrames)
                    {
                        var inv = FindInventoryUi();
                        if (inv != null) inv.SetOpen(false);
                        FindWorkbenchUi()?.SetOpen(true);
                        Capture("ui-workbench.png");
                        NextPhase();
                    }
                    break;

                case 4: // 关工作台、开暂停菜单（m8 B3 第 5 张），截图
                    if (_frames >= CaptureSettleFrames)
                    {
                        var wb = FindWorkbenchUi();
                        if (wb != null) wb.SetOpen(false);
                        FindPauseMenuUi()?.SetOpen(true); // 与 Esc 同路：timeScale=0 真暂停
                        Capture("ui-pause.png");
                        NextPhase();
                    }
                    break;

                case 5: // 关暂停（恢复 timeScale=1）、开帮助菜单（m6 终审修 M5），截图
                    if (_frames >= CaptureSettleFrames)
                    {
                        var pause = FindPauseMenuUi();
                        if (pause != null) pause.SetOpen(false);
                        FindHelpMenuUi()?.SetOpen(true);
                        Capture("ui-help.png");
                        NextPhase();
                    }
                    break;

                case 6: // 等最后一张 PNG 落盘 → 写哨兵 → 退出
                    if (_frames >= CaptureSettleFrames
                        && (File.Exists(OutputPath("ui-help.png")) || _frames >= FlushTimeoutFrames))
                    {
                        File.WriteAllText(OutputPath("ui.done"), string.Empty);
                        Debug.Log("[UiScreenshotOnArg] 六张 UI 截图完成，退出。");
                        Application.Quit();
                        _phase = 7;
                    }
                    break;
            }
        }

        private void NextPhase()
        {
            _phase++;
            _frames = 0;
        }

        private static CraftingInventoryUi FindInventoryUi() =>
            FindObjectOfType<CraftingInventoryUi>();

        private static CraftingWorkbenchUi FindWorkbenchUi() =>
            FindObjectOfType<CraftingWorkbenchUi>();

        private static HelpMenuUi FindHelpMenuUi() =>
            FindObjectOfType<HelpMenuUi>();

        private static PauseMenuUi FindPauseMenuUi() =>
            FindObjectOfType<PauseMenuUi>();

        // av W2-12：主菜单（开局遮罩）——开始游戏时销毁遮罩解锁输入。
        private static TitleScreenUi FindTitleScreenUi() =>
            FindObjectOfType<TitleScreenUi>();

        private void Capture(string fileName)
        {
            string path = OutputPath(fileName);
            Directory.CreateDirectory(OutputDirectory());
            // 截图前先删旧文件（B1 评审 Minor）：Phase 3 靠 File.Exists 确认最后一张落盘，
            // 若目录里留有上一次运行的同名残留 PNG，会在新截图编码完成前误判提前退出
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[UiScreenshotOnArg] CaptureScreenshot -> {path}");
        }

        /// <summary>
        /// 截图输出目录 <c>Builds/screenshots</c>：
        /// standalone 的 exe 在 <c>Builds/Windows/</c>，<see cref="Application.dataPath"/> 是
        /// <c>Builds/Windows/MyWordGame_Data</c>，上跳两级正好是 <c>Builds/</c>；
        /// 编辑器下 dataPath 是 <c>&lt;repo&gt;/Assets</c>，上跳一级再进 Builds。
        /// 路径推导集中在这一个方法里，便于推断验证。
        /// </summary>
        public static string OutputDirectory()
        {
            string dataPath = Application.dataPath;
            return Application.isEditor
                ? Path.GetFullPath(Path.Combine(dataPath, "..", "Builds", "screenshots"))
                : Path.GetFullPath(Path.Combine(dataPath, "..", "..", "screenshots"));
        }

        private static string OutputPath(string fileName) =>
            Path.Combine(OutputDirectory(), fileName);
    }
}
