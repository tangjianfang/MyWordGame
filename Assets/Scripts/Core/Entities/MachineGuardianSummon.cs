using System.Collections.Generic;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 机元守卫召唤（m11 W3-3）：图腾检测 + 已用登记。
    /// <para>
    /// 图腾 = 同一水平面内 <b>2×2 相邻的 4 块机元矿石</b>，且 y &lt;
    /// <see cref="TotemMaxYExclusive"/>（= <see cref="OreFeature.MachineEssenceMaxY"/>，
    /// 矿只生在深层的语义延伸——表层凑不出有效图腾）。右键命中 2×2 里任意一块
    /// 即检测成功，Boss 生成于图腾中心上方一格（Unity 侧 BlockInteraction 的
    /// 右键路由调这里，再经 MobManager.SpawnMobAt 落地实体）。
    /// </para>
    /// <para>纯函数 + 世界只读：不 SetBlock、不动状态——消耗无（矿石留着，挖掉即拆图腾），
    /// 防重复靠 <see cref="BossSummonState"/> 的已用登记（进存档，见该类注释）。</para>
    /// </summary>
    public static class MachineGuardianSummon
    {
        /// <summary>图腾生效的深度上限（不含）：与机元矿生成层一致（y &lt; 16）。</summary>
        public const int TotemMaxYExclusive = OreFeature.MachineEssenceMaxY;

        /// <summary>图腾边长（方块数）：2×2 共 4 块机元矿石。</summary>
        public const int TotemSize = 2;

        /// <summary>
        /// 检测右键命中的 (hitX, hitY, hitZ)（应为机元矿石）所属的 2×2 图腾。
        /// 命中不是机元矿石 / y 超门槛 / 四角不全 → false（不是完整图腾，右键落到后续路由）。
        /// 成功时 anchor = 2×2 中 x/z 最小的角（图腾键的坐标基准），
        /// spawnPos = 图腾中心上方一格（Boss 脚底落点）。
        /// <para>纯查询——不查已用登记：那由 <see cref="BossSummonState"/> 管，
        /// 调用方分两步走，方便对「图腾不完整」与「图腾已用」给不同反馈。</para>
        /// </summary>
        public static bool TryDetectTotem(World world, int hitX, int hitY, int hitZ,
            out int anchorX, out int anchorY, out int anchorZ, out Float3 spawnPos)
        {
            anchorX = anchorY = anchorZ = 0;
            spawnPos = default;
            if (world == null) return false;
            if (hitY < 0 || hitY >= TotemMaxYExclusive) return false;
            if (world.GetBlock(hitX, hitY, hitZ) != BlockIds.MachineEssenceOre) return false;

            // 命中方块可能是 2×2 的四个角之一——四个候选锚点（dx/dz ∈ {-1, 0}）逐一试，
            // 先到先得（2×2 嵌套重叠时理论上多个锚点都成立，取第一个即行为确定）
            for (int dx = -1; dx <= 0; dx++)
            {
                for (int dz = -1; dz <= 0; dz++)
                {
                    int ax = hitX + dx;
                    int az = hitZ + dz;
                    if (IsTotemAt(world, ax, hitY, az))
                    {
                        anchorX = ax;
                        anchorY = hitY;
                        anchorZ = az;
                        // 2×2 中心 = 锚点 +1（两格跨度的中点），Boss 脚底落在图腾顶面上方
                        spawnPos = new Float3(ax + 1f, hitY + 1f, az + 1f);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>图腾键："x,y,z"（anchor 角）——与 FarmStates/ChestContents 的键风格一致。</summary>
        public static string TotemKey(int anchorX, int anchorY, int anchorZ)
            => anchorX + "," + anchorY + "," + anchorZ;

        private static bool IsTotemAt(World world, int ax, int y, int az)
        {
            for (int dx = 0; dx < TotemSize; dx++)
            {
                for (int dz = 0; dz < TotemSize; dz++)
                {
                    if (world.GetBlock(ax + dx, y, az + dz) != BlockIds.MachineEssenceOre)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 已用图腾登记（m11 W3-3）：每个 2×2 图腾只出一只 Boss。
    /// <para>
    /// <b>运行时挂靠的取舍</b>（照 EnchantStore.Default 同款）：PlayerContext 是并行波次的
    /// 热点文件挂不了字段，运行时各消费方（BlockInteraction 召唤路由 / SaveLoadService
    /// 存档往返）共用 <see cref="Default"/> 单例；EditMode 测试 new 独立实例直注，不碰静态状态。
    /// </para>
    /// <para>
    /// <b>存档取舍（任务卡二选一）</b>：不搭 <see cref="MyWorld.Core.Persistence.LevelData.FarmStates"/>
    /// 的车、新开字段 <see cref="MyWorld.Core.Persistence.LevelData.UsedBossTotems"/>——
    /// 农田层的写盘是 SaveLoadService 里 FarmSystem.ExportFarmStates() 的<b>整体替换</b>
    ///（FarmSystem 未建时该字段留 null），图腾键混进去会在「没农田系统」的档里被顺手清空；
    /// 独立字段配独立的一对 Export/Import，一行不多写、语义不寄生。
    /// </para>
    /// </summary>
    public sealed class BossSummonState
    {
        /// <summary>运行时默认实例（见类注释的取舍说明）。</summary>
        public static BossSummonState Default { get; } = new BossSummonState();

        private readonly HashSet<string> _used = new HashSet<string>();

        /// <summary>已登记的图腾数（存档侧「空表不写字段」的判据）。</summary>
        public int Count => _used.Count;

        /// <summary>图腾是否已用过（键 = <see cref="MachineGuardianSummon.TotemKey"/>）。</summary>
        public bool IsUsed(string key) => _used.Contains(key);

        /// <summary>登记一个已用图腾（null/空串防御式忽略）。</summary>
        public void MarkUsed(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _used.Add(key);
        }

        /// <summary>清空（读档全量替换的第一步 / 测试隔离用）。</summary>
        public void Clear() => _used.Clear();

        /// <summary>导出为存档列表（new 列表，调用方改不动内部集合）。</summary>
        public List<string> Export() => new List<string>(_used);

        /// <summary>从存档恢复（全量替换）。null（旧档无字段）= 清空全新开始；
        /// 坏行（null/空键）跳过——与其余存档层的读容忍策略一致。</summary>
        public void Import(IEnumerable<string> keys)
        {
            _used.Clear();
            if (keys == null) return;
            foreach (string key in keys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    _used.Add(key);
                }
            }
        }
    }
}
