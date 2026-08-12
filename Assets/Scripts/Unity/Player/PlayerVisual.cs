using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 第三人称可见的玩家身体 + 走路动画。
    /// 纯 Primitive 拼装，MaterialPropertyBlock 染色。第一人称视角下也保留物体，
    /// 由 CameraThirdPerson 控制相机位置规避自遮挡。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        // 调色板——与 art/requests/player/skin.md 一致
        private static readonly Color SkinColor   = new Color(0xC9/255f, 0x8F/255f, 0x68/255f);
        private static readonly Color JacketColor = new Color(0x3E/255f, 0x7A/255f, 0x9C/255f);
        private static readonly Color PantsColor  = new Color(0x4A/255f, 0x4A/255f, 0x5E/255f);
        private static readonly Color BootColor   = new Color(0x5A/255f, 0x46/255f, 0x32/255f);
        private static readonly Color HairColor   = new Color(0x3B/255f, 0x2A/255f, 0x1C/255f);

        private Transform _head, _torso, _armL, _armR, _legL, _legR;
        private PlayerController _controller;
        private Vector3 _lastPos;
        private float _headBaseY = 1.65f;

        public float WalkPhase { get; set; }

        private void Awake()
        {
            // 注意：先 SetParent 再设 localPosition/localScale——顺序反了会导致
            // localPosition 在 SetParent 前被解释成世界坐标，reparent 后偏移。
            _torso = MakePart("Torso", transform, new Vector3(0.6f, 0.7f, 0.3f), new Vector3(0f, 0.85f, 0f), JacketColor);
            _head  = MakePart("Head",  transform, new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.65f, 0f), SkinColor);
            _armL  = MakePart("ArmL",  transform, new Vector3(0.2f, 0.7f, 0.2f), new Vector3(-0.4f, 0.9f, 0f), JacketColor);
            _armR  = MakePart("ArmR",  transform, new Vector3(0.2f, 0.7f, 0.2f), new Vector3(+0.4f, 0.9f, 0f), JacketColor);
            _legL  = MakePart("LegL",  transform, new Vector3(0.25f, 0.85f, 0.25f), new Vector3(-0.15f, 0.4f, 0f), PantsColor);
            _legR  = MakePart("LegR",  transform, new Vector3(0.25f, 0.85f, 0.25f), new Vector3(+0.15f, 0.4f, 0f), PantsColor);
        }

        private void Start()
        {
            _controller = GetComponent<PlayerController>();
            _lastPos = transform.position;
        }

        private void Update()
        {
            if (_controller == null) return;
            Vector3 pos = transform.position;
            Vector3 delta = pos - _lastPos;
            _lastPos = pos;

            float speed = new Vector2(delta.x, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

            if (speed > 0.1f)
            {
                WalkPhase += Time.deltaTime * speed * 8f;
            }

            float swing = Mathf.Sin(WalkPhase) * 30f;        // 度
            float bob = Mathf.Abs(Mathf.Sin(WalkPhase * 2f)) * 0.08f;

            if (_legL != null) _legL.localRotation = Quaternion.Euler(+swing, 0f, 0f);
            if (_legR != null) _legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (_armL != null) _armL.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(+swing, 0f, 0f);
            if (_head != null) _head.localPosition = new Vector3(0f, _headBaseY + bob, 0f);
        }

        private void OnDestroy()
        {
            // 6 个 Cube 是 CreatePrimitive 创建的，Unity 不会随父节点销毁而自动销毁
            // （因为它们是不同 GameObject）——必须显式清。否则 EditMode 测试间会泄漏，
            // 场景根上残留孤儿子物体，污染后续 fixture。
            // 用 Destroy 而非 DestroyImmediate：OnDestroy 是生产生命周期（PlayMode 由 Unity
            // 引擎调用），Destroy 是合法且常规的清理方式。
            foreach (var t in new[] { _head, _torso, _armL, _armR, _legL, _legR })
            {
                if (t != null) Object.Destroy(t.gameObject);
            }
        }

        private static Transform MakePart(string name, Transform parent, Vector3 scale, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            // worldPositionStays=false：避免 SetParent 期间临时 worldPosition 漂移导致
            // 子物体在世界空间里瞬移。后续 localPosition / localScale 都在 parent 本地
            // 坐标系里赋值，是 PlayerController 移动 transform 时身体跟随的关键。
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.localPosition = localPos;
            // 移除自带的 BoxCollider，避免和 ChunkStreamer 玩家位置冲突
            // 用 DestroyImmediate 而非 Destroy：EditMode 测试里 Destroy 会报
            // "Destroy may not be called from edit mode"；DestroyImmediate 在 PlayMode
            // 也安全（物理还没启动，collider 必须立刻被清除）。
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            ApplyColor(go, color);
            return go.transform;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static void ApplyColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
        }
    }
}