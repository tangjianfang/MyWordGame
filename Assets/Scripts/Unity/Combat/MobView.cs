using MyWorld.Core.Entities;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 把 Core 的 <see cref="Mob"/> 绑定到一个 GameObject 上。
    /// 简单 cube：猪粉、羊白、僵尸绿。
    /// 同步 Position/Color（受伤红闪）。
    /// </summary>
    public sealed class MobView : MonoBehaviour
    {
        public Mob Mob;
        public Renderer Renderer;
        public Color BaseColor;
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        public static MobView Attach(GameObject host, Mob mob)
        {
            var view = host.AddComponent<MobView>();
            view.Mob = mob;
            view.Renderer = host.GetComponent<Renderer>();
            view.BaseColor = mob.MobTypeId switch
            {
                1 => new Color(0.95f, 0.7f, 0.7f),   // pig
                2 => new Color(0.95f, 0.95f, 0.95f), // sheep
                3 => new Color(0.4f, 0.7f, 0.3f),    // zombie
                _ => Color.gray,
            };
            view.ApplyColor(view.BaseColor);
            return view;
        }

        public void ApplyColor(Color c)
        {
            if (Renderer == null) return;
            Renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            Renderer.SetPropertyBlock(_block);
        }

        private void LateUpdate()
        {
            if (Mob == null) return;
            transform.position = new Vector3(Mob.Position.X, Mob.Position.Y, Mob.Position.Z);
            if (Mob.HitFlashTimer > 0)
            {
                ApplyColor(Color.red);
            }
            else
            {
                ApplyColor(BaseColor);
            }
        }
    }
}
