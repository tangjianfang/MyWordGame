using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 建立 URP 管线资产并挂到项目设置上。
    /// <para>
    /// 手工路径是「右键 Create → Rendering → URP Asset」再去 Project Settings 里指两处，
    /// 但那两处设置存在 <c>ProjectSettings/*.asset</c> 里，手写 YAML 不现实，
    /// 批处理也点不了右键菜单——所以整套流程固化成这段脚本，人和 CI 走同一条路径。
    /// </para>
    /// </summary>
    public static class UrpSetup
    {
        private const string SettingsDirectory = "Assets/Settings";
        private const string PipelineAssetPath = SettingsDirectory + "/UniversalRenderPipelineAsset.asset";
        private const string RendererAssetPath = SettingsDirectory + "/UniversalRenderPipelineAsset_Renderer.asset";

        [MenuItem("MyWorld/接入 URP 管线")]
        public static void Apply()
        {
            UniversalRenderPipelineAsset pipeline = LoadOrCreatePipelineAsset();

            GraphicsSettings.defaultRenderPipeline = pipeline;

            // 每个质量档位都要单独指一遍：档位上留空表示「用内置管线」，
            // 只改 Graphics 的话，一切换质量档画面就掉回内置管线
            int previousLevel = QualitySettings.GetQualityLevel();
            for (var level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = pipeline;
            }

            QualitySettings.SetQualityLevel(previousLevel, applyExpensiveChanges: false);

            SaveProjectSettings();

            Debug.Log($"URP 已接入：{PipelineAssetPath}，覆盖 {QualitySettings.names.Length} 个质量档位。");
        }

        private static UniversalRenderPipelineAsset LoadOrCreatePipelineAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(SettingsDirectory);
            AssetDatabase.Refresh();

            // 渲染器数据必须先落盘成独立资产：管线资产只存对它的引用，
            // 留在内存里的话保存之后引用就成了空，运行时直接没有渲染器
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
            AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
            ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);

            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            AssetDatabase.SaveAssets();

            return pipeline;
        }

        /// <summary>
        /// <c>ProjectSettings/</c> 下的设置不是普通资产，改完不标脏就不会写盘，
        /// 批处理退出后改动会静默丢失。
        /// </summary>
        private static void SaveProjectSettings()
        {
            MarkDirty("ProjectSettings/GraphicsSettings.asset");
            MarkDirty("ProjectSettings/QualitySettings.asset");
            AssetDatabase.SaveAssets();
        }

        private static void MarkDirty(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
