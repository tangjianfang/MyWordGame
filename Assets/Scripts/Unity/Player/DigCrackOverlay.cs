using System.IO;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 破坏裂纹叠层（m12 P0-a）——挖掘蓄力期间在目标方块上叠一张裂纹贴图，
    /// 按进度分档换图（break-0..4 五档，越挖越碎），满格时由 BlockInteraction 破坏。
    /// <para>
    /// 结构照 <see cref="SelectionBox"/>（Cube mesh + 叠加材质），1.003 倍比方块略大
    /// （在选中框 1.002 之外再让一丝，两层不 z-fighting）。
    /// 贴图从 <c>StreamingAssets/ui/break-N.png</c> 读（m6 B3 教训：standalone 读不到
    /// Assets/Art 目录，必须走 streamingAssetsPath）；缺图静默降级为"无裂纹"，
    /// 蓄力计时不受影响（视觉 nice-to-have，不阻塞玩法）。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DigCrackOverlay : MonoBehaviour
    {
        /// <summary>裂纹档位数（break-0..4 五档，与 Assets/Art/UI→StreamingAssets/ui 的五张图对应）。</summary>
        public const int StageCount = 5;

        private Texture2D[] _stages;
        private Material _material;
        private MeshRenderer _renderer;

        /// <summary>EditMode 断言用：当前显示的档位（-1 = 隐藏）。</summary>
        public int CurrentStage { get; private set; } = -1;

        /// <summary>EditMode 断言用：贴图是否加载成功（EditMode/实机缺图时 false，功能降级不炸）。</summary>
        public bool TexturesLoaded => _stages != null;

        public static DigCrackOverlay Create(Transform parent)
        {
            var go = new GameObject("DigCrackOverlay");
            go.transform.SetParent(parent, worldPositionStays: false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = SelectionBox.BuildEdgeMeshForReuse();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var crack = go.AddComponent<DigCrackOverlay>();
            crack._renderer = renderer;
            crack._material = Rendering.UrpMaterialFactory.CreateOverlay(Color.white);
            crack._renderer.sharedMaterial = crack._material;
            crack._stages = LoadStageTextures();
            crack.Hide();
            return crack;
        }

        private static Texture2D[] LoadStageTextures()
        {
            var stages = new Texture2D[StageCount];
            int loaded = 0;
            for (int i = 0; i < StageCount; i++)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "ui", $"break-{i}.png");
                if (!File.Exists(path))
                {
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                };
                if (tex.LoadImage(bytes))
                {
                    stages[i] = tex;
                    loaded++;
                }
                else
                {
                    // EditMode 下 Destroy 非法（会当错误日志炸测试）——初始化期一次性
                    // 清理用 DestroyImmediate
                    Object.DestroyImmediate(tex);
                }
            }

            return loaded == StageCount ? stages : null; // 缺任何一张整体降级，不出现半截裂纹
        }

        /// <summary>摆到目标方块并切到第 <paramref name="stage"/> 档裂纹（0..StageCount-1）。</summary>
        public void ShowAt(int x, int y, int z, int stage)
        {
            if (_stages == null)
            {
                return; // 贴图缺失：整个叠层保持隐藏，蓄力照常
            }

            stage = Mathf.Clamp(stage, 0, StageCount - 1);
            gameObject.SetActive(true);
            CurrentStage = stage;
            _material.mainTexture = _stages[stage];
            transform.position = new Vector3(x, y, z) + new Vector3(0.5f, 0.5f, 0.5f);
            transform.localScale = new Vector3(1.003f, 1.003f, 1.003f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            CurrentStage = -1;
        }
    }
}
