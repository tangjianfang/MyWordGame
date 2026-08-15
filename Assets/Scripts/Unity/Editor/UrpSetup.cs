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
    /// <para>
    /// 顺带在 <c>Assets/Resources/</c> 里建一个引用 URP/Lit 的哑材质（<c>BlockLitMaterial.mat</c>）。
    /// URP 包没被任何材质引用时，Build 会按「未引用资源」剔除 URP/Lit，运行时
    /// <c>Shader.Find("Universal Render Pipeline/Lit")</c> 返回 null，
    /// <see cref="MyWorld.Unity.Rendering.BlockMaterialLibrary.FindShader"/> 抛
    /// <c>InvalidOperationException</c>，<see cref="MyWorld.Unity.Bootstrap.WorldBootstrap.Awake"/>
    /// 失败，画面只剩 skybox。哑材质引用 URP/Lit 让 shader 进 build（且只编需要的变体
    /// ——alwaysIncludedShaders 会强制编全部 29 万变体，30 分钟跑不完；哑材质只编默认值
    /// 对应的几十种）。
    /// </para>
    /// </summary>
    public static class UrpSetup
    {
        private const string SettingsDirectory = "Assets/Settings";
        private const string PipelineAssetPath = SettingsDirectory + "/UniversalRenderPipelineAsset.asset";
        private const string RendererAssetPath = SettingsDirectory + "/UniversalRenderPipelineAsset_Renderer.asset";
        private const string UrpLitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";
        private const string BlockLitMaterialDirectory = "Assets/Resources";
        private const string BlockLitMaterialPath = BlockLitMaterialDirectory + "/BlockLitMaterial.mat";

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

            EnsureBlockLitMaterial();

            SaveProjectSettings();

            Debug.Log($"URP 已接入：{PipelineAssetPath}，覆盖 {QualitySettings.names.Length} 个质量档位。");
        }

        /// <summary>
        /// 把项目色彩空间切到 Linear（m5 B1）。
        /// <para>
        /// Gamma + URP 是「画面系统性偏暗」的根因：URP 官方只支持 Linear 色彩空间，
        /// Gamma 下光照计算在 gamma 域进行，输出再被显示器 gamma 压一次，整体暗且不通透。
        /// 与 Graphics/Quality 一样，色彩空间存在 <c>ProjectSettings/ProjectSettings.asset</c>，
        /// 不手改 YAML——走 <c>PlayerSettings.colorSpace</c> API，改完标脏落盘。
        /// 幂等：已是 Linear 则直接跳过。
        /// </para>
        /// </summary>
        [MenuItem("MyWorld/切换 Linear 色彩空间")]
        public static void ApplyLinearColorSpace()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                Debug.Log("[UrpSetup] 色彩空间已是 Linear，跳过。");
                return;
            }

            PlayerSettings.colorSpace = ColorSpace.Linear;

            MarkDirty("ProjectSettings/ProjectSettings.asset");
            AssetDatabase.SaveAssets();

            Debug.Log($"[UrpSetup] 色彩空间已切 Linear（原为 {ColorSpace.Gamma}），" +
                      $"ProjectSettings.asset 已落盘。切换后画面整体应变亮，颜色资产需目检回归。");
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
        /// 在 <c>Assets/Resources/BlockLitMaterial.mat</c> 建一个引用 URP/Lit 的哑材质。
        /// BlockMaterialLibrary.FindShader 通过 <c>Resources.Load&lt;Material&gt;</c> 加载它，
        /// 用它的 <c>.shader</c> 引用——这条路径把 URP/Lit 拽进 build，且只编哑材质需要的变体。
        /// 已存在则跳过（幂等）。Resources 是运行时可寻址的特殊目录，URP shader 通过材质
        /// 间接进 build 避免 alwaysIncludedShaders 触发 29 万变体编译。
        /// </summary>
        private static void EnsureBlockLitMaterial()
        {
            Shader lit = AssetDatabase.LoadAssetAtPath<Shader>(UrpLitShaderPath);
            if (lit == null)
            {
                Debug.LogWarning($"[UrpSetup] 找不到 {UrpLitShaderPath}（URP 包可能没装），跳过 BlockLitMaterial 创建");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(BlockLitMaterialPath);
            if (existing != null)
            {
                if (existing.shader == lit)
                {
                    Debug.Log("[UrpSetup] BlockLitMaterial 已存在且引用 URP/Lit，跳过");
                    return;
                }
                existing.shader = lit;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log("[UrpSetup] BlockLitMaterial 已更新为 URP/Lit");
                return;
            }

            Directory.CreateDirectory(BlockLitMaterialDirectory);
            var mat = new Material(lit) { name = "BlockLitMaterial" };
            AssetDatabase.CreateAsset(mat, BlockLitMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UrpSetup] 创建 {BlockLitMaterialPath}（shader = URP/Lit）");
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
