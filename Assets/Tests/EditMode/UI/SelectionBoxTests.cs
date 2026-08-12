#if UNITY_EDITOR
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// Task C2：验证 SelectionBox 不接收阴影（避免选区轮廓被投影暗化）。
    /// 实际材质创建逻辑在 <see cref="BlockInteraction"/>，构造链路太长；
    /// 这里直接测 <see cref="SelectionBox.Create"/> 出来的 GameObject 上的 MeshRenderer。
    /// </summary>
    [TestFixture]
    public class SelectionBoxTests
    {
        [Test]
        public void Create_Renderer_HasReceiveShadowsFalse()
        {
            // 用最简材质（URP/Unlit 存在时才用，否则 Hidden/Internal-Colored），
            // 关键断言是 SelectionBox.Create 把 renderer.receiveShadows 设成 false。
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Hidden/Internal-Colored");
            Assert.That(shader, Is.Not.Null, "URP/Unlit 或 Hidden/Internal-Colored 必须有一个");
            var mat = new Material(shader);
            var host = new GameObject("Host");
            try
            {
                var go = SelectionBox.Create(host.transform, mat);
                var renderer = go.GetComponent<MeshRenderer>();
                Assert.That(renderer.receiveShadows, Is.False,
                    "SelectionBox 必须不接收阴影（避免选区轮廓被投影暗化）");
                Assert.That(renderer.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),
                    "SelectionBox 必须不投射阴影");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif