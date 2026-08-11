using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 把 <c>Assets/Scenes/Preview.unity</c> 加进 Scenes In Build。
    /// <para>
    /// 缺失时先调 <see cref="PreviewSceneBuilder.Build"/> 重建场景，再覆盖写入
    /// <see cref="EditorBuildSettings.scenes"/>。整个方法是幂等的：跑一次和跑 N 次
    /// 结果一致（单场景数组，不去重不留旧）。Standalone 构建前置必跑——本项目里
    /// 这条信息写在 <c>ProjectSettings/EditorBuildSettings.asset</c> 里，CLAUDE.md 强调
    /// ProjectSettings YAML 不要手改，由本方法程序化改写。
    /// </para>
    /// </summary>
    public static class BuildSetup
    {
        private const string ScenePath = "Assets/Scenes/Preview.unity";

        [MenuItem("MyWorld/Build 准备: 把 Preview.unity 加入 Scenes In Build")]
        public static void Apply()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.Log($"[BuildSetup] 场景缺失 ({ScenePath})，先调 PreviewSceneBuilder.Build 重建");
                PreviewSceneBuilder.Build();
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[BuildSetup] Scenes In Build 已设置: {ScenePath} (enabled)");
        }
    }
}