using System.Collections.Generic;

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
    }

    /// <summary>玩家快照：位置/速度/生命/饥饿/经验/背包全部 36 槽。</summary>
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
