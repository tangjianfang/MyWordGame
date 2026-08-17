using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.FX
{
    /// <summary>
    /// m11 W3-4：通用粒子池——<see cref="Capacity"/> 64 槽复用，**热路径零 new**
    /// （状态数组与视图对象全部预分配/懒建一次后循环使用，Update 内无任何分配）。
    /// 三种纯视觉特效：
    /// <list type="bullet">
    /// <item><b>挖掘碎屑</b>：挖掉/收获方块时 4-6 粒「方块贴图均值色」小方块，
    ///   抛物线回落 <see cref="DebrisDuration"/>0.5s（贴图缺失退亮灰，不品红——
    ///   品红是 UI 贴图缺失占位语义）</item>
    /// <item><b>爆炸帧</b>：fx-explosion 三帧横排贴图，<see cref="ExplosionDuration"/>0.3s
    ///   逐帧播放（占位贴图在 Assets/StreamingAssets/fx/，缺图退纯色面片不消失）</item>
    /// <item><b>附魔光柱</b>：magic-enchant-column 竖向面片从地面升起 <see cref="EnchantDuration"/>1s
    ///   后淡出（Alpha 走材质色，两态材质约定见 <see cref="UrpMaterialFactory.CreateOverlay"/>）</item>
    /// </list>
    /// <para>
    /// <b>挂载点全走公开事件/通知，不碰玩法逻辑</b>：
    /// <see cref="MyWorld.Unity.Player.BlockInteraction.BlockBroken"/>（m11 W3-4 新增公开事件）、
    /// <see cref="MyWorld.Core.Combat.Explosion.AfterDetonate"/>（同）、
    /// <see cref="MyWorld.Unity.UI.EnchantingUi.Enchanted"/>（同）。本组件 OnEnable 订阅、
    /// OnDisable 退订；无本组件时三个事件空发零开销，游戏逻辑完全无感。
    /// </para>
    /// </summary>
    public sealed class ParticlePool : MonoBehaviour
    {
        /// <summary>粒子种类。public 给 EditMode 测试断言槽位状态。</summary>
        public enum FxKind
        {
            /// <summary>空槽（可分配）。</summary>
            None = 0,

            /// <summary>挖掘碎屑：小方块抛物线。</summary>
            Debris,

            /// <summary>爆炸：三帧贴画面片。</summary>
            Explosion,

            /// <summary>附魔光柱：竖向贴画面片升起淡出。</summary>
            EnchantColumn,
        }

        /// <summary>池容量（同时存活的粒子上限；超发覆盖最旧槽）。</summary>
        public const int Capacity = 64;

        /// <summary>碎屑存活时长（秒）。</summary>
        public const float DebrisDuration = 0.5f;

        /// <summary>一次挖掘的碎屑粒数下限。</summary>
        public const int DebrisMinPerBreak = 4;

        /// <summary>一次挖掘的碎屑粒数上限。</summary>
        public const int DebrisMaxPerBreak = 6;

        /// <summary>碎屑重力（格/s²）——v0y≈2-3.2 时 0.5s 内恰好走过一段抛物线。</summary>
        public const float DebrisGravity = 9f;

        /// <summary>碎屑小方块的边长（格）。</summary>
        public const float DebrisVisualSize = 0.1f;

        /// <summary>爆炸特效时长（秒）。</summary>
        public const float ExplosionDuration = 0.3f;

        /// <summary>每帧秒数（3 帧 / 0.3s）。</summary>
        public const float ExplosionFrameSeconds = 0.1f;

        /// <summary>爆炸帧数（fx-explosion-0/1/2）。</summary>
        public const int ExplosionFrameCount = 3;

        /// <summary>附魔光柱时长（秒）。</summary>
        public const float EnchantDuration = 1f;

        /// <summary>光柱升起动画时长（秒）——前 0.4s 从 0.25 长到全高。</summary>
        public const float EnchantRiseSeconds = 0.4f;

        /// <summary>光柱面片全高（格）。</summary>
        public const float EnchantColumnHeight = 4.2f;

        /// <summary>光柱面片宽（格）。</summary>
        public const float EnchantColumnWidth = 1.2f;

        /// <summary>碎屑/光柱贴图缺失或注册表未注入时的兜底色（亮灰暗化，不品红）。</summary>
        public static readonly Color FallbackDebrisColor = new Color(0.58f, 0.58f, 0.58f);

        /// <summary>爆炸占位贴图缺失时的面片色（焰橙）。</summary>
        public static readonly Color ExplosionFallbackColor = new Color(0.97f, 0.61f, 0.13f, 0.9f);

        /// <summary>附魔光柱占位贴图缺失时的面片色（柱紫）。</summary>
        public static readonly Color EnchantFallbackColor = new Color(0.61f, 0.42f, 0.91f, 0.9f);

        /// <summary>槽位状态（struct 数组预分配，Tick 原地更新）。</summary>
        private struct SlotState
        {
            public FxKind Kind;
            public float Elapsed;
            public Vector3 Position;
            public Vector3 Velocity; // 碎屑抛物线初速（其它种类不用）
            public float Scale;      // 爆炸面片边长 / 碎屑边长
        }

        private readonly SlotState[] _states = new SlotState[Capacity];
        private readonly Transform[] _debrisViews = new Transform[Capacity];
        private readonly Renderer[] _debrisRenderers = new Renderer[Capacity];
        private readonly Transform[] _quadViews = new Transform[Capacity];
        private readonly Renderer[] _quadRenderers = new Renderer[Capacity];
        private readonly Material[] _quadMaterials = new Material[Capacity];
        private int _ring; // 复用指针：下一个分配槽（覆盖最旧）
        private BlockRegistry _registry;
        private Camera _camera;

        // ── 静态缓存（同进程只读一次磁盘；坏路径只碰一次） ──
        private static Texture2D[] _explosionFrames;
        private static Texture2D _enchantTexture;
        private static readonly Dictionary<string, Color?> BlockTextureColorCache = new Dictionary<string, Color?>();

        /// <summary>累计发射粒数（含已被覆盖的——测试断言「超发后视图数不涨」用）。</summary>
        public int TotalEmitted { get; private set; }

        /// <summary>当前存活粒子数（Kind != None 的槽位数）。</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Capacity; i++)
                {
                    if (_states[i].Kind != FxKind.None) n++;
                }
                return n;
            }
        }

        /// <summary>已懒建的碎屑视图数（测试断言 ≤ <see cref="Capacity"/>：超发不新建）。</summary>
        public int CreatedDebrisViews
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Capacity; i++)
                {
                    if (_debrisViews[i] != null) n++;
                }
                return n;
            }
        }

        /// <summary>注入方块注册表（碎屑颜色按方块贴图均值解析）。可省略——退兜底亮灰。</summary>
        public void Bind(BlockRegistry registry)
        {
            _registry = registry;
        }

        // ── 事件挂载（m11 W3-4 三个公开事件；OnEnable/OnDisable 对称退订） ──

        private void OnEnable()
        {
            MyWorld.Unity.Player.BlockInteraction.BlockBroken += OnBlockBroken;
            MyWorld.Core.Combat.Explosion.AfterDetonate += OnExplosion;
            MyWorld.Unity.UI.EnchantingUi.Enchanted += OnEnchanted;
        }

        private void OnDisable()
        {
            MyWorld.Unity.Player.BlockInteraction.BlockBroken -= OnBlockBroken;
            MyWorld.Core.Combat.Explosion.AfterDetonate -= OnExplosion;
            MyWorld.Unity.UI.EnchantingUi.Enchanted -= OnEnchanted;
        }

        private void OnBlockBroken(int x, int y, int z, ushort blockId)
            => EmitDigDebrisForBlock(blockId, x, y, z);

        private void OnExplosion(Float3 center, float radius)
            => PlayExplosion(new Vector3(center.X, center.Y, center.Z), radius);

        private void OnEnchanted()
            => PlayEnchantColumn(transform.position + Vector3.up * 0.1f); // 宿主=玩家（WorldBootstrap 挂玩家 GO）

        // ── 发射 API（公开给 EditMode 测试直驱；事件处理器只是薄转发） ──

        /// <summary>
        /// 挖掘碎屑入口：按方块贴图均值色在 (x,y,z) 方块中心炸 4-6 粒
        /// （粒数由坐标哈希掷点——同一次挖掘可复现，不持随机数对象）。
        /// </summary>
        public int EmitDigDebrisForBlock(ushort blockId, int x, int y, int z)
        {
            Color color = ResolveBlockColor(blockId, _registry);
            return EmitDigDebris(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), color, x * 31 + y * 7 + z);
        }

        /// <summary>
        /// 在 <paramref name="center"/> 发一批碎屑（4-6 粒，初速/偏移由 salt 哈希派生）。
        /// salt 参与掷点保证「同一次挖掘同结果」的确定性纪律。
        /// </summary>
        public int EmitDigDebris(Vector3 center, Color color, int salt)
        {
            int count = DebrisMinPerBreak + MyWorld.Unity.Environment.AtmosphereHash.PositiveMod(
                MyWorld.Unity.Environment.AtmosphereHash.Hash32(salt, 0x0EBA11),
                DebrisMaxPerBreak - DebrisMinPerBreak + 1);
            for (int i = 0; i < count; i++)
            {
                int slot = TakeSlot();
                int h1 = MyWorld.Unity.Environment.AtmosphereHash.Hash32(salt, 0xA11CE + i);
                int h2 = MyWorld.Unity.Environment.AtmosphereHash.Hash32(salt, 0x5EED0 + i);
                int h3 = MyWorld.Unity.Environment.AtmosphereHash.Hash32(salt, 0xC10D0 + i);
                var state = new SlotState
                {
                    Kind = FxKind.Debris,
                    Elapsed = 0f,
                    Position = center + new Vector3(
                        MyWorld.Unity.Environment.AtmosphereHash.Frac01(h1) * 0.6f - 0.3f,
                        MyWorld.Unity.Environment.AtmosphereHash.Frac01(h2) * 0.4f,
                        MyWorld.Unity.Environment.AtmosphereHash.Frac01(h3) * 0.6f - 0.3f),
                    Velocity = new Vector3(
                        MyWorld.Unity.Environment.AtmosphereHash.Frac01(h2) * 2.4f - 1.2f,
                        2.0f + MyWorld.Unity.Environment.AtmosphereHash.Frac01(h1) * 1.2f,
                        MyWorld.Unity.Environment.AtmosphereHash.Frac01(h3) * 2.4f - 1.2f),
                    Scale = DebrisVisualSize,
                };
                _states[slot] = state;

                Transform view = EnsureDebrisView(slot);
                _debrisRenderers[slot].sharedMaterial = UrpMaterialFactory.CreateLit(color); // 同色缓存复用
                view.SetPositionAndRotation(state.Position, Quaternion.identity);
                view.localScale = Vector3.one * DebrisVisualSize;
            }
            TotalEmitted += count;
            return count;
        }

        /// <summary>
        /// 爆炸特效：爆心一张三帧面片（0.3s），边长按破坏半径放大约 2.2 倍。
        /// 贴图 fx-explosion-0/1/2 缺失时退焰橙纯色面片（特效不消失）。
        /// </summary>
        public void PlayExplosion(Vector3 center, float radius)
        {
            int slot = TakeSlot();
            float scale = Mathf.Max(radius, 1f) * 2.2f;
            _states[slot] = new SlotState
            {
                Kind = FxKind.Explosion,
                Elapsed = 0f,
                Position = center,
                Scale = scale,
            };
            ActivateQuad(slot, center, scale, LoadExplosionFrames()[0], ExplosionFallbackColor);
            TotalEmitted++;
        }

        /// <summary>
        /// 附魔光柱：<paramref name="basePosition"/> 处竖向面片升起 1s 后淡出
        /// （贴图 magic-enchant-column 缺失时退柱紫纯色面片）。
        /// </summary>
        public void PlayEnchantColumn(Vector3 basePosition)
        {
            int slot = TakeSlot();
            _states[slot] = new SlotState
            {
                Kind = FxKind.EnchantColumn,
                Elapsed = 0f,
                Position = basePosition,
                Scale = EnchantColumnWidth,
            };
            ActivateQuad(slot, basePosition, EnchantColumnWidth, LoadEnchantTexture(), EnchantFallbackColor);
            TotalEmitted++;
        }

        // ── 推进（时间注入点；EditMode 直调，Update 喂 deltaTime） ──

        private void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// 全部存活粒子的唯一步进入口：碎屑抛物线积分 / 爆炸切帧+面片朝相机 /
        /// 光柱升长+淡出。全程无分配（材质/贴图/视图均已就位或懒建过一次）。
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_camera == null) _camera = Camera.main; // 缓存；被销毁变 fake-null 时重查

            for (int i = 0; i < Capacity; i++)
            {
                var s = _states[i];
                if (s.Kind == FxKind.None) continue;

                s.Elapsed += dt;
                float duration = DurationOf(s.Kind);
                if (s.Elapsed >= duration)
                {
                    ReleaseSlot(i);
                    continue;
                }

                switch (s.Kind)
                {
                    case FxKind.Debris:
                        TickDebris(i, ref s, dt);
                        break;
                    case FxKind.Explosion:
                        TickExplosion(i, ref s);
                        break;
                    case FxKind.EnchantColumn:
                        TickEnchantColumn(i, ref s);
                        break;
                }
                _states[i] = s;
            }
        }

        private static float DurationOf(FxKind kind)
        {
            switch (kind)
            {
                case FxKind.Debris: return DebrisDuration;
                case FxKind.Explosion: return ExplosionDuration;
                case FxKind.EnchantColumn: return EnchantDuration;
                default: return 0f;
            }
        }

        private void TickDebris(int slot, ref SlotState s, float dt)
        {
            s.Velocity.y -= DebrisGravity * dt;
            s.Position += s.Velocity * dt;
            var view = _debrisViews[slot];
            if (view == null) return;
            view.position = s.Position;
            // 尾段 0.15s 等比缩小到 0（别整粒瞬消）
            float remaining = DebrisDuration - s.Elapsed;
            float k = remaining < 0.15f ? Mathf.Max(remaining / 0.15f, 0f) : 1f;
            view.localScale = Vector3.one * (DebrisVisualSize * k);
        }

        private void TickExplosion(int slot, ref SlotState s)
        {
            var view = _quadViews[slot];
            if (view == null) return;
            // 切帧（贴图缺失时帧槽位为 null，材质留兜底色面片）
            int frame = Mathf.Min((int)(s.Elapsed / ExplosionFrameSeconds), ExplosionFrameCount - 1);
            var frames = LoadExplosionFrames();
            if (frames[frame] != null)
            {
                _quadMaterials[slot].mainTexture = frames[frame];
                _quadMaterials[slot].color = Color.white; // 贴图本色，不再乘兜底色
            }
            // 面片朝相机 + 末段微放大（爆开的呼吸感）
            if (_camera != null)
            {
                view.rotation = Quaternion.LookRotation(s.Position - _camera.transform.position);
            }
            float progress = s.Elapsed / ExplosionDuration;
            view.localScale = Vector3.one * (s.Scale * (1f + 0.1f * progress));
        }

        private void TickEnchantColumn(int slot, ref SlotState s)
        {
            var view = _quadViews[slot];
            if (view == null) return;
            // 升起：前 0.4s 从 0.25 长到全高（pivot 在面片中心 → y 随高度抬升贴地）
            float growth = Mathf.Min(s.Elapsed / EnchantRiseSeconds, 1f);
            float height01 = Mathf.Lerp(0.25f, 1f, growth);
            float height = EnchantColumnHeight * height01;
            view.localScale = new Vector3(EnchantColumnWidth, height, 1f);
            view.position = s.Position + Vector3.up * (height * 0.5f);
            // 淡出：末段 0.35s 材质色 Alpha → 0（着色器色 Alpha，两态材质约定）
            float remaining = EnchantDuration - s.Elapsed;
            float alpha = Mathf.Clamp(remaining / 0.35f, 0f, 1f);
            _quadMaterials[slot].color = new Color(1f, 1f, 1f, alpha);
            // 只绕 Y 朝相机（柱面公告板——光柱竖直，不该整体躺倒）
            if (_camera != null)
            {
                Vector3 flat = s.Position - _camera.transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 1e-6f)
                {
                    view.rotation = Quaternion.LookRotation(flat);
                }
            }
        }

        // ── 槽位与视图管理 ──

        /// <summary>取一个槽（环形指针覆盖最旧），先清掉旧占有者的视图。</summary>
        private int TakeSlot()
        {
            int slot = _ring;
            _ring = (_ring + 1) % Capacity;
            ReleaseSlot(slot);
            return slot;
        }

        private void ReleaseSlot(int slot)
        {
            _states[slot] = default;
            if (_debrisViews[slot] != null) _debrisViews[slot].gameObject.SetActive(false);
            if (_quadViews[slot] != null) _quadViews[slot].gameObject.SetActive(false);
        }

        /// <summary>懒建碎屑小方块（每槽最多建一次，之后复用；材质按发射色换）。</summary>
        private Transform EnsureDebrisView(int slot)
        {
            if (_debrisViews[slot] != null)
            {
                _debrisViews[slot].gameObject.SetActive(true);
                if (_quadViews[slot] != null) _quadViews[slot].gameObject.SetActive(false);
                return _debrisViews[slot];
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "FX碎屑" + slot;
            var collider = cube.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider); // 粒子不挡移动/射线
            cube.transform.SetParent(transform, false);
            _debrisViews[slot] = cube.transform;
            _debrisRenderers[slot] = cube.GetComponent<Renderer>();
            return cube.transform;
        }

        /// <summary>激活贴片视图（碎屑/贴片二选一：同槽互斥），并按需懒建。</summary>
        private void ActivateQuad(int slot, Vector3 position, float scale, Texture2D texture, Color fallbackTint)
        {
            if (_quadViews[slot] == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "FX贴片" + slot;
                var collider = quad.GetComponent<Collider>();
                if (collider != null) DestroyImmediate(collider);
                quad.transform.SetParent(transform, false);
                _quadViews[slot] = quad.transform;
                _quadRenderers[slot] = quad.GetComponent<Renderer>();
            }
            if (_debrisViews[slot] != null) _debrisViews[slot].gameObject.SetActive(false);
            _quadViews[slot].gameObject.SetActive(true);

            // 有贴图时 tint 用纯白（_BaseColor 与贴图相乘，别把帧染色）；
            // 缺图才落 fallbackTint 纯色面片
            Color tint = texture != null ? Color.white : fallbackTint;

            // 每槽独立材质实例（切帧/淡出只影响自己）；首建后复用同一份
            if (_quadMaterials[slot] == null)
            {
                _quadMaterials[slot] = UrpMaterialFactory.CreateTexturedOverlay(texture, tint);
                _quadRenderers[slot].sharedMaterial = _quadMaterials[slot];
            }
            else
            {
                if (texture != null) _quadMaterials[slot].mainTexture = texture;
                _quadMaterials[slot].color = tint;
            }

            _quadViews[slot].position = position;
            _quadViews[slot].rotation = Quaternion.identity;
            _quadViews[slot].localScale = Vector3.one * scale;
        }

        // ── 贴图与颜色解析（静态缓存；磁盘只碰一次） ──

        private static Texture2D[] LoadExplosionFrames()
        {
            if (_explosionFrames != null) return _explosionFrames;
            var frames = new Texture2D[ExplosionFrameCount];
            for (int i = 0; i < ExplosionFrameCount; i++)
            {
                frames[i] = LoadFxTexture("fx-explosion-" + i);
            }
            _explosionFrames = frames;
            return frames;
        }

        private static Texture2D LoadEnchantTexture()
        {
            if (_enchantTexture == null)
            {
                _enchantTexture = LoadFxTexture("magic-enchant-column");
            }
            return _enchantTexture;
        }

        /// <summary>
        /// 读 FX 贴图：优先 StreamingAssets/fx（编辑器与 standalone 都可达——m6 B3 的
        /// 教训：standalone 读不到 Assets 目录），编辑器再退 Assets/Art/Effects
        /// （正式美术后处理入库处）。找不到返回 null（调用方退纯色面片）。
        /// </summary>
        private static Texture2D LoadFxTexture(string name)
        {
            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "fx", name + ".png"),
                Path.Combine(Application.dataPath, "Art", "Effects", name + ".png"),
            };
            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    Object.Destroy(texture);
                    continue;
                }
                texture.filterMode = FilterMode.Point; // 32×32 像素风，拒绝双线性糊
                return texture;
            }
            return null;
        }

        /// <summary>
        /// 方块均值色：注册表 → 第一面贴图名 → blocks/textures PNG 像素均值（跳过全透明）。
        /// 注册表未注入 / 方块未注册 / 贴图缺失 → <see cref="FallbackDebrisColor"/>。
        /// 结果按贴图名静态缓存（含「找不到」的 null）。
        /// </summary>
        public static Color ResolveBlockColor(ushort blockId, BlockRegistry registry)
        {
            if (registry != null
                && registry.TryGetByNumericId(blockId, out var definition)
                && definition.Textures != null && definition.Textures.Length > 0
                && !string.IsNullOrEmpty(definition.Textures[0])
                && TryAverageTextureColor(definition.Textures[0], out Color color))
            {
                return color;
            }
            return FallbackDebrisColor;
        }

        /// <summary>读方块贴图求均值色（ItemDropView.TryAverageTextureColor 同款双候选路径）。</summary>
        private static bool TryAverageTextureColor(string textureName, out Color color)
        {
            color = default;
            if (BlockTextureColorCache.TryGetValue(textureName, out var cached))
            {
                if (cached.HasValue) color = cached.Value;
                return cached.HasValue;
            }

            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "blocks", "textures", textureName + ".png"),
            };
            string resolved = null;
            foreach (string candidate in candidates)
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
                    if (pixel.a == 0) continue;
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

            BlockTextureColorCache[textureName] = found;
            if (found.HasValue) color = found.Value;
            return found.HasValue;
        }

        // ── 测试读数（EditMode 断言槽位状态；Update/渲染本身无头不可见） ──

        /// <summary>第 i 槽的种类。</summary>
        public FxKind SlotKind(int i) => _states[i].Kind;

        /// <summary>第 i 槽的累计时间。</summary>
        public float SlotElapsed(int i) => _states[i].Elapsed;

        /// <summary>第 i 槽的逻辑位置（碎屑的当前物理位置；贴片为锚点）。</summary>
        public Vector3 SlotPosition(int i) => _states[i].Position;

        /// <summary>第 i 槽碎屑视图的世界坐标（抛物线轨迹观测；无视图返回零向量）。</summary>
        public Vector3 SlotViewPosition(int i) => _debrisViews[i] != null ? _debrisViews[i].position : Vector3.zero;

        /// <summary>第 i 槽爆炸当前帧号（0-2；无槽/非爆炸返回 -1）。</summary>
        public int SlotExplosionFrame(int i)
        {
            if (_states[i].Kind != FxKind.Explosion) return -1;
            return Mathf.Min((int)(_states[i].Elapsed / ExplosionFrameSeconds), ExplosionFrameCount - 1);
        }
    }
}
