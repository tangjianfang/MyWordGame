using System.Linq;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Rendering;
using UnityEditor;
using UnityEngine;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 无头冒烟检查：在非播放态下把渲染层整条链路真跑一遍（读注册表 → 建材质 → 生成世界 →
    /// 贪心网格 → 拆 submesh → 上传 Mesh → 挂 MeshRenderer），并检查产物是否讲得通。
    /// <para>
    /// 它**替代不了人工验收**——贴图是否无缝、区块是否对齐这类事只有眼睛能判断。
    /// 它能挡住的是崩溃、贴图文件缺失、submesh 与材质槽数量对不上、网格空掉这类硬错误。
    /// </para>
    /// </summary>
    public static class RenderSmokeCheck
    {
        [MenuItem("MyWorld/渲染层冒烟检查")]
        public static void Run()
        {
            var host = new GameObject("冒烟检查");
            var failures = 0;

            try
            {
                var bootstrap = host.AddComponent<WorldBootstrap>();
                bootstrap.BuildWorld();

                MeshFilter[] filters = host.GetComponentsInChildren<MeshFilter>();
                MeshRenderer[] renderers = host.GetComponentsInChildren<MeshRenderer>();

                failures += Check(filters.Length > 0, $"应当至少生成一个区块段，实际 {filters.Length} 个");

                var totalTriangles = 0;
                foreach (MeshFilter filter in filters)
                {
                    Mesh mesh = filter.sharedMesh;
                    var renderer = filter.GetComponent<MeshRenderer>();

                    failures += Check(mesh != null && mesh.vertexCount > 0,
                        $"{filter.name} 的网格没有顶点");

                    if (mesh == null)
                    {
                        continue;
                    }

                    totalTriangles += (int)mesh.GetIndexCount(0) / 3;

                    failures += Check(mesh.subMeshCount == renderer.sharedMaterials.Length,
                        $"{filter.name} 有 {mesh.subMeshCount} 个 submesh，却挂了 " +
                        $"{renderer.sharedMaterials.Length} 个材质，两者必须一一对应");

                    failures += Check(renderer.sharedMaterials.All(m => m != null),
                        $"{filter.name} 的材质槽里有 null");

                    failures += Check(renderer.sharedMaterials.All(m => m.name != "缺失贴图"),
                        $"{filter.name} 用到了占位材质，说明有贴图文件没找到");

                    failures += Check(renderer.sharedMaterials.All(m => m.mainTexture != null &&
                                                                       m.mainTexture.wrapMode == TextureWrapMode.Repeat),
                        $"{filter.name} 的贴图环绕模式不是 Repeat，合并后的大面会被拉伸");

                    failures += Check(renderer.sharedMaterials.All(m => m.mainTexture == null ||
                                                                       m.mainTexture.filterMode == FilterMode.Point),
                        $"{filter.name} 的贴图过滤不是 Point，像素风会糊掉");
                }

                Debug.Log($"冒烟检查完成：{filters.Length} 个区块段，第 0 号 submesh 合计 {totalTriangles} 个三角形。");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            if (failures > 0)
            {
                Debug.LogError($"冒烟检查失败：{failures} 项不通过。");
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.Log("冒烟检查通过。注意：贴图接缝与区块对齐仍需人工在 Play 模式下确认。");
            }
        }

        private static int Check(bool condition, string message)
        {
            if (condition)
            {
                return 0;
            }

            Debug.LogError($"冒烟检查不通过：{message}");
            return 1;
        }
    }
}
