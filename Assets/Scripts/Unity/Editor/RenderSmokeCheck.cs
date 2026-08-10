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
                // TODO: RenderSmokeCheck 已废弃——Task 11 用 PlayHarness 替代 (玩家层: Bootstrap 改造)
                // 原实现调 bootstrap.BuildWorld() 同步建出所有区块段；Task 10 把 WorldBootstrap 改为
                // 由 ChunkStreamer 按玩家位置持续生成 / 卸载区块，没有 BuildWorld() 这种一次性入口。
                // 留个 WorldBootstrap 让依赖的 Editor 程序集保持可编译，等 Task 11 替换为真正的 PlayHarness。
                _ = host.AddComponent<WorldBootstrap>();

                failures += Check(false,
                    "RenderSmokeCheck 已废弃——Task 11 用 PlayHarness 替换，" +
                    "届时在 Play 模式下驱动 ChunkStreamer 跑冒烟链路");

                Debug.Log("冒烟检查跳过：链路已改由 ChunkStreamer + PlayHarness 验证，请用「MyWorld/PlayHarness」入口。");
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

        /// <summary>
        /// 报告实际用到的 shader。装了 URP 应当是 <c>Universal Render Pipeline/Lit</c>，
        /// 没装则是内置的 <c>Standard</c>——这行日志是判断 URP 到底有没有生效的唯一无头手段。
        /// </summary>
        private static string ShaderNameOf(MeshRenderer[] renderers)
        {
            Material material = renderers
                .SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m != null);

            return material == null ? "（没有材质）" : material.shader.name;
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
