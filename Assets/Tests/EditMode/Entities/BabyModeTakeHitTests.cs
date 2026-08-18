using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// m13 W2：宝宝模式一击必杀契约——<see cref="DifficultyMode.Enabled"/> = true 时，
    /// 所有 mob（含 Boss）受任意 1 点正伤害都进 Dying 序列。验证路径是
    /// <see cref="MobAI.TakeHit"/> 入口处一次性判定 damage = mob.Health.Max。
    /// <para>
    /// 取舍：选「入口处判定」而非「建档时缩放 MaxHealth」的理由见
    /// <see cref="MobAI.TakeHit"/> 注释——后者切难度后场上现存的 mob 仍是旧血，得重载
    /// 场景才一致；入口处即时生效，下一次受击就按新规则结算。
    /// </para>
    /// </summary>
    [TestFixture]
    public class BabyModeTakeHitTests
    {
        [SetUp]
        public void SetUp() => DifficultyMode.ResetCache();

        [TearDown]
        public void TearDown() => DifficultyMode.ResetCache();

        private static readonly Float3 AttackerPos = new Float3(0, 64, 0);

        [Test]
        public void 宝宝模式_僵尸受击1伤_一击必杀()
        {
            // 孩子原话「一级血一击必杀」——僵尸默认 HP=20，受 1 伤应死在入口处缩放
            DifficultyMode.SetEnabled(true);

            var zombie = Mob.Create(9, AttackerPos); // mobTypeId 9 = 新僵尸 HP=20
            Assert.That(zombie.Health.Max, Is.EqualTo(20f), "前置：新僵尸 MaxHealth=20");
            Assert.That(zombie.Health.Current, Is.EqualTo(20f));

            bool lethal = MobAI.TakeHit(zombie, AttackerPos, 1f);

            Assert.That(lethal, Is.True, "宝宝模式下 1 伤应触发致死一击内联 TransitionToDying");
            Assert.That(zombie.Health.IsDead, Is.True, "僵尸应死在受击瞬间（Current=0）");
            Assert.That(zombie.State, Is.EqualTo(MobState.Dying),
                "TakeHit 致死分支应同步转 Dying（m9 A3 修断环③：LastDrops 必须可达）");
            Assert.That(zombie.LastDrops, Is.Not.Null,
                "宝宝一击必杀仍要走 MobAI 死亡序列，掉落物该出");
        }

        [Test]
        public void 宝宝模式_猪受击1伤_一击必杀()
        {
            // 猪 HP=10（mobTypeId 6），1 伤在普通档只扣 1/10；宝宝档必死
            DifficultyMode.SetEnabled(true);

            var pig = Mob.Create(6, AttackerPos);
            Assert.That(pig.Health.Max, Is.EqualTo(10f), "前置：猪 MaxHealth=10");

            bool lethal = MobAI.TakeHit(pig, AttackerPos, 1f);

            Assert.That(lethal, Is.True, "宝宝模式猪受 1 伤应致死");
            Assert.That(pig.State, Is.EqualTo(MobState.Dying));
        }

        [Test]
        public void 宝宝模式_Boss受击_一击必杀()
        {
            // Boss HP=60（mobTypeId 27），按任务卡「不动 Boss 单独规则」——孩子原话
            // 不区分 Boss 与普通怪，统一处理符合预期
            DifficultyMode.SetEnabled(true);

            var boss = Mob.Create(27, AttackerPos);
            Assert.That(boss.Health.Max, Is.EqualTo(60f), "前置：Boss MaxHealth=60");

            bool lethal = MobAI.TakeHit(boss, AttackerPos, 1f);

            Assert.That(lethal, Is.True, "宝宝模式 Boss 受 1 伤应一击必杀（孩子原话不含 Boss 例外）");
            Assert.That(boss.Health.IsDead, Is.True);
            Assert.That(boss.State, Is.EqualTo(MobState.Dying));
        }

        [Test]
        public void 普通模式_僵尸受击1伤_不死()
        {
            // 反向对照：普通档（默认）扣 1 血，僵尸 HP=20 应存活
            DifficultyMode.ResetCache(); // 默认 false
            Assert.That(DifficultyMode.Enabled, Is.False, "前置：难度应默认关");

            var zombie = Mob.Create(9, AttackerPos);
            bool lethal = MobAI.TakeHit(zombie, AttackerPos, 1f);

            Assert.That(lethal, Is.False, "普通档 1 伤不应致死");
            Assert.That(zombie.Health.Current, Is.EqualTo(19f),
                "普通档 1 伤应只扣 1（僵尸 HP=20 - 1 = 19）");
            Assert.That(zombie.Health.IsDead, Is.False);
            Assert.That(zombie.State, Is.Not.EqualTo(MobState.Dying),
                "普通档僵尸受 1 伤不应进 Dying");
        }

        [Test]
        public void 宝宝模式_0伤不入缩放_尸体短路保持()
        {
            // 边界：damage <= 0 时不进缩放（Health.Damage 已忽略 0/负值），
            //   同时「尸体免再伤」依旧生效（mob.IsAlive=false 短路）
            DifficultyMode.SetEnabled(true);

            var pig = Mob.Create(6, AttackerPos);
            MobAI.TakeHit(pig, AttackerPos, 100f); // 先打死
            Assert.That(pig.IsAlive, Is.False, "前置：猪应已死");

            bool lethalAgain = MobAI.TakeHit(pig, AttackerPos, 1f);
            Assert.That(lethalAgain, Is.False,
                "尸体再受击整体短路（A2 Minor 2），即使宝宝档也不应再触发");
        }
    }
}