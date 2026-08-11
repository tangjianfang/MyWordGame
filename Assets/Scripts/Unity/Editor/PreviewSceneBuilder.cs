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
            CreateWorld();

            var player = new GameObject("玩家");
            player.AddComponent<WorldBootstrap>();

            CreateCamera(player.transform);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"预览场景已生成：{ScenePath}");
        }

        /// <summary>
        /// 建一个空的 <c>世界</c> GameObject，专门用作区块 GameObject 的父节点。
        /// <para>
        /// 之前用 <c>WorldBootstrap.transform</c>（也就是 <c>玩家</c>）当父节点——
        /// <see cref="MyWorld.Unity.Rendering.ChunkSectionView.Create"/> 把
        /// <c>chunkX*16</c> 等世界坐标当作 localPosition 写进玩家根下，配合
        /// <c>SetParent(parent, worldPositionStays:false)</c>，每个区块整体被玩家 Y
        /// 平移 120 单位，玩家从 (0.5, 120, 0.5) 出生、相机 (0.5, 121.62, 0.5) 朝下看，
        /// 地表方块全在 y≈216，相机抬头才能看到，画面只剩天空蓝。建一个静态的
        /// <c>世界</c> 节点，<see cref="MyWorld.Unity.Bootstrap.WorldBootstrap.Awake"/>
        /// 用 <c>GameObject.Find("世界")</c> 拿到它的 transform 传给
        /// <see cref="MyWorld.Unity.Rendering.ChunkViewRegistry"/>，让区块 GameObject
        /// 的世界坐标不再跟随玩家移动。
        /// </para>
        /// </summary>
        private static void CreateWorld()
        {
            new GameObject("世界");
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

        /// <summary>
        /// 相机挂在玩家根下作为眼睛。高度取 <see cref="MyWorld.Core.Player.PlayerMotorSettings.EyeHeight"/>
        /// 的默认值，让玩家抬头 / 低头时相机跟随旋转（FreeFlyCamera 已退役）。
        /// </summary>
        private static void CreateCamera(Transform player)
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

            // 出生点: WorldBootstrap 默认 (0.5, 80, 0.5),把相机挂到玩家身上,相对坐标 = 眼高
            gameObject.transform.SetParent(player, worldPositionStays: false);
            gameObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            gameObject.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        }
    }
}
