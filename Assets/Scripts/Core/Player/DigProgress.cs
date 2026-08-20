using MyWorld.Core.Math;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 挖掘蓄力状态机（m12 P0-a）——按住左键累计进度，满 1 才允许破坏方块。
    /// <para>
    /// 纯数学、零 Unity 依赖，dotnet / EditMode 双链同源可测；
    /// <see cref="MyWorld.Unity.Player.BlockInteraction"/> 每帧把 dt 与目标方块喂进来。
    /// 破坏耗时本身由 <c>BlockInteraction.BreakTime</c>（门槛矩阵 + 群系倍率 +
    /// 效率附魔乘数）解算，本类只管"进度怎么攒、何时清"。
    /// </para>
    /// <para>
    /// 换目标策略（容差 0.1 格）：准星贴着方块边缘小幅抖动时，命中点会在相邻格间
    /// 来回跳——每次都清进度会让边缘方块"永远挖不动"。命中点距上次稳定锚点
    /// &lt; <see cref="RetargetTolerance"/> 视作抖动，保留原目标与进度；真换目标才清零重蓄。
    /// </para>
    /// </summary>
    public sealed class DigProgress
    {
        /// <summary>换目标容差（格）：命中点抖动小于本值时视作仍瞄着原方块，不清进度。</summary>
        public const float RetargetTolerance = 0.1f;

        private int _x;
        private int _y;
        private int _z;
        private Float3 _anchor;
        private bool _hasTarget;
        private float _fraction;

        /// <summary>当前是否有蓄力目标（松手 / 移开准星后为 false）。</summary>
        public bool HasTarget => _hasTarget;

        /// <summary>蓄力进度 0..1+（满 1 即可破坏，允许越过 1 便于断言）。</summary>
        public float Fraction => _fraction;

        /// <summary>目标方块坐标（世界格）。</summary>
        public int TargetX => _x;

        public int TargetY => _y;

        public int TargetZ => _z;

        /// <summary>满格判定：蓄力到 1 即可破坏。</summary>
        public bool ShouldBreak => _hasTarget && _fraction >= 1f;

        /// <summary>
        /// 每帧对准：同格续挖不动进度；换格但命中点距锚点 &lt; 容差 → 视作贴边抖动，
        /// 保留原目标；真换目标 → 清零重蓄。返回 true 表示目标发生了切换
        /// （调用方可用于同步重置裂纹档位等视觉）。
        /// </summary>
        public bool Aim(int x, int y, int z, Float3 hitPoint)
        {
            if (_hasTarget && x == _x && y == _y && z == _z)
            {
                return false; // 同格：进度继续攒，锚点保持首次命中点不动（防缓慢漂移蹭过边界）
            }

            if (_hasTarget && Distance(hitPoint, _anchor) < RetargetTolerance)
            {
                return false; // 贴边抖动：保留原目标与进度
            }

            bool changed = _hasTarget;
            _x = x;
            _y = y;
            _z = z;
            _anchor = hitPoint;
            _hasTarget = true;
            _fraction = 0f;
            return changed;
        }

        /// <summary>蓄力一帧：按破坏耗时归一化累加。耗时非法（≤0）按 1s 兜底防除零。</summary>
        public void Tick(float dt, float breakSeconds)
        {
            if (!_hasTarget)
            {
                return;
            }

            float seconds = breakSeconds > 0f ? breakSeconds : 1f;
            _fraction += dt / seconds;
        }

        /// <summary>清空蓄力（松手 / 移开准星 / 模态 UI 打开 / 切到攻击目标）。</summary>
        public void Reset()
        {
            _hasTarget = false;
            _fraction = 0f;
        }

        /// <summary>
        /// 按进度取破坏裂纹档位：<paramref name="stageCount"/> 档取 0..stageCount-1，
        /// 越顶钳到最后一档；无目标或零进度返回 -1（调用方据此隐藏裂纹）。
        /// </summary>
        public int CrackStage(int stageCount)
        {
            if (!_hasTarget || _fraction <= 0f || stageCount <= 0)
            {
                return -1;
            }

            int stage = (int)(_fraction * stageCount);
            return stage >= stageCount ? stageCount - 1 : stage;
        }

        private static float Distance(Float3 a, Float3 b)
        {
            Float3 d = a - b;
            return (float)System.Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        }
    }
}
