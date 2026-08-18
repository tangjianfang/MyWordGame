using MyWorld.Core.Math;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// m13 W1：飞行态状态机的纯数学契约测试（双链 dotnet + EditMode 共用）。
    /// 不依赖 UnityEngine 因此 EditMode 与 dotnet 走同一份断言。
    /// <para>覆盖：
    /// 切换（双击窗口 0.3s 内才算双击）、F 键等效（不绕过双击窗口）、
    /// 触地退出（OnLanded 在 Enabled 时退出 + 清窗口）、六向速度合成
    /// （WASD + 空格升 + Shift 降）、同时按升+降抵消。</para>
    /// </summary>
    [TestFixture]
    public class FlightStateTests
    {
        // ─── 切换 ──────────────────────────────────────────────────────────

        [Test]
        public void TryToggle_单次按不切换_必须等窗口内第二下()
        {
            // 单次按 = 0.1s（窗口内）也不切换；必须连按两次才进入飞行态
            var s = new FlightState();
            Assert.That(s.Enabled, Is.False, "新实例默认步行");
            bool first = s.TryToggle(0.0);
            Assert.That(first, Is.False, "第一次按只更新窗口，不切换");
            Assert.That(s.Enabled, Is.False);
            bool second = s.TryToggle(0.1);
            Assert.That(second, Is.True, "0.1s < 0.3s 窗口内第二下进入飞行");
            Assert.That(s.Enabled, Is.True);
        }

        [Test]
        public void TryToggle_窗口外第二次不切换_随后窗口被重置()
        {
            // 0.0 第一次按 → _lastTogglePressedAt = 0.0；0.5 再按（窗口外 0.5 > 0.3）
            // → 不切换但 _lastTogglePressedAt = 0.5；第三次 0.6 按 = 离 0.5 仅 0.1s → 切到飞行。
            // 这条契约是说「窗口外的第二次不会立即切，但窗口记的是当下」——避免玩家
            // 半秒前按过空格，等想起再立刻按一下就起飞的不合理手感。
            var s = new FlightState();
            s.TryToggle(0.0);                 // 第一次：0.0
            bool second = s.TryToggle(0.5);   // 窗口外 0.5 > 0.3 → 不切但重置窗口
            Assert.That(second, Is.False, "0.5s 窗口外不切");
            Assert.That(s.Enabled, Is.False);
            bool third = s.TryToggle(0.6);    // 离 0.5 仅 0.1s → 双击窗口内 → 切换
            Assert.That(third, Is.True, "窗口被重置后第三下进入窗口才能切");
            Assert.That(s.Enabled, Is.True);
        }

        [Test]
        public void TryToggle_窗口边界_恰好等于0p3算窗口内()
        {
            // 实现：`nowSeconds - lastSeconds <= 0.3` → 恰好 0.3s 算窗口内。
            // 这是 desktop UX 的标准：双击间隔典型阈值 500ms，500ms 整应该 inclusive。
            var s = new FlightState();
            s.TryToggle(0.0);
            bool ok = s.TryToggle(0.3);                 // 恰好 0.3 = 窗口边界 inclusive
            Assert.That(ok, Is.True, "恰好 0.3s 算窗口内（≤ 用）→ 切换");
        }

        [Test]
        public void TryToggle_窗口边界_0p31s算窗口外()
        {
            var s = new FlightState();
            s.TryToggle(0.0);
            bool ok = s.TryToggle(0.31);                // 0.31 > 0.3 窗口外
            Assert.That(ok, Is.False, "0.31s 窗口外→不切");
        }

        [Test]
        public void TryToggle_飞行中再按一次切回步行()
        {
            var s = new FlightState();
            s.TryToggle(0.0);
            s.TryToggle(0.1);                       // 开启
            Assert.That(s.Enabled, Is.True);
            bool toggled = s.TryToggle(0.15);       // 飞行中按一下：直接关闭（无窗口）
            Assert.That(toggled, Is.True);
            Assert.That(s.Enabled, Is.False);
        }

        [Test]
        public void TryToggleViaKey_F键不绕过双击窗口()
        {
            // spec：「F 键等效」≠ 跳过双击窗口——单按 F 仍要按两次才进飞行
            var s = new FlightState();
            bool first = s.TryToggleViaKey(1.0);
            Assert.That(first, Is.False, "单按 F 不切换");
            bool second = s.TryToggleViaKey(1.1);
            Assert.That(second, Is.True, "窗口内第二下切到飞行");
            Assert.That(s.Enabled, Is.True);
        }

        // ─── OnLanded ─────────────────────────────────────────────────────

        [Test]
        public void OnLanded_步行态调用noOp_返回false()
        {
            var s = new FlightState();
            bool changed = s.OnLanded();
            Assert.That(changed, Is.False, "步行态调用 OnLanded 必须 no-op");
            Assert.That(s.Enabled, Is.False);
        }

        [Test]
        public void OnLanded_飞行态调用强制退出并清窗口()
        {
            var s = new FlightState();
            s.TryToggle(0.0);
            s.TryToggle(0.1);                       // 开
            Assert.That(s.Enabled, Is.True);
            bool changed = s.OnLanded();
            Assert.That(changed, Is.True);
            Assert.That(s.Enabled, Is.False, "触地强制退出");
            // 触地后退步行时窗口应当被清空——玩家想再起飞必须重新双击
            // 验证：紧接第一次 toggle 0.2s 处再次单按不切（窗口外）
            bool afterTouch = s.TryToggle(0.3);
            Assert.That(afterTouch, Is.False, "OnLanded 清了窗口，单按不再叠双击");
        }

        // ─── 六向速度合成 ──────────────────────────────────────────────────

        [Test]
        public void ComputeVelocity_无输入速度为零()
        {
            Float3 v = FlightState.ComputeVelocity(new Float3(0, 0, 0), false, false);
            Assert.That(v.X, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(v.Y, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(v.Z, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void ComputeVelocity_WASD水平_速度等于水平8()
        {
            // W 单独：方向 +Z，长度 1 → 速度 (0, 0, +8)
            Float3 v = FlightState.ComputeVelocity(new Float3(0, 0, 1), false, false);
            Assert.That(v.X, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(v.Y, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(v.Z, Is.EqualTo(FlightState.HorizontalSpeed).Within(1e-6f),
                "W 单独按 = z 方向走水平速度 8m/s");

            // D 单独：+X = 8
            v = FlightState.ComputeVelocity(new Float3(1, 0, 0), false, false);
            Assert.That(v.X, Is.EqualTo(FlightState.HorizontalSpeed).Within(1e-6f));

            // SW 对角：归一化后长度 = 1/√2 ——但 PlayerController 已 clamp ≤ 1，
            // 测试用「W + D」长度 = √2 验证乘速度后大小 = 8·√2 ≈ 11.31，与设计一致
            float s = 1f / System.MathF.Sqrt(2f);
            v = FlightState.ComputeVelocity(new Float3(s, 0, s), false, false);
            float m = System.MathF.Sqrt(v.X * v.X + v.Z * v.Z);
            Assert.That(m, Is.EqualTo(FlightState.HorizontalSpeed).Within(1e-4f),
                "对角线归一化方向输入 → 总速度 = 8m/s（与速度上限一致）");
        }

        [Test]
        public void ComputeVelocity_空格升_只升8不水平()
        {
            Float3 v = FlightState.ComputeVelocity(new Float3(0, 0, 0), true, false);
            Assert.That(v.Y, Is.EqualTo(FlightState.VerticalSpeed).Within(1e-6f));
            Assert.That(v.X, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(v.Z, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void ComputeVelocity_Shift降_只降8不水平()
        {
            Float3 v = FlightState.ComputeVelocity(new Float3(0, 0, 0), false, true);
            Assert.That(v.Y, Is.EqualTo(-FlightState.VerticalSpeed).Within(1e-6f),
                "Shift 降 = 竖直速度 -8（与升对称）");
        }

        [Test]
        public void ComputeVelocity_升加降同时抵消()
        {
            // 玩家不小心同时按 Space + Shift 不应该「飞得更快」，而是 0
            Float3 v = FlightState.ComputeVelocity(new Float3(1, 0, 0), true, true);
            Assert.That(v.Y, Is.EqualTo(0f).Within(1e-6f),
                "Space + Shift 同时按 = 竖直抵消，不累加，不取较大者");
            Assert.That(v.X, Is.EqualTo(FlightState.HorizontalSpeed).Within(1e-6f),
                "水平方向不受影响");
        }

        [Test]
        public void ComputeVelocity_六向同时合成_W前进加空格升()
        {
            // W + Space 同时按：水平 +Z + 竖直 +Y，水平速度 8 竖直 8
            Float3 v = FlightState.ComputeVelocity(new Float3(0, 0, 1), true, false);
            Assert.That(v.X, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(v.Y, Is.EqualTo(FlightState.VerticalSpeed).Within(1e-6f));
            Assert.That(v.Z, Is.EqualTo(FlightState.HorizontalSpeed).Within(1e-6f));
        }

        [Test]
        public void ComputeVelocity_水平与竖直各自不超过各自上限()
        {
            // 约定：水平上限 HorizontalSpeed，竖直上限 VerticalSpeed，方向各自独立
            Float3 v = FlightState.ComputeVelocity(new Float3(1, 0, 0), true, false);
            float horiz = System.MathF.Sqrt(v.X * v.X + v.Z * v.Z);
            Assert.That(horiz, Is.LessThanOrEqualTo(FlightState.HorizontalSpeed + 1e-6f),
                "水平分量不超过 HorizontalSpeed");
            Assert.That(System.MathF.Abs(v.Y), Is.LessThanOrEqualTo(FlightState.VerticalSpeed + 1e-6f),
                "竖直分量绝对值不超过 VerticalSpeed");
        }

        // ─── 常量契约 ─────────────────────────────────────────────────────

        [Test]
        public void 常量契约_窗口与速度与设计文档一致()
        {
            // 钉死：默认值改了 spec 也得跟着改——这条是「spec 与实现对齐」的最后一根稻草
            Assert.That(FlightState.DoubleTapWindowSeconds, Is.EqualTo(0.3),
                "双击窗口必须 0.3s——m13 spec 第 28 行钉死");
            Assert.That(FlightState.HorizontalSpeed, Is.EqualTo(8f),
                "水平速度必须 8m/s——约走速 4.3 的 1.86 倍");
            Assert.That(FlightState.VerticalSpeed, Is.EqualTo(8f),
                "竖直速度必须 8m/s——与水平一致，无重力差异");
        }
    }
}
