#if UNITY_EDITOR
// Fix-up X1：验证 MobManager 在 mob 死亡时把 mob.LastDrops 实例化为 ItemDropEntity 并加入
// PlayerContext.ItemDrops。覆盖 review-final.md critical #1：MobAI 写入 LastDrops 但 Unity 侧
// 无消费者，导致 spec D4+D5 功能失效（僵尸死亡不掉任何东西）。
// 整个文件用 #if UNITY_EDITOR 包裹：依赖 MyWorld.Unity.Combat.MobManager，dotnet 链跑不动。
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;
// Core/Items 与 Core/Entities 各有一个掉落表：前者是 MobAI.cs:48 用的静态 Items.ItemDropTable，
// 后者是 JSON 驱动的概率表（Entities/MobDropTable）。本测试只关心前者（写 LastDrops 的）。
using ItemDropTable = MyWorld.Core.Items.ItemDropTable;

namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// MobManager.SpawnDropsForMob 的契约：
    /// 1) Dying mob 的每条 LastDrops 都应实例化为一个 ItemDropEntity 加到 PlayerContext.ItemDrops。
    /// 2) 调用后应把 mob.LastDrops 清空，防止重复掉落（幂等）。
    /// 3) Position = mob.Position。Content = 对应 ItemStack。
    /// 4) 空 LastDrops / 无 PlayerContext 时为 no-op。
    /// </summary>
    [TestFixture]
    public class MobDeathDropTests
    {
        private GameObject _ctxHost;
        private GameObject _mgrHost;
        private GameObject _player;
        private PlayerContext _ctx;
        private MobManager _mgr;

        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake
        /// （Unity 仅在 PlayMode / 场景加载时回调），用反射显式调用私有 Awake。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [SetUp]
        public void SetUp()
        {
            _ctxHost = new GameObject("MobDeathDropTestCtx");
            _ctx = _ctxHost.AddComponent<PlayerContext>();
            InvokeAwake(_ctx); // 显式触发 Awake，让 Instance = _ctx
            _mgrHost = new GameObject("MobDeathDropTestMgr");
            _player = new GameObject("MobDeathDropTestPlayer");
            _player.transform.position = new Vector3(0.5f, 71f, 0.5f);
            _mgr = _mgrHost.AddComponent<MobManager>();
            _mgr.Bind(world: null, time: null, player: _player.transform,
                generator: null, rules: null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_mgrHost != null) Object.DestroyImmediate(_mgrHost);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_ctxHost != null) Object.DestroyImmediate(_ctxHost);
        }

        /// <summary>
        /// 猪死亡：LastDrops 含 1 个 porkchop（1008）。调用 SpawnDropsForMob 应
        /// 创建一个 ItemDropEntity 加到 PlayerContext.ItemDrops，并把 LastDrops 清空。
        /// </summary>
        [Test]
        public void SpawnDropsForMob_Pig_CreatesPorkchopDrop()
        {
            var pig = Mob.Create(6, new Float3(5f, 70f, 5f)); // MobTypeId 6 = 新猪 (MobKind.Pig)
            pig.State = MobState.Dying;
            pig.LastDrops = ItemDropTable.Drop(pig.Kind);
            int expectedDrops = pig.LastDrops.Length;
            Assume.That(expectedDrops, Is.GreaterThan(0), "ItemDropTable 应给猪返回至少 1 个掉落");

            _mgr.SpawnDropsForMob(pig);

            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(expectedDrops),
                $"ItemDrops 应增加 {expectedDrops} 条（实际 {_ctx.ItemDrops.Count}）");
            // 找到 ItemId=PorkchopItemId 的掉落实体
            bool foundPork = false;
            foreach (var d in _ctx.ItemDrops)
            {
                if (d.Content.HasValue && d.Content.Value.ItemId == ItemDropTable.PorkchopItemId)
                {
                    foundPork = true;
                    break;
                }
            }
            Assert.That(foundPork, Is.True, "猪死亡应掉 porkchop (1008)");
            Assert.That(pig.LastDrops, Is.Null,
                "SpawnDropsForMob 调用后应清空 LastDrops（幂等）");
        }

        /// <summary>
        /// 僵尸死亡：LastDrops 含 1 个 rotten_flesh（1010）。
        /// </summary>
        [Test]
        public void SpawnDropsForMob_Zombie_CreatesRottenFleshDrop()
        {
            var z = Mob.Create(9, new Float3(10f, 70f, 10f)); // MobTypeId 9 = 新僵尸
            z.State = MobState.Dying;
            z.LastDrops = ItemDropTable.Drop(z.Kind);

            _mgr.SpawnDropsForMob(z);

            Assert.That(_ctx.ItemDrops.Count, Is.GreaterThan(0),
                "僵尸死亡应至少掉一个 ItemDropEntity");
            bool foundRotten = false;
            foreach (var d in _ctx.ItemDrops)
            {
                if (d.Content.HasValue && d.Content.Value.ItemId == ItemDropTable.RottenFleshItemId)
                {
                    foundRotten = true;
                    break;
                }
            }
            Assert.That(foundRotten, Is.True, "Zombie 死亡应掉 rotten_flesh (1010)");
        }

        /// <summary>
        /// 幂等性：连续调用两次 SpawnDropsForMob 应只产生一份掉落（清空 LastDrops 后第二次 no-op）。
        /// </summary>
        [Test]
        public void SpawnDropsForMob_IsIdempotent()
        {
            var pig = Mob.Create(6, new Float3(0f, 70f, 0f));
            pig.State = MobState.Dying;
            pig.LastDrops = ItemDropTable.Drop(pig.Kind);
            int expected = pig.LastDrops.Length;

            _mgr.SpawnDropsForMob(pig);
            int afterFirst = _ctx.ItemDrops.Count;
            _mgr.SpawnDropsForMob(pig); // 第二次：LastDrops 已清空，应 no-op
            int afterSecond = _ctx.ItemDrops.Count;

            Assert.That(afterFirst, Is.EqualTo(expected),
                $"首次调用应添加 {expected} 个 ItemDropEntity（实际 {afterFirst}）");
            Assert.That(afterSecond, Is.EqualTo(afterFirst),
                "重复调用 SpawnDropsForMob 不应产生额外掉落（幂等）");
            Assert.That(pig.LastDrops, Is.Null, "首次调用后 LastDrops 应被清空");
        }

        /// <summary>
        /// 旧 Passive mob（mobTypeId=1）没有掉表：SpawnDropsForMob 应为 no-op，不产生掉落。
        /// </summary>
        [Test]
        public void SpawnDropsForMob_NoDrops_IsNoOp()
        {
            var passive = Mob.Create(1, new Float3(0f, 70f, 0f)); // MobTypeId 1 = 旧猪 (MobKind.Passive)
            passive.State = MobState.Dying;
            passive.LastDrops = ItemDropTable.Drop(passive.Kind); // Passive 不在 switch → 空数组
            Assume.That(passive.LastDrops.Length, Is.EqualTo(0), "Passive mob 应无掉落表");

            _mgr.SpawnDropsForMob(passive);

            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(0),
                "空 LastDrops 不应产生 ItemDropEntity");
        }

        /// <summary>
        /// null LastDrops（玩家伤害走 CombatController 路径时 MobAI.Tick 还没机会写入）也应是 no-op。
        /// </summary>
        [Test]
        public void SpawnDropsForMob_NullLastDrops_IsNoOp()
        {
            var pig = Mob.Create(6, new Float3(0f, 70f, 0f));
            pig.State = MobState.Dying;
            pig.LastDrops = null;

            _mgr.SpawnDropsForMob(pig);

            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(0),
                "null LastDrops 不应产生掉落（让 MobAI.Tick 有机会写入）");
        }

        /// <summary>
        /// ItemDropEntity.Position 必须等于 mob.Position（让掉落物出现在 mob 死亡的精确位置）。
        /// </summary>
        [Test]
        public void SpawnDropsForMob_Position_MatchesMobPosition()
        {
            var pos = new Float3(12.5f, 65.0f, 7.25f);
            var pig = Mob.Create(6, pos);
            pig.State = MobState.Dying;
            pig.LastDrops = ItemDropTable.Drop(pig.Kind);

            _mgr.SpawnDropsForMob(pig);

            Assert.That(_ctx.ItemDrops.Count, Is.GreaterThan(0));
            var drop = _ctx.ItemDrops[_ctx.ItemDrops.Count - 1];
            Assert.That(drop.Position.X, Is.EqualTo(pos.X), "X 应一致");
            Assert.That(drop.Position.Y, Is.EqualTo(pos.Y), "Y 应一致");
            Assert.That(drop.Position.Z, Is.EqualTo(pos.Z), "Z 应一致");
        }

        /// <summary>
        /// Fix-up X4：验证 X1 的 SpawnDropsForMob 正确透传 ItemStack.Count 到 ItemDropEntity。
        /// 即 JSON-driven Core ItemDropTable 算出来的 count（如 1-3 porkchop），
        /// 经 LastDrops → SpawnDropsForMob 路径后能完整保留到 ItemDropEntity。
        /// 我们手动塞一个 count=3 的 stack 入 LastDrops，验证 entity 的 Content.Count == 3。
        /// </summary>
        [Test]
        public void SpawnDropsForMob_CountPropagatesToItemDropEntity()
        {
            var pig = Mob.Create(6, new Float3(0, 70, 0));
            pig.State = MobState.Dying;
            // 手动构造 LastDrops 模拟「JSON 区间 1-3」中随机到 3 的情况
            pig.LastDrops = new[]
            {
                new ItemStack(ItemDropTable.PorkchopItemId, 3),
            };

            _mgr.SpawnDropsForMob(pig);

            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(1),
                "应只创建一个 ItemDropEntity");
            var drop = _ctx.ItemDrops[0];
            Assert.That(drop.Content.HasValue, Is.True);
            Assert.That(drop.Content.Value.ItemId, Is.EqualTo(ItemDropTable.PorkchopItemId));
            Assert.That(drop.Content.Value.Count, Is.EqualTo(3),
                "ItemDropEntity.Count 应等于 LastDrops 的 Count（X1 透传 count）");
        }

        // ─── m9 A3：击杀经验入账（猪 3 / 牛 5 / 鸡 2 / 僵尸 10） ─────────────

        /// <summary>
        /// 经验常量表：猪 3 / 牛 5 / 鸡 2 / 僵尸 10（spec §3「击杀经验」）；
        /// 不在表内的 kind（旧 Passive/Hostile、Villager）为 0——打死不白给分。
        /// </summary>
        [Test]
        public void KillExperience_Constants()
        {
            Assert.That(MobManager.KillExperience(MobKind.Pig), Is.EqualTo(3), "杀猪 +3");
            Assert.That(MobManager.KillExperience(MobKind.Cow), Is.EqualTo(5), "杀牛 +5");
            Assert.That(MobManager.KillExperience(MobKind.Chicken), Is.EqualTo(2), "杀鸡 +2");
            Assert.That(MobManager.KillExperience(MobKind.Zombie), Is.EqualTo(10), "杀僵尸 +10");
            Assert.That(MobManager.KillExperience(MobKind.Passive), Is.EqualTo(0),
                "旧 Passive 不在经验表内");
            Assert.That(MobManager.KillExperience(MobKind.Hostile), Is.EqualTo(0),
                "旧 Hostile 不在经验表内");
            Assert.That(MobManager.KillExperience(MobKind.Villager), Is.EqualTo(0),
                "村民不可杀不计分");
        }

        /// <summary>
        /// 死因标记置位的 mob 经 <see cref="MobManager.GrantKillExperience"/> 入账经验，
        /// 并复位标记（幂等：同一尸体只发一次——Update 的 Dying 分支每帧都会调）。
        /// </summary>
        [Test]
        public void GrantKillExperience_AddsToContext_AndResetsFlag()
        {
            var pig = Mob.Create(6, new Float3(5f, 70f, 5f));
            pig.KilledByPlayer = true; // 模拟 TakeHit 致死一击的标记
            Assume.That(_ctx.Experience.Current, Is.EqualTo(0), "前置：初始经验 0");

            _mgr.GrantKillExperience(pig);

            Assert.That(_ctx.Experience.Current, Is.EqualTo(3), "杀猪应 +3 经验");
            Assert.That(pig.KilledByPlayer, Is.False, "入账后应复位死因标记");

            _mgr.GrantKillExperience(pig); // Dying 倒计时内第二次调用：应 no-op
            Assert.That(_ctx.Experience.Current, Is.EqualTo(3),
                "标记已复位，重复入账不应再加经验（幂等）");
        }

        /// <summary>非玩家击杀（despawn / 苦力怕自爆等未置标记）不产生经验。</summary>
        [Test]
        public void GrantKillExperience_FlagNotSet_IsNoOp()
        {
            var zombie = Mob.Create(9, new Float3(5f, 70f, 5f)); // 未置 KilledByPlayer

            _mgr.GrantKillExperience(zombie);

            Assert.That(_ctx.Experience.Current, Is.EqualTo(0),
                "死因不可归玩家时不应入账经验");
        }
    }
}
#endif