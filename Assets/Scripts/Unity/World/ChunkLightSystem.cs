using MyWorld.Core.Blocks;
using MyWorld.Core.Lighting;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.World
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

        /// <summary>当前是否已有可用体积（测试 / 诊断用）。</summary>
        public bool HasVolume => _combined != null && _blockLight != null;

        /// <summary>注入依赖（WorldBootstrap 挂载后调用）。player 为空时组件静默 no-op。</summary>
        public void Bind(World world, BlockRegistry registry, Transform player)
        {
            _world = world;
            _registry = registry;
            _player = player;
            _dirty = true;
        }

        /// <summary>方块改动方标记重建（火把放置/破坏后想立即生效就调；不调等下一周期）。</summary>
        public void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (_world == null || _registry == null || _player == null) return;

            _rebuildTimer += Time.deltaTime;
            if (!_dirty && _rebuildTimer < RebuildIntervalSeconds) return;

            _rebuildTimer = 0f;
            _dirty = false;
            RebuildNow();
        }

        /// <summary>
        /// 立即重建两份体积（以玩家当前位置为中心）。公开供 EditMode 测试手动步进
        /// （EditMode 不自动跑 Update）。构建是同步的：~48×96×48 格 × 2 份，
        /// 单次毫秒级，2s 一次不挤帧预算。
        /// </summary>
        public void RebuildNow()
        {
            if (_world == null || _registry == null || _player == null) return;

            int originX = Mathf.FloorToInt(_player.position.x / VoxelCoords.ChunkSize)
                - FootprintChunks / 2;
            int originZ = Mathf.FloorToInt(_player.position.z / VoxelCoords.ChunkSize)
                - FootprintChunks / 2;
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
