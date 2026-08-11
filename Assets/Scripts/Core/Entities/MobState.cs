namespace MyWorld.Core.Entities
{
    /// <summary>动物 AI 状态机。</summary>
    public enum MobState
    {
        Idle,                 // 站立
        Wander,               // 漫游
        Scared,               // 被玩家吓跑
        Chasing,              // 追击玩家（敌对）
        FleeingFromAttacker,  // 被攻击后逃跑
        Dying,                // 死亡中（播放动画）
        Dead,                 // 已死（待回收）
    }
}
