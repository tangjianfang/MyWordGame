using System.IO;
using MyWorld.Unity.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 生成里程碑 1 的验收场景。场景文件是 YAML 引用网，手写不现实，
    /// 一律由这段脚本重建——既能在编辑器菜单里点，也能被批处理 -executeMethod 调用。
    /// </summary>
    public static class PreviewSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Preview.unity";

        [MenuItem("MyWorld/重建预览场景")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateCamera();

            var world = new GameObject("世界");
            world.AddComponent<WorldBootstrap>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"预览场景已生成：{ScenePath}");
        }

        private static void CreateLight()
        {
            var gameObject = new GameObject("方向光");
            Light light = gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            // 略微偏斜，让方块的三个可见面亮度分开，轮廓才清楚
            gameObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateCamera()
        {
            var gameObject = new GameObject("相机")
            {
                tag = "MainCamera"
            };

            Camera camera = gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.65f, 0.95f);
            // 默认 1000 太远、地形又不高，缩到 500 让深度精度好一些
            camera.farClipPlane = 500f;

            gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<FreeFlyCamera>();

            // 站在地表之上、稍微退开一点，Play 之后立刻能看到地形而不是卡在土里
            gameObject.transform.position = new Vector3(0f, 95f, -40f);
            gameObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
        }
    }
}
