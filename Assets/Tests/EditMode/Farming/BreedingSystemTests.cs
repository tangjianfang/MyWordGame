using System.Linq;
using MyWorld.Core.Entities;
using MyWorld.Core.Farming;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Farming
{
    /// <summary>
    /// m11 W1-6：Core 喂食繁殖数据模型（照 HungerSystemTests 的纯 Core 计时语义测试）。
    /// 契约：同种成年两只各吃一份对应食物 → 配对 → 孕期 30s → 幼崽记录（scale 0.5）→
    /// 600s 长大。零随机数——全部计时推进，replay 天然可复现。
    /// 实际刷 Mob / 缩放视觉在集成点② 接 Unity。
    /// </summary>
    [TestFixture]
    public class BreedingSystemTests
    {
        private static readonly Float3 PosA = new Float3(0f, 64f, 0f);
        private static readonly Float3 PosB = new Float3(3f, 64f, 0f); // 距 A 3m，配对半径内
        private static readonly Float3 PosFar = new Float3(100f, 64f, 0f); // 距 A 100m，配对半径外

        [Test]
        public void 可繁殖名单_只含被动生物()
        {
            Assert.That(BreedingSystem.CanBreed(MobKind.Pig), Is.True);
            Assert.That(BreedingSystem.CanBreed(MobKind.Cow), Is.True);
            Assert.That(BreedingSystem.CanBreed(MobKind.Chicken), Is.True);
            Assert.That(BreedingSystem.CanBreed(MobKind.Sheep), Is.True);

            Assert.That(BreedingSystem.CanBreed(MobKind.Zombie), Is.False, "敌对生物不可繁殖");
            Assert.That(BreedingSystem.CanBreed(MobKind.Skeleton), Is.False);
            Assert.That(BreedingSystem.CanBreed(MobKind.Villager), Is.False, "村民走交易线，不进繁殖");
        }

        [Test]
        public void 饲料表_每种可繁殖生物都有已注册的食物()
        {
            foreach (MobKind kind in new[]
                     {
                         MobKind.Pig, MobKind.Cow, MobKind.Chicken, MobKind.Sheep, MobKind.Rabbit,
                         MobKind.Fox, MobKind.Deer, MobKind.Panda, MobKind.Penguin, MobKind.Goat,
                         MobKind.Raccoon, MobKind.Hamster,
                     })
            {
                Assert.That(BreedingSystem.FeedItemFor(kind), Is.Not.Null, $"{kind} 必须有对应饲料（否则右键喂食永远无效）");
            }

            Assert.That(BreedingSystem.FeedItemFor(MobKind.Zombie), Is.Null, "敌对生物没有饲料");
        }

        [Test]
        public void 喂食_对的食物_进入发情名单_错的食物或敌对生物拒绝()
        {
            var breeding = new BreedingSystem();

            Assert.That(breeding.TryFeed(1, MobKind.Cow, PosA, "wheat"), Is.True, "牛吃小麦应成功");
            Assert.That(breeding.FedEntityIds, Does.Contain(1));

            Assert.That(breeding.TryFeed(2, MobKind.Pig, PosB, "wheat"), Is.False, "猪的饲料不是小麦（甜菜系）");
            Assert.That(breeding.TryFeed(3, MobKind.Zombie, PosA, "wheat"), Is.False, "敌对生物喂什么都不吃");

            // 重复喂同一只不叠加（一次繁殖一份食物，防刷）
            Assert.That(breeding.TryFeed(1, MobKind.Cow, PosA, "wheat"), Is.False, "已发情的个体再喂无效");
        }

        [Test]
        public void 繁殖_同种两只各吃一份_孕期30s后产幼崽记录()
        {
            var breeding = new BreedingSystem();

            Assert.That(breeding.TryFeed(1, MobKind.Cow, PosA, "wheat"), Is.True);
            Assert.That(breeding.TryFeed(2, MobKind.Cow, PosB, "wheat"), Is.True);

            breeding.Tick(BreedingSystem.PregnancySeconds - 1f);
            Assert.That(breeding.TakeNewborns(), Is.Empty, "孕期未满不该产崽");

            breeding.Tick(1f); // 累计正好 30s（29+1 都是二进制可精确表示的数，避免浮点边缘）
            var newborns = breeding.TakeNewborns();
            Assert.That(newborns.Count, Is.EqualTo(1), "一对成年应产一只幼崽记录");
            Assert.That(newborns[0].Kind, Is.EqualTo(MobKind.Cow));
            Assert.That(newborns[0].Scale, Is.EqualTo(BreedingSystem.BabyScale), "幼崽缩放固定 0.5");
            // 出生位置 = 配对时父母位置的中点
            Assert.That(newborns[0].Position.X, Is.EqualTo((PosA.X + PosB.X) * 0.5f).Within(0.001f));
            Assert.That(newborns[0].Position.Z, Is.EqualTo(PosA.Z).Within(0.001f));

            // 配对后父母双方退出发情名单（喂食状态被孕期消费）
            Assert.That(breeding.FedEntityIds, Is.Empty, "配对后父母的发情状态清空");
            Assert.That(breeding.TakeNewborns(), Is.Empty, "TakeNewborns 取走后清空，不重复发");
        }

        [Test]
        public void 繁殖_异种或距离过远_不配对()
        {
            var mixed = new BreedingSystem();
            Assert.That(mixed.TryFeed(1, MobKind.Cow, PosA, "wheat"), Is.True);
            Assert.That(mixed.TryFeed(2, MobKind.Pig, PosB, "beet"), Is.True); // 异种各吃各的饲料
            mixed.Tick(BreedingSystem.PregnancySeconds * 4);
            Assert.That(mixed.TakeNewborns(), Is.Empty, "不同物种即使都发情也不能配对");

            var distant = new BreedingSystem();
            Assert.That(distant.TryFeed(1, MobKind.Cow, PosA, "wheat"), Is.True);
            Assert.That(distant.TryFeed(2, MobKind.Cow, PosFar, "wheat"), Is.True);
            distant.Tick(BreedingSystem.PregnancySeconds * 4);
            Assert.That(distant.TakeNewborns(), Is.Empty, "超出配对半径的两只不会凑一对");
        }

        [Test]
        public void 繁殖_只喂一只_发情30s过期_不产崽()
        {
            var breeding = new BreedingSystem();
            Assert.That(breeding.TryFeed(1, MobKind.Cow, PosA, "wheat"), Is.True);

            breeding.Tick(BreedingSystem.FedWindowSeconds + 1f);
            Assert.That(breeding.FedEntityIds, Is.Empty, "发情窗口（30s）过了没配对上就作废");

            // 之后再喂第二只也来不及了——第一只早已退出，只算新一轮发情
            Assert.That(breeding.TryFeed(2, MobKind.Cow, PosB, "wheat"), Is.True);
            breeding.Tick(BreedingSystem.PregnancySeconds * 4);
            Assert.That(breeding.TakeNewborns(), Is.Empty, "过期后单边补喂不会凭空产崽");
        }

        [Test]
        public void 幼崽_注册后IsBaby_600s后进长大名单()
        {
            var breeding = new BreedingSystem();
            breeding.RegisterBaby(77, MobKind.Cow);

            Assert.That(breeding.IsBaby(77), Is.True, "注册即幼崽（scale 0.5 由 Unity 侧按 IsBaby 施加）");
            Assert.That(breeding.IsBaby(78), Is.False, "未注册的实体不是幼崽");

            breeding.Tick(BreedingSystem.BabyGrowSeconds - 0.5f);
            Assert.That(breeding.TakeGrownBabies(), Is.Empty, "600s 未满不算长大");

            breeding.Tick(0.5f); // 累计正好 600s（599.5+0.5 二进制精确，避免浮点边缘）
            Assert.That(breeding.IsBaby(77), Is.False, "到点长大，不再是幼崽");
            Assert.That(breeding.TakeGrownBabies(), Is.EqualTo(new[] { 77 }), "长大名单发号给 Unity 恢复 scale 1");
            Assert.That(breeding.TakeGrownBabies(), Is.Empty, "长大名单也只发一次");
        }

        [Test]
        public void 幼崽_不能被喂食_防连代刷()
        {
            var breeding = new BreedingSystem();
            breeding.RegisterBaby(77, MobKind.Cow);

            Assert.That(breeding.TryFeed(77, MobKind.Cow, PosA, "wheat"), Is.False,
                "幼崽吃了也不算数——不然出生就能再排队繁殖");
        }

        [Test]
        public void 计时_多对并发_各自独立到点()
        {
            var breeding = new BreedingSystem();
            // 三对牛错开喂食 → 孕期依次到点
            breeding.TryFeed(1, MobKind.Cow, PosA, "wheat");
            breeding.TryFeed(2, MobKind.Cow, PosB, "wheat");
            breeding.Tick(10f);

            breeding.TryFeed(3, MobKind.Cow, PosA, "wheat");
            breeding.TryFeed(4, MobKind.Cow, PosB, "wheat");
            breeding.Tick(10f);

            breeding.TryFeed(5, MobKind.Cow, PosA, "wheat");
            breeding.TryFeed(6, MobKind.Cow, PosB, "wheat");
            breeding.Tick(10f); // 第一对已孕 30s → 产崽

            var batch1 = breeding.TakeNewborns();
            Assert.That(batch1.Count, Is.EqualTo(1), "第 30s 只有第一对到点");

            breeding.Tick(10f);
            breeding.Tick(10f);
            var batch2 = breeding.TakeNewborns();
            Assert.That(batch2.Count, Is.EqualTo(2), "第 50s/60s 的两对随后各产一只");
        }
    }
}
