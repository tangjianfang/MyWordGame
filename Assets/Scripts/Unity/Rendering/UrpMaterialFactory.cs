using System;
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
            Material material = new Material(EnsureLitShader());
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
            else material.SetColor("_Color", baseColor); // 非 URP 回退
            LitCache[baseColor] = material;
            return material;
        }

        /// <summary>
        /// 半透明叠加材质（选中框等）：URP/Lit 切 Transparent 表面。
        /// <para>
        /// 配方与 URP 14 BaseShaderGUI.SetupMaterialBlendMode 的 Transparent 分支一致
        /// （关键字 <c>_SURFACE_TYPE_TRANSPARENT</c> 已对照本机包源码确认），
        /// 混合状态由 shader 里的 <c>Blend[_SrcBlend][_DstBlend]</c> 材质属性驱动。
        /// 不缓存——调用方（选中框）只建一次，缓存反而留一份永不复用的材质。
        /// </para>
        /// </summary>
        public static Material CreateOverlay(Color rgba)
        {
            var material = new Material(EnsureLitShader());
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", rgba);
            else material.SetColor("_Color", rgba); // 非 URP 回退
            // URP 14 运行时切透明表面的配方（Surface=Transparent, Blend=Alpha）
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // Blend 行带单独的 alpha 通道分量：[_SrcBlendAlpha][_DstBlendAlpha]，按 Alpha 混合设 One/OneMinusSrcAlpha
            material.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        /// <summary>
        /// m11 W3-4：带贴图的半透明叠加材质（粒子特效面片：爆炸帧 / 附魔光柱）。
        /// 配方同 <see cref="CreateOverlay"/>（着色器色 Alpha 控制透明，两态材质约定），
        /// 额外接 <paramref name="texture"/> 到 _BaseMap；贴图传 null 时退纯色面片——
        /// 占位贴图未入库也不至于整个特效消失。不缓存——每粒子槽位独立实例
        /// （切帧/淡出只改自己），总量 ≤ 池容量，缓存无意义。
        /// </summary>
        public static Material CreateTexturedOverlay(Texture2D texture, Color rgba)
        {
            var material = CreateOverlay(rgba);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point; // 32×32 像素风，拒绝双线性糊
                material.mainTexture = texture;
            }
            return material;
        }

        /// <summary>
        /// 取 URP/Lit shader（哑材质防剔除 + Standard 回退），CreateLit / CreateOverlay 共用。
        /// 找不到时抛异常——与 BlockMaterialLibrary 同一硬约束：shader 为 null 时
        /// new Material(null) 不报错但渲染全粉红，必须在源头拦下并给出可操作的提示。
        /// </summary>
        private static Shader EnsureLitShader()
        {
            if (_litShader != null) return _litShader;
            // 与 BlockMaterialLibrary.FindShader 同理：Resources 哑材质把 URP/Lit 拽进
            // build 防 Shader.Find 在 standalone 里被剔除成 null
            var resourceMat = Resources.Load<Material>("BlockLitMaterial");
            _litShader = resourceMat != null && resourceMat.shader != null
                ? resourceMat.shader
                : Shader.Find("Universal Render Pipeline/Lit");
            // 装了 URP 用 URP Lit，没装则退回内置管线 Standard，两条路都能跑
            if (_litShader == null)
            {
                _litShader = Shader.Find("Standard");
            }
            if (_litShader == null)
            {
                throw new InvalidOperationException(
                    "URP Lit 与内置 Standard 都找不到，无法建立角色材质。先跑 MyWorld/接入 URP 管线。");
            }
            return _litShader;
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
