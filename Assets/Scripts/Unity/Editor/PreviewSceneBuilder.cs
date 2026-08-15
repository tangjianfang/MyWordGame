using System.IO;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 生成里程碑 1+2+3 的验收场景。场景文件是 YAML 引用网，手写不现实，
    /// 一律由这段脚本重建——既能在编辑器菜单里点，也能被批处理 -executeMethod 调用。
    /// </summary>
    public static class PreviewSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Preview.unity";

        [MenuItem("MyWorld/重建预览场景")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // m5 B2：环境光 Flat——Skybox 模式下 RenderSettings.ambientLight 是 no-op
            // （DayNightCycle 每帧写了画面也不吃），Flat 让它真正参与光照。
            // 场景默认给白天环境光 0.7 灰：EditMode 截图流水线没有 PlayerContext 单例，
            // DayNightCycle.Update 不跑，画面亮度全靠这里序列化进场景的值。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = MyWorld.Unity.Environment.DayNightCycle.DayAmbient;

            CreateLight();
            CreateWorld();

            // 玩家根（WorldBootstrap + PlayerController + BlockInteraction + 整套 UI）
            var player = new GameObject("玩家");
            player.AddComponent<WorldBootstrap>();
            player.AddComponent<FullscreenToggle>();
            player.AddComponent<HotbarUI>();
            player.AddComponent<HealthBarUI>();
            player.AddComponent<FoodBarUI>();
            player.AddComponent<DamageFlashUi>();
            player.AddComponent<CraftingPocketUi>();
            player.AddComponent<CraftingInventoryUi>();
            player.AddComponent<CraftingWorkbenchUi>();

            CreateCamera(player.transform);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"预览场景已生成：{ScenePath}");
        }

        private static void CreateWorld()
        {
            new GameObject("世界");
        }

        private static void CreateLight()
        {
            var gameObject = new GameObject("方向光");
            Light light = gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            // m5 B2：白天太阳强度 1.3（Linear 色彩空间下的 spec 校准值，
            // 与 DayNightCycle.DaySunIntensity 同源，别改成一个魔数）
            light.intensity = MyWorld.Unity.Environment.DayNightCycle.DaySunIntensity;
            gameObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateCamera(Transform player)
        {
            var gameObject = new GameObject("相机")
            {
                tag = "MainCamera"
            };

            Camera camera = gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.65f, 0.95f);
            camera.farClipPlane = 500f;

            gameObject.AddComponent<AudioListener>();
            gameObject.transform.SetParent(player, worldPositionStays: false);
            gameObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            gameObject.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        }
    }
}
