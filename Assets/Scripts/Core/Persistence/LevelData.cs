using System.Collections.Generic;
using MyWorld.Core.Math;

namespace MyWorld.Core.Persistence
{
    /// <summary>level.dat 根对象：玩家全套 + 世界轻量状态（milestone-4 spec）。</summary>
    public class LevelData
    {
        /// <summary>与 bootstrap seed 校验一致才恢复，防串档。</summary>
        public long Seed;
        /// <summary>TimeOfDay.CurrentTick（float，0..24000）。</summary>
        public float TimeTick;
        public PlayerSnapshot Player;
        public FurnaceSnapshot Furnace;
        /// <summary>地面掉落物。SpawnTime 不存——恢复时统一赋当前时间，宽限期重新计时。</summary>
        public List<DropSnapshot> Drops;
        /// <summary>引导任务链进度（m6 C4）。旧档缺此字段时 Newtonsoft 反序列化得 null，
        /// 恢复侧按 null 跳过 = 任务链全新开始，天然向后兼容。</summary>
        public MyWorld.Core.Quests.QuestState Quest;
        /// <summary>多章节任务书进度（m11 W2-4）：每章一个 QuestState、顺序与章节文件一致。
        /// 旧档缺此字段时为 null——恢复侧回退读上面的单章 Quest 字段（RestoreLegacy）；
        /// 本字段**刻意不做缺键归一**（null 是「旧档、走兼容路径」的判别信号）。
        /// 写盘侧两个字段都写（Quest = 当前活动章快照，兼容旧版读档）。</summary>
        public List<MyWorld.Core.Quests.QuestState> QuestChapters;
        /// <summary>箱子内容（m11 I3）。key = "x,y,z" 箱子坐标；条目复用掉落物快照结构
        /// <see cref="DropSnapshot"/>（ItemId/Count/Metadata 有效，X/Y/Z 不用）。
        /// 可空字段：旧档缺键时 <see cref="LevelDataCodec.Load"/> 统一归一为空字典。</summary>
        public Dictionary<string, List<DropSnapshot>> ChestContents;
        /// <summary>已激活床的重生点（m11 I3）。可空：旧档缺键归一为空列表。</summary>
        public List<Float3> BedSpawnPoints;
        /// <summary>玩家附魔（m11 I3）。key = 槽位/物品 uid，value = 附魔 id。可空：旧档缺键归一为空字典。</summary>
        public Dictionary<string, string> PlayerEnchantments;
        /// <summary>成就统计（m11 I3）：击杀数/挖掘数/时长秒等。可空：旧档缺键归一为空字典。</summary>
        public Dictionary<string, int> Stats;
        /// <summary>作物状态（m11 I3）。key = "x,y,z" 农田坐标，value = 作物状态串。可空：旧档缺键归一为空字典。</summary>
        public Dictionary<string, string> FarmStates;
        /// <summary>已用 Boss 图腾（m11 W3-3）。元素 = "x,y,z" 图腾 anchor 角键
        ///（<see cref="MyWorld.Core.Entities.MachineGuardianSummon.TotemKey"/>），
        /// 每个图腾只召唤一次机元守卫。可空：旧档缺键归一为空列表。
        /// 不复用 FarmStates 的取舍见 <c>BossSummonState</c> 类注释（农田层整体替换会清掉寄生键）。</summary>
        public List<string> UsedBossTotems;
    }

    /// <summary>玩家快照：位置/速度/生命/饥饿/经验/背包全部 36 槽 + 穿戴栏 4 槽（m11 W2-1）。</summary>
    public class PlayerSnapshot
    {
        public float X, Y, Z;
        public float VX, VY, VZ;
        public float HealthCurrent, HealthMax;
        public int Hunger;
        public float Saturation;
        public int ExpCurrent, ExpLevel;
        public int SelectedHotbarIndex;
        public SlotSnapshot[] Slots;

        /// <summary>穿戴栏（头/胸/腿/脚，m11 W2-1）。旧档缺此字段时 Newtonsoft 反序列化得 null，
        /// <see cref="LevelDataCodec"/> 归一为空数组 = 空穿戴（照 I3「缺键 → 空集合」兼容模式）。</summary>
        public SlotSnapshot[] ArmorSlots;
    }

    /// <summary>单个物品槽。Metadata 必须保存：工具耐久存在这里。</summary>
    public class SlotSnapshot
    {
        public int ItemId;
        public int Count;
        public ushort Metadata;
    }

    /// <summary>熔炉快照。Input/Fuel/Output 任一可为 null 表示空槽。</summary>
    public class FurnaceSnapshot
    {
        public SlotSnapshot Input;
        public SlotSnapshot Fuel;
        public SlotSnapshot Output;
        public float Progress;
        /// <summary>剩余燃料燃烧时间（秒）。不存的话读档后火会灭，得重新投燃料——行为错误。</summary>
        public float FuelRemaining;
    }

    /// <summary>地面掉落物快照。</summary>
    public class DropSnapshot
    {
        public int ItemId;
        public int Count;
        public ushort Metadata;
        public float X, Y, Z;
    }
}
