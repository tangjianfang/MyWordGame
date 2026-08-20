using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 放置落点幽灵框（m12 P0-b）——准星命中时，在放置格（命中格沿法线外挪一格）
    /// 画一个半透明预览方块：<b>放哪一格看得见</b>，这是"六向可选"的体感来源。
    /// <para>
    /// 两态材质照水款半透做法（<see cref="Rendering.UrpMaterialFactory.CreateOverlay"/>）：
    /// 合法 = 水蓝 30%，会被防卡身挡下（含垫脚也不行的场合）= 红 30%。
    /// 结构照 <see cref="SelectionBox"/>（Cube mesh + 不参与光照的叠加材质，
    /// URP 下渲染最稳）；1.001 倍比方块略大防 z-fighting。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlacementGhostUi : MonoBehaviour
    {
        private Material _validMaterial;
        private Material _blockedMaterial;
        private MeshRenderer _renderer;

        /// <summary>合法放置态的水蓝半透色（与 SelectionBox 的黑 35% 同一材质工厂）。</summary>
        public static readonly Color ValidColor = new Color(0.55f, 0.85f, 1f, 0.30f);

        /// <summary>被挡态的红半透色。</summary>
        public static readonly Color BlockedColor = new Color(1f, 0.35f, 0.30f, 0.30f);

        /// <summary>EditMode 断言用：当前是否显示。</summary>
        public bool IsShown => gameObject.activeSelf;

        /// <summary>EditMode 断言用：当前显示的是不是合法态。</summary>
        public bool CurrentValid { get; private set; }

        public static PlacementGhostUi Create(Transform parent)
        {
            var go = new GameObject("PlacementGhost");
            go.transform.SetParent(parent, worldPositionStays: false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = SelectionBox.BuildEdgeMeshForReuse();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var ghost = go.AddComponent<PlacementGhostUi>();
            ghost._renderer = renderer;
            ghost._validMaterial = Rendering.UrpMaterialFactory.CreateOverlay(ValidColor);
            ghost._blockedMaterial = Rendering.UrpMaterialFactory.CreateOverlay(BlockedColor);
            ghost.Hide();
            return ghost;
        }

        /// <summary>把幽灵框摆到放置格；<paramref name="valid"/> 切换两态材质。</summary>
        public void ShowAt(int x, int y, int z, bool valid)
        {
            gameObject.SetActive(true);
            CurrentValid = valid;
            _renderer.sharedMaterial = valid ? _validMaterial : _blockedMaterial;
            transform.position = new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f);
            transform.localScale = new Vector3(1.001f, 1.001f, 1.001f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            CurrentValid = false;
        }
    }
}
