using MyWorld.Core.Time;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.Environment
{
    /// <summary>
    /// m11 W3-4：天气状态机（确定性雨/雪窗）+ 屏幕粒子层。**纯视觉**——不写任何玩法状态，
    /// 雨雪不影响移动/农耕/刷怪（音轨的 amb 挑选也不读它，av 赛道自有通道）。
    /// <para>
    /// <b>雨雪窗派生</b>：从世界 seed 整数哈希出「雨日链」——第 n 场雨之后隔
    /// <see cref="MinGapDays"/>~<see cref="MaxGapDays"/> 天（3-5 游戏日）是第 n+1 场；
    /// 雨日当天从 <see cref="WindowStartTick"/> 起连下 <see cref="WindowDurationTicks"/>
    /// （0.5 日 = 12000 tick），窗口上限锁 23000 tick 以内<b>绝不跨日</b>。
    /// 同一 seed 任一天问两次结果一致（确定性铁律，不持随机数对象）。
    /// </para>
    /// <para>
    /// <b>状态机</b>：<see cref="Update"/> 每帧读 <see cref="PlayerContext.Time"/> 的
    /// <see cref="TimeOfDay.CurrentTick"/>——发现回绕（当前值比上一帧小）即跨日，
    /// 本地日计数 +1（TimeOfDay 只存一天内的 tick，没有绝对日号；读档回拨的极小概率
    /// 由「只认变小」容忍）。窗口内每帧查玩家所在群系：<see cref="Biome.Snow"/> 下雪、
    /// 其余下雨——群系查询走 <see cref="WorldGenerator.BiomeAt"/>（纯噪声采样，零分配）。
    /// </para>
    /// <para>
    /// <b>渲染</b>：IMGUI 半透明层——雨=竖丝（2px 宽 18-32px 长），雪=慢速下落 +
    /// 正弦横摆的小方点，共 <see cref="MaxParticles"/> 60 粒、数组预分配、
    /// 确定性哈希初始化，<see cref="OnGUI"/> 不活跃时第一行就 return（平时零开销）。
    /// 60 粒按「屏幕归一化坐标」推进，与分辨率无关。
    /// </para>
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        /// <summary>降水种类。None=晴（窗口外）。</summary>
        public enum PrecipKind
        {
            /// <summary>晴——窗口外恒为它。</summary>
            None = 0,

            /// <summary>雨（Snow 群系以外的雨日窗口）。</summary>
            Rain,

            /// <summary>雪（玩家正处 Snow 群系的雨日窗口）。</summary>
            Snow,
        }

        /// <summary>屏幕粒子上限（雨雪共用一套确定性位置，画法不同）。</summary>
        public const int MaxParticles = 60;

        /// <summary>一场雨/雪持续 0.5 游戏日（12000 tick）。</summary>
        public const float WindowDurationTicks = 12000f;

        /// <summary>两场雨的最少间隔（游戏日）。</summary>
        public const int MinGapDays = 3;

        /// <summary>两场雨的最大间隔（游戏日）。</summary>
        public const int MaxGapDays = 5;

        /// <summary>雨窗起始 tick 的下限（窗口终点 ≤ 23000，不跨日）。</summary>
        public const float WindowStartTickMin = 1000f;

        /// <summary>雨丝颜色（半透明蓝灰）。</summary>
        public static readonly Color RainStreakColor = new Color(0.62f, 0.72f, 0.92f, 0.26f);

        /// <summary>雪花颜色（半透明白）。</summary>
        public static readonly Color SnowFlakeColor = new Color(0.95f, 0.97f, 1.00f, 0.55f);

        private WorldGenerator _generator;
        private Transform _player;
        private int _seed;
        private int _dayIndex;
        private float _lastTick = -1f;
        private bool _windowActive;
        private PrecipKind _kind = PrecipKind.None;
        private float _visualTime;

        /// <summary>当前降水（窗口外恒 None；窗口内按玩家群系取 Rain/Snow）。</summary>
        public PrecipKind Current => _windowActive ? _kind : PrecipKind.None;

        /// <summary>是否正处雨雪窗（先于群系判定——群系只决定下雨还是下雪）。</summary>
        public bool WindowActive => _windowActive;

        /// <summary>本地日计数（从挂载起累计的跨日次数，读档后重置——纯视觉可接受）。</summary>
        public int DayIndex => _dayIndex;

        /// <summary>屏幕粒子（归一化坐标 + 确定性速度/相位）。预分配，运行期零 new。</summary>
        private struct FallParticle
        {
            public float X01;
            public float Y01;
            public float Speed01;
            public float Size01;
            public float Phase;
        }

        private readonly FallParticle[] _particles = new FallParticle[MaxParticles];

        /// <summary>
        /// 装配：世界生成器（群系查询）+ 玩家 transform（取玩家所在群系）+ 世界 seed
        /// （雨日链派生）。generator/player 传 null 也能跑——群系查询退化为恒下雨。
        /// </summary>
        public void Bind(WorldGenerator generator, Transform player, int seed)
        {
            _generator = generator;
            _player = player;
            _seed = seed;
            BuildParticles();
        }

        private void BuildParticles()
        {
            // 60 粒确定性初始化（整数哈希，不持随机数对象）：雨雪共用同一套位置/速度种子，
            // 画法不同——雨=竖丝快落，雪=慢落 + 横摆。挂载时建一次，之后原地更新。
            for (int i = 0; i < MaxParticles; i++)
            {
                _particles[i] = new FallParticle
                {
                    X01 = AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0x51A7)),
                    Y01 = AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0x77AA)),
                    Speed01 = AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0x5EED)),
                    Size01 = AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0xC1A2)),
                    Phase = AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0x9A17)) * (Mathf.PI * 2f),
                };
            }
        }

        private void Update()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Time == null) return;
            TickWeather(ctx.Time.CurrentTick, Time.deltaTime);
        }

        /// <summary>
        /// 状态机唯一推进入口（时间注入点）：跨日检测 → 窗口判定 → 群系定雨雪 → 粒子步进。
        /// EditMode 测试直调注入 tick/dt（EditMode 下 Update/OnGUI 不回调）。
        /// </summary>
        public void TickWeather(float currentTick, float dt)
        {
            // 跨日：TimeOfDay 到 24000 回绕，当前值比上次小 = 过了午夜。
            // 首帧（_lastTick<0）只记基线不加日号。
            if (_lastTick >= 0f && currentTick < _lastTick)
            {
                _dayIndex++;
            }
            _lastTick = currentTick;

            _windowActive = IsWindowActive(_seed, _dayIndex, currentTick);
            if (!_windowActive)
            {
                _kind = PrecipKind.None;
                return; // 晴天：粒子不动（下次进窗从原地续落，视觉无感）
            }

            _kind = PlayerInSnowBiome() ? PrecipKind.Snow : PrecipKind.Rain;
            if (dt > 0f)
            {
                UpdateParticles(dt);
            }
        }

        /// <summary>玩家是否在 Snow 群系（查不到生成器/玩家时按「不是雪」处理 → 下雨）。</summary>
        private bool PlayerInSnowBiome()
        {
            if (_generator == null) return false;
            Vector3 p = _player != null ? _player.position : transform.position;
            return _generator.BiomeAt((int)p.x, (int)p.z) == Biome.Snow;
        }

        private void UpdateParticles(float dt)
        {
            bool snow = _kind == PrecipKind.Snow;
            _visualTime += dt;
            for (int i = 0; i < MaxParticles; i++)
            {
                // 雨快（0.90-1.40 屏/秒）雪慢（0.10-0.18 屏/秒），同一 Speed01 种子取不同区间
                var p = _particles[i];
                float speed = snow ? 0.10f + p.Speed01 * 0.08f : 0.90f + p.Speed01 * 0.50f;
                p.Y01 += speed * dt;
                if (p.Y01 >= 1f) p.Y01 -= 1f;

                if (snow)
                {
                    // 雪的横摆：正弦 + 逐粒相位（绝对相位而非逐帧累加，帧率无关）
                    p.X01 += Mathf.Sin(_visualTime * 1.6f + p.Phase) * 0.05f * dt;
                    if (p.X01 < 0f) p.X01 += 1f;
                    else if (p.X01 >= 1f) p.X01 -= 1f;
                }
                _particles[i] = p;
            }
        }

        private void OnGUI()
        {
            if (!_windowActive || _kind == PrecipKind.None) return;

            float w = Screen.width;
            float h = Screen.height;
            Color previous = GUI.color;
            if (_kind == PrecipKind.Rain)
            {
                GUI.color = RainStreakColor;
                for (int i = 0; i < MaxParticles; i++)
                {
                    var p = _particles[i];
                    float length = 18f + p.Size01 * 14f;
                    GUI.DrawTexture(new Rect(p.X01 * w, p.Y01 * h, 2f, length), Texture2D.whiteTexture);
                }
            }
            else
            {
                GUI.color = SnowFlakeColor;
                for (int i = 0; i < MaxParticles; i++)
                {
                    var p = _particles[i];
                    float size = 3f + p.Size01 * 3f;
                    GUI.DrawTexture(new Rect(p.X01 * w, p.Y01 * h, size, size), Texture2D.whiteTexture);
                }
            }
            GUI.color = previous;
        }

        // ── 测试读数（EditMode 驱动 TickWeather 后断言粒子状态；OnGUI 本身无头不跑） ──

        /// <summary>第 i 粒的归一化 X（雪摆动可观测）。</summary>
        public float ParticleX01(int i) => _particles[i].X01;

        /// <summary>第 i 粒的归一化 Y（下落可观测）。</summary>
        public float ParticleY01(int i) => _particles[i].Y01;

        // ── 确定性窗口派生（纯静态，EditMode 直接断言；与实例状态无关） ──

        /// <summary>世界 seed 的第一场雨所在游戏日（3-5 日内）。</summary>
        public static int FirstRainDay(int seed)
            => MinGapDays + AtmosphereHash.PositiveMod(
                AtmosphereHash.Hash32(seed, unchecked((int)0x1C0FFEE)), MaxGapDays - MinGapDays + 1);

        /// <summary>第 n 场雨（rainDay）之后下一场的日子——间隔 3-5 日。</summary>
        public static int NextRainDay(int seed, int rainDay)
            => rainDay + MinGapDays + AtmosphereHash.PositiveMod(
                AtmosphereHash.Hash32(seed, rainDay), MaxGapDays - MinGapDays + 1);

        /// <summary>
        /// dayIndex 是否雨日：沿雨日链从第一天走到 ≥ dayIndex（间隔 ≥3，
        /// 实测几十天内个位数迭代；结果只依赖 seed 与 dayIndex，与调用时机无关）。
        /// </summary>
        public static bool IsRainDay(int seed, int dayIndex)
        {
            if (dayIndex < 0) return false;
            int d = FirstRainDay(seed);
            while (d < dayIndex)
            {
                d = NextRainDay(seed, d);
            }
            return d == dayIndex;
        }

        /// <summary>雨日在当天内的窗口起点 tick（1000-10999 → 窗口终点 ≤ 23000 不跨日）。</summary>
        public static float WindowStartTick(int seed, int dayIndex)
            => WindowStartTickMin + AtmosphereHash.PositiveMod(
                AtmosphereHash.Hash32(seed, dayIndex ^ 0x2A2A2A), 10000);

        /// <summary>tick 是否落在 dayIndex 当天的雨雪窗内。</summary>
        public static bool IsWindowActive(int seed, int dayIndex, float tick)
        {
            if (!IsRainDay(seed, dayIndex)) return false;
            float start = WindowStartTick(seed, dayIndex);
            return tick >= start && tick < start + WindowDurationTicks;
        }
    }

    /// <summary>
    /// m11 W3-4：Unity 层共享的确定性整数哈希小工具（WeatherSystem / CloudLayer /
    /// FX.ParticlePool 三家共用；Core 层的 ValueNoise2D 哈希是 private 不能跨层引用）。
    /// 与 gen_m10_*.py / Core 整数哈希同一纪律：不持随机数对象、同输入同输出。
    /// </summary>
    internal static class AtmosphereHash
    {
        /// <summary>32 位整数哈希（两路输入混合）。</summary>
        public static int Hash32(int a, int b)
        {
            unchecked
            {
                int n = a * 374761393 + b * 668265263;
                n = (n ^ (n >> 13)) * 1274126177;
                return n ^ (n >> 16);
            }
        }

        /// <summary>非负取模（% 在负数上向零取整，这里统一折回 [0, m)）。</summary>
        public static int PositiveMod(int value, int m)
        {
            int r = value % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>哈希折成 [0,1) 的小数（65536 级量化，够视觉用且完全确定）。</summary>
        public static float Frac01(int hash) => PositiveMod(hash, 65536) / 65536f;
    }
}
