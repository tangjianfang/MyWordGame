using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 出 Windows Standalone .exe 到 <c>Builds/Windows/MyWordGame.exe</c>。
    /// <para>
    /// 走的是 <see cref="BuildPipeline.BuildPlayer(BuildPlayerOptions)"/>——Unity
    /// 批处理唯一正经出 .exe 的入口。CLI 端靠 <see cref="EditorApplication.Exit"/>
    /// 把成功/失败码抛给父脚本（CLAUDE.md 警告 Unity 批处理崩溃时退出码不可信，
    /// 这里我们显式调 Exit + 父脚本 grep "Build OK:" 双保险）。
    /// </para>
    /// <para>
    /// 必须先跑 <see cref="BuildSetup.Apply"/>：它把 <c>Preview.unity</c> 加进
    /// <see cref="EditorBuildSettings.scenes"/>——本方法直接读这个数组，不在内部
    /// 重复逻辑。<c>BuildPlayerOptions.scenes</c> 是 <c>string[]</c>（路径），
    /// 而 <c>EditorBuildSettings.scenes</c> 是 <c>EditorBuildSettingsScene[]</c>，
    /// 这里用 <see cref="System.Array.ConvertAll{TInput,TOutput}(TInput[], System.Converter{TInput,TOutput})"/>
    /// 抽 <c>.path</c>。
    /// </para>
    /// </summary>
    public static class BuildPlayer
    {
        private const string OutputDirectory = "Builds/Windows";
        private const string ExecutableName = "MyWordGame.exe";

        [MenuItem("MyWorld/Build Windows")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputDirectory);

            string[] scenePaths = System.Array.ConvertAll(
                EditorBuildSettings.scenes,
                scene => scene.path);

            var options = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = Path.Combine(OutputDirectory, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"[BuildPlayer] Build 失败: result={summary.result}, " +
                    $"errors={summary.totalErrors}, warnings={summary.totalWarnings}, " +
                    $"output={summary.outputPath}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log(
                $"[BuildPlayer] Build OK: {summary.outputPath} " +
                $"({summary.totalSize} bytes, {summary.totalTime})");
            EditorApplication.Exit(0);
        }
    }
}