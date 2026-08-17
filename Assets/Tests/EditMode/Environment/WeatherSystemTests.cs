#if UNITY_EDITOR
// m11 W3-4：天气状态机——雨雪窗确定性派生 + TickWeather 状态机 + 60 粒复用/确定性推进。
//   - 派生纯静态：同 seed 同日程、间隔 3-5 日、窗口 0.5 日且不跨日
//   - 状态机：窗内按玩家群系下雪/下雨（Snow 群系雪、其余雨）、窗外晴
//   - 粒子：60 粒上限、同 seed 同轨迹（确定性，不持随机数对象）
// 依赖 UnityEngine（MonoBehaviour/transform），dotnet 链跑不动，
// 整文件 #if UNITY_EDITOR 包裹（与 DayNightCycleAmbientTests 同款）。
using System.Collections.Generic;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Environment;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Environment
{
    [TestFixture]
    public class WeatherSystemTests
    {
        private GameObject _go;
        private GameObject _playerGo;
        private WeatherSystem _weather;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("WeatherSystem(测试)");
            _playerGo = new GameObject("WeatherPlayer(测试)");
            _weather = _go.AddComponent<WeatherSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        }

        private void BindDefaults(int seed)
        {
            _weather.Bind(null, _playerGo.transform, seed);
        }

        /// <summary>
        /// 把本地日计数推进 n 天：TickWeather 靠「tick 变小」检测跨日，
        /// 所以每天给一对 20000→100 的 tick（dt=0 不推粒子，纯走日计数）。
        /// </summary>
        private void AdvanceDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                _weather.TickWeather(20000f, 0f);
                _weather.TickWeather(100f, 0f); // 回绕（100 < 20000）= 跨一天
            }
        }

        [Test]
        public void 窗口派生_同种子同日程_异种子有差异()
        {
            for (int day = 0; day < 30; day++)
            {
                Assert.That(WeatherSystem.IsRainDay(42, day), Is.EqualTo(WeatherSystem.IsRainDay(42, day)),
                    "同 seed 同 day 两次询问结果一致（确定性）");
            }

            bool anyDiff = false;
            for (int day = 0; day < 30 && !anyDiff; day++)
            {
                anyDiff = WeatherSystem.IsRainDay(42, day) != WeatherSystem.IsRainDay(7, day);
            }
            Assert.That(anyDiff, Is.True, "不同 seed 的雨日日程至少有一天不同");
        }

        [Test]
        public void 间隔纪律_每3到5游戏日一场雨()
        {
            foreach (int seed in new[] { 42, 7, 123 })
            {
                var rainDays = new List<int>();
                for (int day = 0; day < 60; day++)
                {
                    if (WeatherSystem.IsRainDay(seed, day)) rainDays.Add(day);
                }
                Assert.That(rainDays.Count, Is.GreaterThan(0), $"seed {seed}：60 天内至少一场雨");
                Assert.That(rainDays[0], Is.InRange(WeatherSystem.MinGapDays, WeatherSystem.MaxGapDays),
                    $"seed {seed}：首场雨在首个 3-5 日内");
                for (int i = 1; i < rainDays.Count; i++)
                {
                    int gap = rainDays[i] - rainDays[i - 1];
                    Assert.That(gap, Is.InRange(WeatherSystem.MinGapDays, WeatherSystem.MaxGapDays),
                        $"seed {seed}：第 {i} 场雨与上一场间隔 {gap} 日，必须在 3-5 日内");
                }
            }
        }

        [Test]
        public void 雨窗半日_起点合法_绝不跨日()
        {
            for (int day = 0; day < 60; day++)
            {
                if (!WeatherSystem.IsRainDay(42, day)) continue;
                float start = WeatherSystem.WindowStartTick(42, day);
                Assert.That(start, Is.InRange(WeatherSystem.WindowStartTickMin, WeatherSystem.WindowStartTickMin + 9999f),
                    "窗口起点在预留区间内");
                Assert.That(start + WeatherSystem.WindowDurationTicks, Is.LessThanOrEqualTo(23000f),
                    "窗口终点 ≤ 23000 tick，绝不跨日");
                Assert.That(WeatherSystem.IsWindowActive(42, day, start - 1f), Is.False, "窗口前不落雨");
                Assert.That(WeatherSystem.IsWindowActive(42, day, start + 1f), Is.True, "窗口开始即落雨");
                Assert.That(WeatherSystem.IsWindowActive(42, day, start + WeatherSystem.WindowDurationTicks - 1f),
                    Is.True, "窗口末 tick 仍在雨中");
                Assert.That(WeatherSystem.IsWindowActive(42, day, start + WeatherSystem.WindowDurationTicks),
                    Is.False, "窗口结束即停");
            }
        }

        [Test]
        public void 状态机_窗内下雨_窗外晴天()
        {
            BindDefaults(42);
            int rainDay = WeatherSystem.FirstRainDay(42);
            float start = WeatherSystem.WindowStartTick(42, rainDay);
            AdvanceDays(rainDay); // 状态机日计数走到第 rainDay 天（挂载起从 0 天计）

            _weather.TickWeather(start - 1f, 0.016f);
            Assert.That(_weather.Current, Is.EqualTo(WeatherSystem.PrecipKind.None), "窗口前是晴天");
            Assert.That(_weather.WindowActive, Is.False);

            _weather.TickWeather(start + 1f, 0.016f);
            Assert.That(_weather.WindowActive, Is.True, "窗口激活");
            Assert.That(_weather.Current, Is.EqualTo(WeatherSystem.PrecipKind.Rain),
                "无生成器时按非雪群系处理 → 下雨");

            _weather.TickWeather(start + WeatherSystem.WindowDurationTicks + 1f, 0.016f);
            Assert.That(_weather.Current, Is.EqualTo(WeatherSystem.PrecipKind.None), "窗口过后放晴");
        }

        [Test]
        public void 状态机_Snow群系下雪_其余群系下雨()
        {
            var generator = new WorldGenerator(42);
            Vector3 snow = Vector3.zero;
            Vector3 other = Vector3.zero;
            bool hasSnow = false, hasOther = false;
            for (int x = -1000; x <= 1000 && (!hasSnow || !hasOther); x += 8)
            {
                for (int z = -1000; z <= 1000; z += 8)
                {
                    var biome = generator.BiomeAt(x, z);
                    if (biome == Biome.Snow && !hasSnow)
                    {
                        hasSnow = true;
                        snow = new Vector3(x + 0.5f, 70f, z + 0.5f);
                    }
                    else if (biome != Biome.Snow && !hasOther)
                    {
                        hasOther = true;
                        other = new Vector3(x + 0.5f, 70f, z + 0.5f);
                    }
                    if (hasSnow && hasOther) break;
                }
            }
            Assert.That(hasSnow, Is.True, "前置：seed 42 在 ±1000 格内找得到 Snow 群系（Preview 已证雪群系存在）");
            Assert.That(hasOther, Is.True, "前置：也找得到非雪群系");

            _weather.Bind(generator, _playerGo.transform, 42);
            int rainDay = WeatherSystem.FirstRainDay(42);
            float start = WeatherSystem.WindowStartTick(42, rainDay);
            AdvanceDays(rainDay); // 日计数走到雨日
            float tick = start + 1f;

            _playerGo.transform.position = snow;
            _weather.TickWeather(tick, 0.016f);
            Assert.That(_weather.Current, Is.EqualTo(WeatherSystem.PrecipKind.Snow),
                "玩家在 Snow 群系的雨日窗口内 → 下雪");

            _playerGo.transform.position = other;
            _weather.TickWeather(tick + 0.016f, 0.016f);
            Assert.That(_weather.Current, Is.EqualTo(WeatherSystem.PrecipKind.Rain),
                "同一窗口内走到非雪群系 → 转下雨");
        }

        [Test]
        public void 粒子_60粒确定性推进_同seed同轨迹()
        {
            BindDefaults(42);
            int rainDay = WeatherSystem.FirstRainDay(42);
            float tick = WeatherSystem.WindowStartTick(42, rainDay) + 1f;
            AdvanceDays(rainDay);

            _weather.TickWeather(tick, 0.016f);
            for (int i = 0; i < 100; i++) _weather.TickWeather(tick, 0.016f);
            for (int i = 0; i < WeatherSystem.MaxParticles; i++)
            {
                Assert.That(_weather.ParticleY01(i), Is.InRange(0f, 1f), "粒子 Y 始终 wrap 在 [0,1)");
            }

            // 第二个系统同 seed 同步进 → 同一轨迹（确定性：哈希初始化 + 同 dt 序列）
            var go2 = new GameObject("WeatherSystem2(测试)");
            try
            {
                var player2 = new GameObject("WeatherPlayer2(测试)");
                var weather2 = go2.AddComponent<WeatherSystem>();
                weather2.Bind(null, player2.transform, 42);
                for (int i = 0; i < rainDay; i++)
                {
                    weather2.TickWeather(20000f, 0f);
                    weather2.TickWeather(100f, 0f);
                }
                weather2.TickWeather(tick, 0.016f);
                for (int i = 0; i < 100; i++) weather2.TickWeather(tick, 0.016f);

                for (int i = 0; i < WeatherSystem.MaxParticles; i++)
                {
                    Assert.That(weather2.ParticleY01(i), Is.EqualTo(_weather.ParticleY01(i)).Within(1e-5f),
                        $"第 {i} 粒 Y 轨迹一致（同 seed 确定性）");
                    Assert.That(weather2.ParticleX01(i), Is.EqualTo(_weather.ParticleX01(i)).Within(1e-5f),
                        $"第 {i} 粒 X 轨迹一致（同 seed 确定性）");
                }
                Object.DestroyImmediate(player2);
            }
            finally
            {
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void 粒子_雨天下落_雪天显著更慢()
        {
            BindDefaults(42);
            int rainDay = WeatherSystem.FirstRainDay(42);
            float tick = WeatherSystem.WindowStartTick(42, rainDay) + 1f;
            AdvanceDays(rainDay);

            // 雨：快落（0.90-1.40 屏/秒）。Δ观测用 wrap 补偿（跨 1.0 回绕时加回 1）
            float rainSpeed = MeasureFallSpeed(tick);
            Assert.That(rainSpeed, Is.GreaterThan(0.8f), "雨天下落速度 ≥ 0.8 屏/秒");

            // 雪同一窗口换 Snow 群系：下落显著更慢（0.10-0.18 屏/秒）
            var generator = new WorldGenerator(42);
            Vector3 snow = Vector3.zero;
            bool found = false;
            for (int x = -1000; x <= 1000 && !found; x += 8)
            {
                for (int z = -1000; z <= 1000; z += 8)
                {
                    if (generator.BiomeAt(x, z) == Biome.Snow)
                    {
                        snow = new Vector3(x + 0.5f, 70f, z + 0.5f);
                        found = true;
                        break;
                    }
                }
            }
            Assert.That(found, Is.True, "前置：seed 42 在 ±1000 格内找得到 Snow 群系");
            _playerGo.transform.position = snow;
            _weather.Bind(generator, _playerGo.transform, 42);
            float snowSpeed = MeasureFallSpeed(tick);
            Assert.That(snowSpeed, Is.LessThan(0.2f), "雪天下落速度 < 0.2 屏/秒");
        }

        /// <summary>量第 3 粒 0.05s 内的下落速度（wrap 补偿：Y 是 [0,1) 循环坐标）。</summary>
        private float MeasureFallSpeed(float tick)
        {
            _weather.TickWeather(tick, 0.001f);
            float before = _weather.ParticleY01(3);
            _weather.TickWeather(tick, 0.05f);
            float after = _weather.ParticleY01(3);
            if (after < before) after += 1f; // 回绕补偿
            return (after - before) / 0.05f;
        }
    }
}
#endif
