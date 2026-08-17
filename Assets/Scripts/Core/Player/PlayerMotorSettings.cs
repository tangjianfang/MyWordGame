namespace MyWorld.Core.Player
{
    /// <summary>
    /// 运动参数。默认值见 `docs/superpowers/specs/2026-08-07-player-layer-design.md` 的参数表——
    /// 那些数是**起点不是终点**，"手感太飘"这类反馈应当落到这里的具体数字上。
    /// </summary>
    public sealed class PlayerMotorSettings
    {
        public float Width { get; set; } = 0.6f;
        public float Height { get; set; } = 1.8f;
        public float EyeHeight { get; set; } = 1.62f;

        public float WalkSpeed { get; set; } = 4.3f;
        public float SprintMultiplier { get; set; } = 1.3f;

        /// <summary>m10 C1：手持装备移速加成（比例，0.05 = +5%）。夏季合金系装备
        /// 经 <c>PlayerContext.RefreshGearBonuses</c> 每帧刷新、<c>PlayerController.Tick</c>
        /// 同步进来，<see cref="PlayerMotor"/> 的水平目标速度乘 (1 + 本值)。0 = 无加成（默认）。</summary>
        public float MoveSpeedBonus { get; set; } = 0f;

        public float JumpSpeed { get; set; } = 8.4f;

        /// <summary>比现实的 −9.8 大得多——体素游戏里现实重力显得"飘"。</summary>
        public float Gravity { get; set; } = -28f;

        /// <summary>终端速度，防止长距离下落时单帧位移过大而穿透薄地板。</summary>
        public float MaxFallSpeed { get; set; } = -60f;

        public float AirControl { get; set; } = 0.35f;
        public float GroundFriction { get; set; } = 12f;
        public float AirFriction { get; set; } = 1f;

        /// <summary>能选中方块的最远距离。</summary>
        public float ReachDistance { get; set; } = 5f;

        public static PlayerMotorSettings Default => new PlayerMotorSettings();
    }
}
