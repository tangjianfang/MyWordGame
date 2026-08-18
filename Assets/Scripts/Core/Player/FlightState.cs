using MyWorld.Core.Math;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// m13 W1：飞行态状态机（纯 Core）。三段职责：
    /// <list type="number">
    ///   <item>记录上次「起飞键」按下的时刻（双击窗口 <see cref="DoubleTapWindowSeconds"/> 内算双击）</item>
    ///   <item>暴露当前 <see cref="Enabled"/> 态 + 切换入口 <see cref="TryToggle"/>（F 键可等效走同一条入口）</item>
    ///   <item>给定按键意图算出六向飞行速度 <see cref="ComputeVelocity"/></item>
    /// </list>
    /// <para>
    /// 设计动机：飞行切换条件不止「切换按键 down」——还要算双击窗口；飞行速度不止
    /// 「WASD 平移」，还要叠上下竖直轴。这两个数学都和 Unity 没关系，落到 Core 就能
    /// 走 <c>dotnet test</c> 全覆盖，Unity 层只负责喂按键与把结果写回 <c>PlayerState</c>。
    /// </para>
    /// <para>
    /// 存档不记：实例字段，序列化层不接触——读档走 <c>PlayerController.Bind</c> 重新
    /// <c>new</c> 一个实例，默认 <see cref="Enabled"/> = false（步行态）。这是 W1 验收
    /// 「存档不记飞行态」的入口；任何读档分支若把老 Enabled 持久化都是设计错误。
    /// </para>
    /// </summary>
    public sealed class FlightState
    {
        /// <summary>双击窗口（秒）。m13 spec 钉死 0.3s——单次按空格不切换（窗口外），
        /// 0.3s 内再按一次才算双击——这是创造模式飞行开关的核心手感，挪数会引争议。</summary>
        public const double DoubleTapWindowSeconds = 0.3;

        /// <summary>水平飞行速度（m/s）。m13 spec 钉死 8 m/s，约为走速 4.3 的 1.86 倍
        /// ——比走快但不至于飘到失重感。</summary>
        public const float HorizontalSpeed = 8f;

        /// <summary>竖直飞行速度（m/s）。上下轴用同一数——不是 W/sprint 倍率。</summary>
        public const float VerticalSpeed = 8f;

        /// <summary>当前是否在飞行态。true = 重力关、竖直/水平都走飞行合成，
        /// 掉血豁免（Unity 侧 <c>PlayerController.TakeDamage</c> 入口判定）。</summary>
        public bool Enabled { get; private set; }

        /// <summary>最近一次「起飞键」按下的时刻（秒）。外部以 <c>Time.time</c> 喂入，
        /// 用 <c>double</c> 是为了 EditMode 测试可以大数值时间戳而不丢精度。</summary>
        private double _lastTogglePressedAt = double.NegativeInfinity;

        /// <summary>尝试按一次起飞键（空格 or F 走同一条）。
        /// <paramref name="nowSeconds"/> &gt; <see cref="DoubleTapWindowSeconds"/> 内再次按 = 切换态；
        /// 单次按 = 只更新窗口时间，不切换（必须连按两次）。
        /// 返回值：true 表示状态发生了切换（含开启/关闭）；false 表示只更新了窗口时间。
        /// <para>需要从「步行」切到「飞行」时也得双击——避免玩家扔掉键盘瞬时按一个键就起飞。</para></summary>
        public bool TryToggle(double nowSeconds)
        {
            if (Enabled)
            {
                // 飞行态下再按一次：窗口不关心，直接切回步行（落地恢复由 Unity 侧
                // 与 <see cref="OnLanded"/> 协作——本方法不替 Unity 做掉地检测）。
                Enabled = false;
                _lastTogglePressedAt = nowSeconds;
                return true;
            }

            // 步行态：必须窗口内连按两次才切换。第一次只刷窗口时间。
            if (nowSeconds - _lastTogglePressedAt <= DoubleTapWindowSeconds)
            {
                Enabled = true;
                _lastTogglePressedAt = nowSeconds;
                return true;
            }

            _lastTogglePressedAt = nowSeconds;
            return false;
        }

        /// <summary>F 键（CreateMenu 同款纯按键）一次 = 等效一次「起飞键按下」。
        /// 同样走双击窗口，不绕过窗口——spec 明确「F 键等效」。
        /// 返回值含义同 <see cref="TryToggle"/>。</summary>
        public bool TryToggleViaKey(double nowSeconds) => TryToggle(nowSeconds);

        /// <summary>强制退出（触地时调用）。Unity 层落地检测到 <c>IsGrounded = true</c> 时
        /// 走这里——特化于「飞行态下被叫醒才动」：步行态调用 no-op。返回 true 表示本次
        /// 发生了退出（Unity 层据此通知 HUD）。</summary>
        public bool OnLanded()
        {
            if (!Enabled) return false;
            Enabled = false;
            // 触地退出后窗口仍然要刷新：玩家想再起飞必须重新双击——「触地即就地站定」
            // 的语义里，连点两下起飞的旧窗口不该延命
            _lastTogglePressedAt = double.NegativeInfinity;
            return true;
        }

        /// <summary>六向速度合成：水平 WASD（方向已旋转到世界空间）+ 空格升 +
        /// Shift 降。方向向量各自独立归一化（水平按方向长度，竖直按 ±1f 各自贡献）。
        /// <paramref name="moveDirectionXZ"/> 是 Unity 层旋转相机到世界空间后的水平
        /// 方向（Unity 侧已经归一化到 |.| ≤ 1，本函数不再缩放；给 0 表示水平无输入）。
        /// 返回的速度直接写进 <c>PlayerState.Velocity</c>——飞行态下不走
        /// <see cref="PlayerMotor"/> 的摩擦收敛，而是「按 dt 直接累加」（Unity 侧实现）。
        /// <para>
        /// 关键不变量：单轴输入返回值 ≤ <see cref="HorizontalSpeed"/>/<see cref="VerticalSpeed"/>；
        /// 水平归一化前 |dz| > 1 视为非法（Unity 侧 ReadInput 已 clamp），
        /// 所以这里不再做长度裁剪。</para>
        /// </summary>
        public static Float3 ComputeVelocity(Float3 moveDirectionXZ, bool ascend, bool descend)
        {
            // 水平分量 = 水平方向 × 水平速度。无输入时方向 = (0,0,0) → 速度 (0,0,0)。
            Float3 horizontal = new Float3(
                moveDirectionXZ.X * HorizontalSpeed,
                0f,
                moveDirectionXZ.Z * HorizontalSpeed);

            // 竖直分量：同时按 Space + Shift = 相互抵消（与玩家直觉一致），
            // 不是「取较大者」也不是「累加到 2 倍」——避免升+降同时按键秒窜天
            float vertical;
            if (ascend && descend) vertical = 0f;
            else if (ascend) vertical = VerticalSpeed;
            else if (descend) vertical = -VerticalSpeed;
            else vertical = 0f;

            return new Float3(horizontal.X, vertical, horizontal.Z);
        }
    }
}
