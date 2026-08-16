namespace MyWorld.Core.Entities
{
    /// <summary>动物 AI 状态机。</summary>
    public enum MobState
    {
        Idle,                 // 站立
        Wander,               // 漫游
        Scared,               // 被玩家吓跑（m9 A2 退役：不再进入；保留枚举值避免其后状态重编号）
        Chasing,              // 追击玩家（敌对）
        FleeingFromAttacker,  // 被攻击后逃跑
        Dying,                // 死亡中（播放动画）
        Dead,                 // 已死（待回收）
    }
}
