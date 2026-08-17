#if UNITY_EDITOR
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m11 W2-3：飘字（Unity/UI/FloatTextUi）。守：经验 +N 绿字 / 伤害 -N 红字的文案与种类、
    /// 1s 计时（AlphaAt/RiseOffsetAt 两条曲线 + ExpireBefore 队列清理）、多条入队不叠字、
    /// 自挂行为（无需 WorldBootstrap 装配）。OnGUI 不跑，全部直调公共入口。
    /// </summary>
    [TestFixture]
    public class FloatTextUiTests
    {
        [TearDown]
        public void TearDown()
        {
            // 懒挂的单例不能跨测试泄漏（下个测试的 Instance 指向已销毁对象会 NRE）
            if (FloatTextUi.Instance != null)
            {
                Object.DestroyImmediate(FloatTextUi.Instance.gameObject);
            }
        }

        // ─── 计时曲线（纯静态，EditMode 直接断言） ──────────────────────

        [Test]
        public void AlphaAt_FadesLinearlyOverOneSecond()
        {
            Assert.That(FloatTextUi.AlphaAt(0f), Is.EqualTo(1f).Within(0.001f), "刚出生不透明");
            Assert.That(FloatTextUi.AlphaAt(0.5f), Is.EqualTo(0.5f).Within(0.001f), "半程半透明（线性淡出）");
            Assert.That(FloatTextUi.AlphaAt(FloatTextUi.DurationSeconds), Is.EqualTo(0f).Within(0.001f), "1s 完全消失");
            Assert.That(FloatTextUi.AlphaAt(5f), Is.EqualTo(0f).Within(0.001f), "过期后保持 0（不复活）");
        }

        [Test]
        public void RiseOffsetAt_RisesOverOneSecond()
        {
            Assert.That(FloatTextUi.RiseOffsetAt(0f), Is.EqualTo(0f).Within(0.01f), "起点不上浮");
            Assert.That(FloatTextUi.RiseOffsetAt(FloatTextUi.DurationSeconds),
                Is.EqualTo(FloatTextUi.RisePixels).Within(0.01f), "1s 时浮满全程");
            Assert.That(FloatTextUi.RiseOffsetAt(7f),
                Is.EqualTo(FloatTextUi.RisePixels).Within(0.01f), "过期后停在终点（钳制）");
        }

        // ─── 静态入口：文案 / 颜色 / 自挂 ───────────────────────────────

        [Test]
        public void ShowExperience_LazilyMounts_AndEnqueuesGreenPlusText()
        {
            // Unity fake-null 语义：EditMode 不回调 OnDestroy，上个测试销毁后静态 _instance
            // 残留 fake-null——NUnit 的 Is.Null 不认 Unity 重载，这里必须用 == null 判定
            Assert.That(FloatTextUi.Instance == null, Is.True, "前置：从未触发就没有可用实例（自挂=按需）");

            FloatTextUi.ShowExperience(3);

            Assert.That(FloatTextUi.Instance, Is.Not.Null, "首次触发自挂（WorldBootstrap 零装配）");
            Assert.That(FloatTextUi.Instance.EntryCount, Is.EqualTo(1));
            var e = FloatTextUi.Instance.ActiveEntries[0];
            Assert.That(e.Text, Is.EqualTo("+3"), "击杀经验飘 +N 文案");
            Assert.That(e.Kind, Is.EqualTo(FloatTextUi.FloatKind.Experience));
        }

        [Test]
        public void ShowDamage_EnqueuesRedMinusText_RoundsSmallAmountToOne()
        {
            FloatTextUi.ShowDamage(0.5f); // m10 B2 镐碎块 0.5 伤：显示取整

            Assert.That(FloatTextUi.Instance.EntryCount, Is.EqualTo(1));
            Assert.That(FloatTextUi.Instance.ActiveEntries[0].Text, Is.EqualTo("-1"), "0.5 取整显示 1，不出「-0」");
            Assert.That(FloatTextUi.Instance.ActiveEntries[0].Kind, Is.EqualTo(FloatTextUi.FloatKind.Damage));
        }

        [Test]
        public void Show_WithNonPositiveAmount_NoText()
        {
            FloatTextUi.ShowExperience(0);
            FloatTextUi.ShowExperience(-2);
            FloatTextUi.ShowDamage(0f);

            // 同上：fake-null 语义用 == null 判定（零/负量在从未挂过实例的干净域里是真 null）
            Assert.That(FloatTextUi.Instance == null, Is.True, "零/负量不出字也不挂实例（0 经验生物不飘 +0）");
        }

        // ─── 队列：多条入队、过期清理、上限 ─────────────────────────────

        [Test]
        public void ExpireBefore_KeepsFreshEntries_DropsExpired()
        {
            var ui = FloatTextUi.EnsureInstance();
            // EditMode 的 Time.time 是非零真实时刻（不是恒 0）——驱动时刻以入队时刻为锚。
            // 全限定：本命名空间外层恰有 MyWorld.Core.Tests.Time，裸 Time 会解析到那个命名空间
            float t0 = UnityEngine.Time.time;
            ui.Add("+3", FloatTextUi.FloatKind.Experience); // StartTime = t0
            ui.Add("-2", FloatTextUi.FloatKind.Damage);

            Assert.That(ui.ExpireBefore(t0 + 0.5f), Is.EqualTo(0), "半程内两条都活着");
            Assert.That(ui.EntryCount, Is.EqualTo(2));

            Assert.That(ui.ExpireBefore(t0 + FloatTextUi.DurationSeconds), Is.EqualTo(2), "满 1s 两条一起过期");
            Assert.That(ui.EntryCount, Is.EqualTo(0), "队列清空");
        }

        [Test]
        public void Add_MultipleEntries_QueueKeepsOrder_ForStackingLayout()
        {
            var ui = FloatTextUi.EnsureInstance();
            ui.Add("+2", FloatTextUi.FloatKind.Experience);
            ui.Add("+5", FloatTextUi.FloatKind.Experience);
            ui.Add("-4", FloatTextUi.FloatKind.Damage);

            // 队列顺序 = 入场顺序（OnGUI 据此分色错行，最新的排最靠下）
            Assert.That(ui.ActiveEntries[0].Text, Is.EqualTo("+2"));
            Assert.That(ui.ActiveEntries[1].Text, Is.EqualTo("+5"));
            Assert.That(ui.ActiveEntries[2].Text, Is.EqualTo("-4"));
            Assert.That(ui.ActiveEntries[2].Kind, Is.EqualTo(FloatTextUi.FloatKind.Damage));
        }

        [Test]
        public void Add_BeyondMax_OldestDroppedImmediately()
        {
            var ui = FloatTextUi.EnsureInstance();
            for (int i = 0; i < FloatTextUi.MaxEntries + 5; i++)
            {
                ui.Add("+" + i, FloatTextUi.FloatKind.Experience);
            }

            Assert.That(ui.EntryCount, Is.EqualTo(FloatTextUi.MaxEntries), "极端刷屏封顶（最旧的当场过期）");
            Assert.That(ui.ActiveEntries[0].Text, Is.EqualTo("+5"), "留下的是最新的那些");
            Assert.That(ui.ActiveEntries[ui.EntryCount - 1].Text,
                Is.EqualTo("+" + (FloatTextUi.MaxEntries + 4)), "最后入队的保留");
        }
    }
}
#endif
