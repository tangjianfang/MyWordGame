using System;
using System.Collections.Generic;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Blocks
{
    /// <summary>
    /// 床脚朝向（m11 W1-4）。放置时由玩家视向决定，<b>头格 = 脚格 + 朝向一格</b>：
    /// North=+Z / South=-Z / East=+X / West=-X（与 MobModels「面朝 +Z」的轴约定同源）。
    /// </summary>
    public enum BedFacing : byte
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3,
    }

    /// <summary>睡觉结果（m11 W1-4）。</summary>
    public enum SleepResult
    {
        /// <summary>睡成了：时间已跳到早晨 0 tick，重生点已设到该床。</summary>
        Slept,

        /// <summary>不是夜间（白天/黄昏不能睡，与 MC 一致）。</summary>
        NotNight,

        /// <summary>那格已经不是床（被拆了/记错位置）。</summary>
        BedMissing,
    }

    /// <summary>
    /// 床系统（m11 W1-4）。Core 只管三件事：两格放置与脚朝向、夜间睡觉跳早晨并设重生点、
    /// 重生点列表进 <see cref="LevelData.BedSpawnPoints"/> 往返。
    /// <para>
    /// <b>两格放置</b>：<see cref="PlaceBed"/> 在脚格与头格各落一个 bed 方块（World.SetBlock 单格能力
    /// 调两次），头格被占则整体不放（不留半张床）。渲染层的头/脚造型差异、以及「床只有半格高」的
    /// 非整格碰撞，都留给 Unity 侧（集成点②）——Core 侧 bed 方块按整格实心简化。
    /// </para>
    /// <para>
    /// <b>脚朝向取舍</b>：朝向只存运行时内存（<c>_facings</c>），<b>不进存档</b>——LevelData 的
    /// <c>BedSpawnPoints</c> schema 已被 I3 冻结为 <c>List&lt;Float3&gt;</c>（只有坐标没有方向位）。
    /// 后果：读档后已知朝向丢失、<see cref="GetFacing"/> 回落 <see cref="BedFacing.North"/>，但睡觉/
    /// 重生行为完全不受影响（重生点只依赖坐标）。要持久化朝向需 I3 再扩 schema，留给后续波次。
    /// </para>
    /// <para>
    /// <b>重生点约定</b>：放床只登记进列表（<see cref="SpawnPoints"/>），睡过才把该床挪到列表末尾并
    /// 成为 <see cref="RespawnPoint"/>——存档约定「最后一条 = 当前重生点」，玩家死亡复活走 Unity 侧
    /// <c>RespawnAtSpawn</c> 读它（接线在集成点②）。
    /// </para>
    /// </summary>
    public sealed class BedSystem
    {
        private readonly ushort _bedBlockId;
        private readonly List<Float3> _spawnPoints = new List<Float3>();
        private readonly Dictionary<string, BedFacing> _facings = new Dictionary<string, BedFacing>(StringComparer.Ordinal);
        /// <summary>当前重生点。只在 <see cref="Sleep"/> 成功时设置——放床只登记不生效。</summary>
        private Float3? _respawnPoint;

        public BedSystem(BlockRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            _bedBlockId = registry.GetById("bed").NumericId;
        }

        /// <summary>当前重生点（最后<b>睡过</b>的床；从未睡过为 null——放床不算，与 MC 一致）。</summary>
        public Float3? RespawnPoint => _respawnPoint;

        /// <summary>已登记的床重生点列表（末条 = 当前重生点）。供存档快照与 UI 列表只读。</summary>
        public IReadOnlyList<Float3> SpawnPoints => _spawnPoints;

        /// <summary>
        /// 放床：脚格 + 头格（脚格沿朝向偏移一格）各落一个 bed 方块，并登记重生点。
        /// 头格或脚格已有非空气方块时整体拒绝（不落任何方块、不登记）。
        /// </summary>
        public bool PlaceBed(World world, int x, int y, int z, BedFacing facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            (int hx, int hy, int hz) = HeadOffset(facing);
            if (world.GetBlock(x, y, z) != ChunkSection.AirId
                || world.GetBlock(x + hx, y + hy, z + hz) != ChunkSection.AirId)
            {
                return false;
            }

            world.SetBlock(x, y, z, _bedBlockId);
            world.SetBlock(x + hx, y + hy, z + hz, _bedBlockId);
            _facings[Key(x, y, z)] = facing;
            RegisterBed(x, y, z);
            return true;
        }

        /// <summary>
        /// 只登记重生点、不占格（单格简化路径：若调用方选择「一格床」摆法，直接用这个；
        /// 两格摆法走 <see cref="PlaceBed"/>，它会顺带登记）。
        /// 同一坐标重复登记去重。
        /// </summary>
        public void RegisterBed(int x, int y, int z)
        {
            Float3 spawn = SpawnPointAt(x, y, z);
            if (!_spawnPoints.Contains(spawn))
            {
                _spawnPoints.Add(spawn);
            }
        }

        /// <summary>查脚格朝向（读档后未记录回落 <see cref="BedFacing.North"/>，取舍见类注释）。</summary>
        public BedFacing GetFacing(int x, int y, int z)
            => _facings.TryGetValue(Key(x, y, z), out BedFacing facing) ? facing : BedFacing.North;

        /// <summary>
        /// 睡觉。判定顺序：先「是否夜间」（白天连床都谈不上），再「那格还是不是床」。
        /// 睡成：时间直接跳到早晨 <c>0</c> tick，该床重生点挪到列表末尾（成为当前重生点）。
        /// 床头格被拆、脚格还在的退化情形按可睡处理（床的锚点是脚格）。
        /// </summary>
        public SleepResult Sleep(World world, int x, int y, int z, TimeOfDay time)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (time == null)
            {
                throw new ArgumentNullException(nameof(time));
            }

            if (!time.IsNight)
            {
                return SleepResult.NotNight;
            }

            if (world.GetBlock(x, y, z) != _bedBlockId)
            {
                return SleepResult.BedMissing;
            }

            time.CurrentTick = 0f;

            Float3 spawn = SpawnPointAt(x, y, z);
            _spawnPoints.Remove(spawn);
            _spawnPoints.Add(spawn);
            _respawnPoint = spawn;
            return SleepResult.Slept;
        }

        /// <summary>
        /// 全量写入 level.dat（替换 BedSpawnPoints）。落盘前把当前重生点挪到列表末尾，
        /// 维持「末条 = 当前重生点」的存档约定（schema 只有坐标列表、没有独立激活位）。
        /// </summary>
        public void SaveTo(LevelData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var ordered = new List<Float3>(_spawnPoints);
            if (_respawnPoint.HasValue)
            {
                ordered.Remove(_respawnPoint.Value);
                ordered.Add(_respawnPoint.Value);
            }

            data.BedSpawnPoints = ordered;
        }

        /// <summary>
        /// 从 level.dat 恢复：列表原样回填，<see cref="RespawnPoint"/> 取末条。
        /// 已知取舍：「放过床但从未睡过」的档（列表非空、无激活床）读回后会把末张床当
        /// 重生点——schema 冻结在纯坐标列表，没有「是否睡过」位，此边缘差异可接受
        /// （真睡过夜的档不受影响，SaveTo 已把激活床排末）。
        /// </summary>
        public void LoadFrom(LevelData data)
        {
            _spawnPoints.Clear();
            _facings.Clear();
            _respawnPoint = null;
            if (data?.BedSpawnPoints == null)
            {
                return;
            }

            _spawnPoints.AddRange(data.BedSpawnPoints);
            if (_spawnPoints.Count > 0)
            {
                _respawnPoint = _spawnPoints[_spawnPoints.Count - 1];
            }
        }

        /// <summary>重生点坐标：脚格中心、床面上一格——床是实心方块，直接站脚格里会卡模型。</summary>
        private static Float3 SpawnPointAt(int x, int y, int z) => new Float3(x + 0.5f, y + 1f, z + 0.5f);

        private static (int X, int Y, int Z) HeadOffset(BedFacing facing)
        {
            switch (facing)
            {
                case BedFacing.North: return (0, 0, 1);
                case BedFacing.South: return (0, 0, -1);
                case BedFacing.East: return (1, 0, 0);
                case BedFacing.West: return (-1, 0, 0);
                default: return (0, 0, 1);
            }
        }

        private static string Key(int x, int y, int z) => $"{x},{y},{z}";
    }
}
