using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Items
{
    /// <summary>
    /// m7 B1：单个掉落物的视觉——0.25 格小方块，悬浮 + 绕 Y 自转。
    /// 挖掉的方块「看得见」全靠它：实体本身是纯数据（Core），这里只把
    /// <see cref="ItemDropEntity.Position"/> 每帧映射到 transform 并叠加动画。
    /// <para>
    /// 材质走 <see cref="UrpMaterialFactory.CreateLit"/>（<see cref="ResolveColor"/>
    /// 取物品贴图均值主色；贴图缺失 / 物品未注册退亮灰暗化）——绝不品红：
    /// 品红是 UI「贴图缺失」占位语义（ItemSlotDrawer.Missing），3D 里再用品红
    /// 就分不清「贴图没配」和「正常渲染」了。
    /// </para>
    /// </summary>
    public sealed class ItemDropView : MonoBehaviour
    {
        /// <summary>小方块边长（格）。0.25 = 四分之一格，掉一片也不挡视线 / 挡路。</summary>
        public const float VisualSize = 0.25f;

        /// <summary>上下浮动幅度（米），叠加在实体 Y 之上。</summary>
        public const float FloatAmplitude = 0.1f;

        /// <summary>浮动角频率（rad/s）：y 偏移 = <see cref="FloatAmplitude"/> * sin(t*2)。</summary>
        public const float FloatFrequency = 2f;

        /// <summary>绕 Y 轴自转角速度（度/秒）。</summary>
        public const float SpinDegreesPerSecond = 90f;

        /// <summary>贴图缺失 / 物品未注册时的兜底色：亮灰暗化（别品红，见类注释）。</summary>
        public static readonly Color FallbackColor = new Color(0.58f, 0.58f, 0.58f);

        /// <summary>本视图绑定的掉落实体（数据真源，位置只读它）。</summary>
        public ItemDropEntity Drop { get; private set; }

        // 贴图均值色缓存：同一物品的掉落物一片几十个，只读一次 PNG
        private static readonly Dictionary<string, Color?> TextureColorCache = new Dictionary<string, Color?>();

        /// <summary>建视图：0.25 格小方块挂到 <paramref name="parent"/> 下，按物品主色染材质。
        /// 只应被 <see cref="ItemDropViewRegistry"/> 调用（生命周期由它 diff 管理）。</summary>
        public static ItemDropView Create(Transform parent, ItemDropEntity drop, ItemDatabase items)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "掉落物";
            // 碰撞体必须删：拾取判定走 PlayerController 与实体的距离检查，
            // 留着会挡玩家移动（WorldSolidSource 之外的意外碰撞体）和挖掘射线
            var collider = cube.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = new Vector3(VisualSize, VisualSize, VisualSize);

            var view = cube.AddComponent<ItemDropView>();
            view.Drop = drop;
            cube.GetComponent<Renderer>().sharedMaterial =
                UrpMaterialFactory.CreateLit(ResolveColor(drop.Content.Value, items));
            view.SyncPose(Time.time);
            return view;
        }

        /// <summary>物品主色：贴图像素均值（跳过全透明像素）。物品未注册 / 贴图缺失退
        /// <see cref="FallbackColor"/> 亮灰暗化——不品红（品红是 UI 贴图缺失占位语义）。</summary>
        public static Color ResolveColor(ItemStack stack, ItemDatabase items)
        {
            if (items != null && items.TryGetByNumericId(stack.ItemId, out var def)
                && !string.IsNullOrEmpty(def.Texture)
                && TryAverageTextureColor(def.Texture, out var color))
            {
                return color;
            }
            return FallbackColor;
        }

        /// <summary>把实体位置 + 浮动 / 自转动画写到 transform。公开是为了 EditMode 测试
        /// 手动步进（EditMode 不自动跑 LateUpdate）；<paramref name="time"/> 是动画时钟。</summary>
        public void SyncPose(float time)
        {
            if (Drop == null) return;
            float y = Drop.Position.Y + FloatAmplitude * Mathf.Sin(time * FloatFrequency);
            transform.position = new Vector3(Drop.Position.X, y, Drop.Position.Z);
            // 绝对角度（time × 角速度）而非逐帧累加——帧率无关、不吃浮点漂移
            transform.rotation = Quaternion.Euler(0f, time * SpinDegreesPerSecond % 360f, 0f);
        }

        private void LateUpdate() => SyncPose(Time.time);

        /// <summary>读贴图求均值色。候选路径与 ItemSlotDrawer.GetTextureOrPlaceholder 同源：
        /// 优先 StreamingAssets/items/textures（运行时 Player 数据），退 Assets/Art/Items（编辑器 fallback）。
        /// 结果按贴图名缓存（含「找不到」的 null，坏路径只碰一次磁盘）。</summary>
        private static bool TryAverageTextureColor(string textureName, out Color color)
        {
            color = default;
            if (TextureColorCache.TryGetValue(textureName, out var cached))
            {
                if (cached.HasValue) color = cached.Value;
                return cached.HasValue;
            }

            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "items", "textures", textureName + ".png"),
                Path.Combine(Application.dataPath, "Art", "Items", textureName + ".png"),
                // 评审 08 F0：共用方块贴图的物品（bed/chest/glass/sand/torch/wooden_door）第三候选
                Path.Combine(Application.streamingAssetsPath, "blocks", "textures", textureName + ".png"),
            };
            string resolved = null;
            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    resolved = candidate;
                    break;
                }
            }

            Color? found = null;
            if (resolved != null)
            {
                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(resolved));
                var pixels = texture.GetPixels32();
                long r = 0, g = 0, b = 0;
                int count = 0;
                foreach (var pixel in pixels)
                {
                    if (pixel.a == 0) continue; // 全透明像素（贴图留白）不参与均值
                    r += pixel.r;
                    g += pixel.g;
                    b += pixel.b;
                    count++;
                }
                if (count > 0)
                {
                    found = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
                if (Application.isPlaying) Destroy(texture);
                else DestroyImmediate(texture);
            }

            TextureColorCache[textureName] = found;
            if (found.HasValue) color = found.Value;
            return found.HasValue;
        }
    }
}
