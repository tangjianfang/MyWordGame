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
        public float WalkPhase { get; set; }

        private void Awake()
        {
            _torso = MakePart("Torso", new Vector3(0.6f, 0.7f, 0.3f), new Vector3(0f, 0.85f, 0f), JacketColor);
            _head  = MakePart("Head",  new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.65f, 0f), SkinColor);
            _armL  = MakePart("ArmL",  new Vector3(0.2f, 0.7f, 0.2f), new Vector3(-0.4f, 0.9f, 0f), JacketColor);
            _armR  = MakePart("ArmR",  new Vector3(0.2f, 0.7f, 0.2f), new Vector3(+0.4f, 0.9f, 0f), JacketColor);
            _legL  = MakePart("LegL",  new Vector3(0.25f, 0.85f, 0.25f), new Vector3(-0.15f, 0.4f, 0f), PantsColor);
            _legR  = MakePart("LegR",  new Vector3(0.25f, 0.85f, 0.25f), new Vector3(+0.15f, 0.4f, 0f), PantsColor);
        }

        private static Transform MakePart(string name, Vector3 scale, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
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