using System.Collections.Generic;
using MyWorld.Core.Entities;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// m5：人物/生物/UI 叠加层的 URP 材质工厂。
    /// 复用 BlockMaterialLibrary 的哑材质防剔除模式（见其 FindShader 注释），
    /// 同色缓存复用。裸 CreatePrimitive 的 Default-Material 是 Standard shader，
    /// URP 下渲染洋红——所有角色 cube 必须经本工厂取材质。
    /// </summary>
    public static class UrpMaterialFactory
    {
        private static Shader _litShader;
        private static readonly Dictionary<Color, Material> LitCache = new Dictionary<Color, Material>();

        /// <summary>URP/Lit 不透明染色材质（人物身体/生物 cube）。</summary>
        public static Material CreateLit(Color baseColor)
        {
            if (LitCache.TryGetValue(baseColor, out var cached)) return cached;
            if (_litShader == null)
            {
                // 与 BlockMaterialLibrary.FindShader 同理：Resources 哑材质把 URP/Lit 拽进
                // build 防 Shader.Find 在 standalone 里被剔除成 null
                var resourceMat = Resources.Load<Material>("BlockLitMaterial");
                _litShader = resourceMat != null && resourceMat.shader != null
                    ? resourceMat.shader
                    : Shader.Find("Universal Render Pipeline/Lit");
            }
            var material = new Material(_litShader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            else material.SetColor("_Color", baseColor); // 非 URP 回退
            LitCache[baseColor] = material;
            return material;
        }

        /// <summary>生物身体色（spec 表）。MobKind → HEX。</summary>
        public static Color MobBodyColor(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig: return Hex("#E8A0A8");
                case MobKind.Cow: return Hex("#6B4A35");
                case MobKind.Chicken: return Hex("#F0EDE5");
                case MobKind.Zombie: return Hex("#5A8A4A");
                case MobKind.Villager: return Hex("#7A5C3E");
                default: return Hex("#8A8A8A"); // 旧三类 Passive/Hostile/Neutral 中性灰
            }
        }

        /// <summary>生物头部色（spec 表：牛头白花/鸡红冠/僵尸浅绿/村民肤色）。</summary>
        public static Color MobHeadColor(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig: return Hex("#E8B8C0");
                case MobKind.Cow: return Hex("#F0E8E0");
                case MobKind.Chicken: return Hex("#D94F3D");
                case MobKind.Zombie: return Hex("#6FA05C");
                case MobKind.Villager: return Hex("#E8B88A");
                default: return Hex("#9A9A9A");
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
