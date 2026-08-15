#if UNITY_EDITOR
// m5 B2：Flat 环境光 + 昼夜光照参数校准。
// 背景：场景 ambientMode=Skybox 时 RenderSettings.ambientLight 是 no-op——
// DayNightCycle 每帧写 ambientLight 但画面根本不吃它，白天黑夜都偏暗。
// 本文件断言：驱动 DayNightCycle 后 ambientMode 被纠正为 Flat（写入真正生效），
// 且白天/夜晚参数 == spec 校准值（sun 1.3/0.35，ambient 0.7 灰 / (0.22,0.25,0.40)）。
// 整个文件 #if UNITY_EDITOR 包裹：dotnet 链跳过，Unity EditMode 链跑。
using MyWorld.Core.Time;
using MyWorld.Unity.Environment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyWorld.Core.Tests.Environment
{
    [TestFixture]
    public class DayNightCycleAmbientTests
    {
        private GameObject _go;
        private Light _sun;
        private DayNightCycle _cycle;
        private AmbientMode _prevMode;
        private Color _prevAmbient;

        [SetUp]
        public void SetUp()
        {
            // 保存全局 RenderSettings 并模拟「旧现场」（Skybox no-op），
            // 测完后还原，避免污染同批跑的其它 EditMode 测试。
            _prevMode = RenderSettings.ambientMode;
            _prevAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Skybox;

            var sunGo = new GameObject("方向光(测试)");
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _go = new GameObject("DayNightCycle(测试)");
            _cycle = _go.AddComponent<DayNightCycle>();
            _cycle.SunLight = _sun;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            UnityEngine.Object.DestroyImmediate(_sun.gameObject);
            RenderSettings.ambientMode = _prevMode;
            RenderSettings.ambientLight = _prevAmbient;
        }

        [Test]
        public void 参数_白天_符合Spec校准值()
        {
            Assert.That(DayNightCycle.DaySunIntensity, Is.EqualTo(1.3f),
                "白天 sun intensity 必须 == 1.3（Linear 下 1.0 偏暗，spec §3 校准值）");
            Assert.That(DayNightCycle.DayAmbient, Is.EqualTo(new Color(0.7f, 0.7f, 0.7f)),
                "白天 ambient 必须 == 0.7 灰（spec §3 校准值）");
        }

        [Test]
        public void 参数_夜晚_符合Spec校准值()
        {
            Assert.That(DayNightCycle.NightSunIntensity, Is.EqualTo(0.35f),
                "夜晚 sun intensity 必须 == 0.35（0.2 时方块不可辨，spec §3 校准值）");
            Assert.That(DayNightCycle.NightAmbient, Is.EqualTo(new Color(0.22f, 0.25f, 0.40f)),
                "夜晚 ambient 必须 == (0.22,0.25,0.40) 带蓝调月光（spec §3 校准值）");
        }

        [Test]
        public void 驱动白天tick_纠正Skybox为Flat并写入环境光()
        {
            var time = new TimeOfDay { CurrentTick = 6000f }; // 正午 → phase 1 = 白天

            _cycle.Apply(time.Phase);

            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat),
                "ambientMode 必须被纠正为 Flat——Skybox 模式下 ambientLight 是 no-op（B2 要修的根因）");
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(DayNightCycle.DayAmbient),
                "白天环境光必须写入 spec 值 0.7 灰");
            Assert.That(_sun.intensity, Is.EqualTo(1.3f),
                "白天太阳强度必须 == 1.3");
            Assert.That(_sun.color, Is.EqualTo(Color.white),
                "白天太阳颜色应为纯白");
        }

        [Test]
        public void 驱动夜晚tick_纠正Skybox为Flat并写入环境光()
        {
            var time = new TimeOfDay { CurrentTick = 20000f }; // 深夜 → phase 3 = 夜晚

            _cycle.Apply(time.Phase);

            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Flat),
                "夜晚同样必须 Flat，否则 ambientLight 写入无效");
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(DayNightCycle.NightAmbient),
                "夜晚环境光必须写入 spec 值 (0.22,0.25,0.40)");
            Assert.That(_sun.intensity, Is.EqualTo(0.35f),
                "夜晚太阳强度必须 == 0.35");
        }
    }
}
#endif
