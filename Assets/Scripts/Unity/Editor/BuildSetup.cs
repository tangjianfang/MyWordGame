using UnityEditor;
using UnityEngine;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 把 <c>Assets/Scenes/Preview.unity</c> 加进 Scenes In Build。
    /// <para>
    /// 总是先调 <see cref="PreviewSceneBuilder.Build"/> 重建场景（耗时 1 秒左右，
    /// 场景只有 3 个 GameObject），再覆盖写入 <see cref="EditorBuildSettings.scenes"/>。
    /// 之所以「总是重建」而不是「缺失才建」：旧版场景可能挂着已删脚本的 GUID 引用
    /// （里程碑 1→2 之间 <c>FreeFlyCamera</c> 被退役但场景里 MonoBehaviour 引用没清），
    /// Build 出来的 .exe 启动会刷「The referenced script on this Behaviour is missing」
    /// 然后 <c>WorldBootstrap.Awake</c> 路径出问题，画面只剩 skybox。重建保证场景
    /// 与当前代码一致。整个方法是幂等的：跑一次和跑 N 次结果一致。
    /// </para>
    /// <para>
    /// Standalone 构建前置必跑——本项目里这条信息写在
    /// <c>ProjectSettings/EditorBuildSettings.asset</c> 里，CLAUDE.md 强调
    /// ProjectSettings YAML 不要手改，由本方法程序化改写。
    /// </para>
    /// </summary>
    public static class BuildSetup
    {
        private const string ScenePath = "Assets/Scenes/Preview.unity";

        [MenuItem("MyWorld/Build 准备: 把 Preview.unity 加入 Scenes In Build")]
        public static void Apply()
        {
            PreviewSceneBuilder.Build();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[BuildSetup] Scenes In Build 已设置: {ScenePath} (enabled)");
        }
    }
}