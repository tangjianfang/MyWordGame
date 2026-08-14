using MyWorld.Core.Entities;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 村民 cube 渲染：棕色头巾（cube 上方偏移）+ 衣服颜色按 profession。
    /// </summary>
    public sealed class VillagerView : MonoBehaviour
    {
        public Villager Villager;
        public Renderer Renderer;
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public static VillagerView Attach(GameObject host, Villager villager)
        {
            var view = host.AddComponent<VillagerView>();
            view.Villager = villager;
            view.Renderer = host.GetComponent<Renderer>();
            var color = villager.Profession switch
            {
                VillagerProfession.Farmer => new Color(0.55f, 0.4f, 0.2f),
                VillagerProfession.Librarian => new Color(0.45f, 0.35f, 0.5f),
                VillagerProfession.Blacksmith => new Color(0.3f, 0.3f, 0.35f),
                _ => new Color(0.6f, 0.5f, 0.4f),
            };
            view.ApplyColor(color);
            return view;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        public void ApplyColor(Color c)
        {
            if (Renderer == null) return;
            // EditMode 测试里 AddComponent 不触发 Awake，_block 可能为 null → 懒初始化
            if (_block == null) _block = new MaterialPropertyBlock();
            Renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            Renderer.SetPropertyBlock(_block);
        }

        private void LateUpdate()
        {
            if (Villager == null) return;
            transform.position = new Vector3(Villager.Position.X, Villager.Position.Y, Villager.Position.Z);
            if (Villager.HitFlashTimer > 0) ApplyColor(Color.red);
        }
    }
}