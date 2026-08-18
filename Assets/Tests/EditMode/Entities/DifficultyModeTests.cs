using MyWorld.Core.Entities;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// m13 W2：难度系统 Core 静态开关契约。
    /// <para>
    /// 真源是 <see cref="DifficultyMode"/>（Core 纯静态 bool），与 m11 W3-5 PeaceMode
    /// 同款语义但放 Core 层——理由见 <see cref="DifficultyMode"/> 注释：Core 不能引
    /// UnityEngine，PlayerPrefs 是 Unity 设施，dotnet 测试链不存在 PlayerPrefs；放
    /// Core 才能让 <c>MobAITests</c> 直接 <c>SetEnabled(true)</c> 验宝宝一击必杀，
    /// 不必反射 Unity 侧类型。
    /// </para>
    /// <para>
    /// PlayerPrefs 持久化（键名 BabyMode）由 Unity 侧 <c>DifficultyModeBridge</c> 负责，
    /// 不在本测试范围（EditMode 测覆盖，见 <c>SettingsPanelUiTests</c>）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class DifficultyModeTests
    {
        [SetUp]
        public void SetUp() => DifficultyMode.ResetCache();

        [TearDown]
        public void TearDown() => DifficultyMode.ResetCache();

        [Test]
        public void 默认_未设过_Enabled为false_普通档()
        {
            DifficultyMode.ResetCache();
            Assert.That(DifficultyMode.Enabled, Is.False,
                "未设过值时难度应默认关 = 普通档（与 PeaceMode 同款契约，孩子不该被静默切难度）");
        }

        [Test]
        public void SetEnabled_true_Enabled立即返回true()
        {
            DifficultyMode.SetEnabled(true);
            Assert.That(DifficultyMode.Enabled, Is.True,
                "SetEnabled(true) 后 Enabled 应立即为 true（缓存命中，不需重启）");
        }

        [Test]
        public void SetEnabled_false_从true切回false()
        {
            DifficultyMode.SetEnabled(true);
            Assert.That(DifficultyMode.Enabled, Is.True);

            DifficultyMode.SetEnabled(false);
            Assert.That(DifficultyMode.Enabled, Is.False,
                "SetEnabled(false) 应能从 true 切回 false（双向切换，不锁死）");
        }

        [Test]
        public void ResetCache_强制回到默认false()
        {
            DifficultyMode.SetEnabled(true);
            Assert.That(DifficultyMode.Enabled, Is.True);

            DifficultyMode.ResetCache();
            Assert.That(DifficultyMode.Enabled, Is.False,
                "ResetCache 应强制回到默认 false（仅测试用，让 SetUp/TearDown 清状态）");
        }
    }
}