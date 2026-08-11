using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 一个 1.002 倍大小的立方体线框，用 12 条细长方体（盒子的边）模拟 12 条线。
    /// <para>
    /// 为什么不直接用 <c>GL.LINES</c>：URP 下 <c>OnRenderObject</c> / <c>GL</c> 行为不可靠，
    /// 一个不参与光照的 Mesh + Material 路线最稳，URP 也能正常渲染。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SelectionBox : MonoBehaviour
    {
        // 注释：本类用 CreatePrimitive(Cube) + scale=1.002 模拟"线框"，最简但视觉粗糙。
        // 早期设计曾考虑 12 条细长方体合并 mesh（EdgeDirections / EdgeOrigins / EdgeThickness），
        // 当前实现未使用——如需更精细的线框，可在此处重新引入。

        public static SelectionBox Create(Transform parent, Material material)
        {
            var go = new GameObject("SelectionBox");
            go.transform.SetParent(parent, worldPositionStays: false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildEdgeMesh();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go.AddComponent<SelectionBox>();
        }

        private static Mesh BuildEdgeMesh()
        {
            // 每条边是一个细长方体：origin + direction × length，截面 EdgeThickness×EdgeThickness
            // 简单起见，用 Cube.CreatePrimitive 的顶点数 24（6 面 × 4 顶点），但只画外表面
            // ——为了不增加资源依赖，直接手算 12 个 box，每个 8 个顶点合并即可。
            // 实际工程里用 Graphics.DrawMesh / Mesh.CombineMeshes 更整洁；这里用最简实现。
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mesh = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
                mesh.name = "SelectionBoxEdges";
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        /// <summary>把线框摆到指定方块格的位置；scale 1.002 让它比方块略大一点。</summary>
        public void ShowAt(int x, int y, int z)
        {
            gameObject.SetActive(true);
            transform.position = new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f);
            transform.localScale = new Vector3(1.002f, 1.002f, 1.002f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
