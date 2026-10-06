using MyWorld.Core.Blocks;
using MyWorld.Core.Lighting;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Lighting
{
    /// <summary>
    /// 玩家周界的光照采样系统（m11 W1-4 集成点②）：定时以玩家为中心重建一个
    /// 3×3 区块脚印 × <see cref="VolumeHeight"/> 层的光照体积，供刷怪等系统按
    /// 世界坐标采样真实光值——替代 MobManager 旧「白天恒 15 / 夜晚恒 0」近似。
    /// <para>
    /// 体积构建走 <see cref="WorldLightVolumeBuilder.Build"/>（契约顺序：先天光、
    /// 后填 lightEmission、再方块光）；夜间采样另建一份纯方块光体积
    /// （<see cref="WorldLightVolumeBuilder.BuildBlockLight"/>）——合成体积里
    /// 天光与方块光同字节取更亮，入夜后露天格仍是 15，剥不出火把光。
    /// </para>
    /// <para>
    /// 采样语义：白天 = max(天光, 方块光)（合成体积直接读）；夜间 = 纯方块光
    /// （露天 0、火把圈 14 递减）。落点在体积外 / 尚未建好时 <see cref="TrySampleLight"/>
    /// 返回 false，调用方（MobManager）退回昼夜相位近似——旧档启动、跨区块瞬间不炸。
    /// </para>
    /// <para>
    /// 节奏：不是每帧（体积构建是几十万格的洪泛，照 <see cref="ChunkStreamer"/> 的
    /// 毫秒预算纪律不能挤主循环），<see cref="RebuildIntervalSeconds"/> 定时重建 +
    /// <see cref="MarkDirty"/> 立即标记（方块改动方按需调用；不调也只是晚一个周期生效）。
    /// </para>
    /// </summary>
    public sealed class ChunkLightSystem : MonoBehaviour
    {
        /// <summary>定时重建间隔（秒）。2s 在「火把光刷新及时」与「洪泛开销」之间取中。</summary>
        public const float RebuildIntervalSeconds = 2f;

        /// <summary>水平脚印边长（区块数，奇数——玩家所在区块居中）。3×3 = 48×48 格。</summary>
        public const int FootprintChunks = 3;

        /// <summary>体积层数（格）。96 层覆盖「地表上下各 ~48 格」的常规活动带；
        /// 跨层峡谷/深洞边界外按 0 光降级，等玩家走近后的下一轮重建覆盖。</summary>
        public const int VolumeHeight = 96;

        private World _world;
        private BlockRegistry _registry;
        private Transform _player;

        /// <summary>白天采样用合成体积（天光+方块光同通道取更亮，构建器契约顺序的产物）。</summary>
        private WorldLightVolume _combined;

        /// <summary>夜间采样用纯方块光体积（露天读 0，火把圈读 14 递减）。</summary>
        private WorldLightVolume _blockLight;

        private float _rebuildTimer;
        private bool _dirty = true; // 挂上后第一帧就建（初始亮度判定不能等 2s）

        /// <summary>上次重建时体积的区块 origin（int.MinValue = 从未建过）——玩家挪出
        /// 脚印后体积过期，必须重建（评审 02#1/03 B-1：此前无 origin 追踪）。</summary>
        private int _lastOriginX = int.MinValue;
        private int _lastOriginZ = int.MinValue;

        /// <summary>上次重建时的 <see cref="World.EditCounter"/>——世界没变且人没挪时跳过
        /// 重建（评审 02#1/03 B-1 的核心：旧逻辑每 2s 无条件重建，挂机/主菜单白烧
        /// 83-270ms/轮 + MB 级垃圾）。</summary>
        private long _lastSeenEditCounter;

        /// <summary>重建次数（测试断言「没重建」用——不能只看 HasVolume）。</summary>
        internal int RebuildCountForTests;

        /// <summary>EditMode 注入的重建间隔（默认 <see cref="RebuildIntervalSeconds"/>）。</summary>
        internal float IntervalSecondsForTests = RebuildIntervalSeconds;

        /// <summary>EditMode 注入的帧时长（默认 <see cref="Time.deltaTime"/>）——测试用假时钟
        /// 确定性驱动节流，不依赖真实帧率。</summary>
        internal System.Func<float> DeltaTimeForTests;

        /// <summary>场景内唯一实例（静态转发用）。Awake 置、OnDestroy 清。</summary>
        private static ChunkLightSystem _instance;

        /// <summary>当前是否已有可用体积（测试 / 诊断用）。</summary>
        public bool HasVolume => _combined != null && _blockLight != null;

        private void Awake()
        {
            _instance = this;
        }

        /// <summary>方块改动方标记重建（火把放置/破坏后想立即生效就调；不调等下一周期）。
        /// 运行时多数改动方不需要显式调用——<see cref="World.EditCounter"/> 已自动覆盖
        /// 一切走 <see cref="World.SetBlock"/> 的写入（挖掘/放置/爆炸）。</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>静态转发：方块改动方无需持有本组件引用即可标记重建。
        /// 场景无光照系统（EditMode/未挂载）时安全 no-op。</summary>
        public static void MarkWorldDirty()
        {
            if (_instance != null) _instance._dirty = true;
        }

        /// <summary>注入依赖（WorldBootstrap 挂载后调用）。player 为空时组件静默 no-op。</summary>
        public void Bind(World world, BlockRegistry registry, Transform player)
        {
            _world = world;
            _registry = registry;
            _player = player;
            _dirty = true;
            _lastSeenEditCounter = 0; // Bind 即视为「世界可能变过」，下个周期先建一次
            _instance = this; // EditMode 下 AddComponent 不触发 Awake，Bind 兜底登记
        }

        private float DeltaTime() => DeltaTimeForTests != null ? DeltaTimeForTests() : Time.deltaTime;

        // internal 供 EditMode 直驱（EditMode 不自动跑 Update；Unity 运行时照常反射调用）
        internal void Update()
        {
            if (_world == null || _registry == null || _player == null) return;

            _rebuildTimer += DeltaTime();
            // 节流：至多每 IntervalSeconds 一次（脏了也先等间隔——连续挖掘不逐格重建）
            if (_rebuildTimer < IntervalSecondsForTests) return;
            _rebuildTimer = 0f;

            // 评审 02#1/03 B-1 按需门控：脏（显式标记）|| 世界变过（EditCounter，
            // 覆盖挖掘/放置/爆炸）|| 玩家挪出体积脚印——三者皆无则跳过（挂机/主菜单零重建）。
            // 已知豁免：直接 ChunkColumn.SetBlock 的旁路写入（树苗长成）不进 EditCounter，
            // 其光影延迟到下一次真实编辑或玩家移动才刷新——可接受。
            int originX = Mathf.FloorToInt(_player.position.x / VoxelCoords.ChunkSize)
                - FootprintChunks / 2;
            int originZ = Mathf.FloorToInt(_player.position.z / VoxelCoords.ChunkSize)
                - FootprintChunks / 2;
            bool originMoved = originX != _lastOriginX || originZ != _lastOriginZ;
            bool worldEdited = _world.EditCounter != _lastSeenEditCounter;
            if (!_dirty && !originMoved && !worldEdited) return;

            _dirty = false;
            _lastOriginX = originX;
            _lastOriginZ = originZ;
            _lastSeenEditCounter = _world.EditCounter;
            RebuildNow();
        }

        /// <summary>
        /// 立即重建两份体积（以玩家当前位置为中心）。公开供 EditMode 测试手动步进
        /// （EditMode 不自动跑 Update）。构建是同步的：~48×96×48 格 × 2 份。
        /// 评审 02#1/03 B-1 实测单次 83-270ms（.NET 9 下界，Mono 更慢）——正因如此，
        /// <see cref="Update"/> 已改为按需门控，本方法只在「脏/挪动/世界变过」时被调。
        /// </summary>
        public void RebuildNow()
        {
            if (_world == null || _registry == null || _player == null) return;

            RebuildCountForTests++;
            int originX = Mathf.FloorToInt(_player.position.x / VoxelCoords.ChunkSize)
                - FootprintChunks / 2;
            int originZ = Mathf.FloorToInt(_player.position.z / VoxelCoords.ChunkSize)
                - FootprintChunks / 2;
            _lastOriginX = originX;
            _lastOriginZ = originZ;
            _lastSeenEditCounter = _world.EditCounter;
            // 垂直窗口以玩家为中心，夹进世界高度域（[-64,320)——越界列 World.GetBlock 恒空气）
            int originY = Mathf.Clamp(
                Mathf.FloorToInt(_player.position.y) - VolumeHeight / 2,
                VoxelCoords.MinY,
                VoxelCoords.MaxY - VolumeHeight);

            int size = FootprintChunks * VoxelCoords.ChunkSize;
            _combined = WorldLightVolumeBuilder.Build(
                _world, _registry, originX * VoxelCoords.ChunkSize, originY,
                originZ * VoxelCoords.ChunkSize, size, VolumeHeight, size);
            _blockLight = WorldLightVolumeBuilder.BuildBlockLight(
                _world, _registry, originX * VoxelCoords.ChunkSize, originY,
                originZ * VoxelCoords.ChunkSize, size, VolumeHeight, size);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// 按世界坐标采样光照。isNight=true 返回纯方块光（露天 0），否则返回天光与方块光的合成值。
        /// 体积未建 / 坐标在脚印外返回 <b>false</b>（light 置 0），调用方退回昼夜相位近似——
        /// 读容忍，绝不抛异常。
        /// </summary>
        public bool TrySampleLight(int worldX, int worldY, int worldZ, bool isNight, out int light)
        {
            light = 0;
            if (isNight)
            {
                if (_blockLight == null || !_blockLight.Contains(worldX, worldY, worldZ)) return false;
                light = _blockLight.GetLight(worldX, worldY, worldZ);
                return true;
            }

            if (_combined == null || !_combined.Contains(worldX, worldY, worldZ)) return false;
            light = _combined.GetLight(worldX, worldY, worldZ);
            return true;
        }
    }
}
