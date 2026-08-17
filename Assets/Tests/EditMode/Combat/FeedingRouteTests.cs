#if UNITY_EDITOR
// m11 ②C（任务 2）：繁殖喂食右键路由守卫。CombatController.TryFeedMobInCrosshair——
// 右键持对应饲料（BreedingSystem.FeedItemFor）+ 准星 4m 内瞄着 mob 时改走
// PlayerContext.BreedingSystem 的喂食（TryFeed），扣 1 个饲料并让出本次右键
//（置 BlockInteraction.InputLocked 一帧，防 beet/mung_bean 被「食物优先」分支双扣）。
// 左键攻击（TryAttack）不受影响。依赖 MyWorld.Unity 程序集与 GameObject，
// dotnet 链跑不动，整个文件用 #if UNITY_EDITOR 包裹（与 PlayerAttackTests 同款）。
using System.Collections.Generic;
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Farming;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// m11 ②C 契约：
    /// 1) 右键 + 手持该 mob 的饲料 + 准星 4m 内是 mob → BreedingSystem.TryFeed 被调
    ///    （FedEntityIds 出现该实体）、扣 1 个饲料、本次右键被让出（InputLocked 置位、
    ///    LateUpdate 释放——锁窗口恰好一帧）。
    /// 2) 饲料不匹配 / 无 mob / 超距离 / 模态 UI 开着 → 不喂不扣，右键落回既有路由。
    /// 3) 已发情重复喂 → TryFeed 拒掉，不扣第二份。
    /// 4) 左键攻击不受喂食路由影响（喂完照常挥击扣血）。
    /// </summary>
    [TestFixture]
    public class FeedingRouteTests
    {
        private GameObject _ctxHost;
        private GameObject _playerHost;
        private GameObject _eyeChild;
        private PlayerContext _ctx;
        private CombatController _combat;
        private ItemDatabase _items;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake，
        /// 用反射显式调用（与 PlayerAttackTests / MobDeathDropTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        /// <summary>喂食让位锁的释放点也是私有 LateUpdate——EditMode 不自动驱动，同款反射直调。</summary>
        private static void InvokeLateUpdate(CombatController combat)
        {
            var method = combat.GetType().GetMethod("LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "CombatController 应有私有 LateUpdate（喂食让位锁的释放点）");
            method.Invoke(combat, null);
        }

        /// <summary>准星正前方 dist 米处造一头指定 kind 的 mob（BoxCollider + MobView，
        /// 攻击/喂食的准星射线都只认这个组合）。EntityId 手工指派，FedEntityIds 断言可读。</summary>
        private Mob SpawnMobAhead(MobKind kind, float dist, int entityId)
        {
            var host = new GameObject("Mob" + kind + "@" + dist);
            _spawned.Add(host);
            host.transform.position = _eyeChild.transform.position
                                      + _eyeChild.transform.forward * dist;
            host.AddComponent<BoxCollider>();
            var view = host.AddComponent<MobView>();
            view.Mob = Mob.Create(KindToTypeId(kind), new Float3(
                host.transform.position.x, host.transform.position.y, host.transform.position.z));
            view.Mob.EntityId = entityId;
            // EditMode 下 transform 改动不自动同步物理世界
            UnityEngine.Physics.SyncTransforms();
            return view.Mob;
        }

        /// <summary>Kind → Mob.Create 的 mobTypeId（6 猪 / 7 牛 / 8 鸡，Mob.Create 分档建档）。</summary>
        private static int KindToTypeId(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig: return 6;
                case MobKind.Cow: return 7;
                case MobKind.Chicken: return 8;
                default: return 1;
            }
        }

        /// <summary>把 num 个 itemId 物品放进选中槽（喂食扣的就是这一格）。</summary>
        private void HoldItem(int itemId, int num)
        {
            _ctx.Inventory.SetSlot(_ctx.Inventory.SelectedHotbarIndex, new ItemStack(itemId, num));
        }

        [SetUp]
        public void SetUp()
        {
            // 静态门是全局状态，防别的 fixture 泄漏进来（也防本 fixture 泄漏出去）
            BlockInteraction.InputLocked = false;
            UiCursorGate.Reset();

            _ctxHost = new GameObject("FeedingRouteTestCtx");
            _ctx = _ctxHost.AddComponent<PlayerContext>();
            InvokeAwake(_ctx); // 显式触发 Awake，让 Instance = _ctx
            _items = ItemDatabaseLoader.Load(); // 真物品表（StreamingAssets/items）
            _ctx.Items = _items;
            _ctx.BreedingSystem = new BreedingSystem(); // 真源同 WorldBootstrap：挂 PlayerContext

            _playerHost = new GameObject("FeedingRouteTestPlayer");
            _eyeChild = new GameObject("相机"); // PlayerController.Awake 会把它绑成 Eye
            _eyeChild.transform.SetParent(_playerHost.transform);
            _eyeChild.transform.position = Vector3.zero;
            _eyeChild.transform.rotation = Quaternion.identity; // 朝 +Z，mob 都放在 +Z 轴上
            var player = _playerHost.AddComponent<PlayerController>();
            InvokeAwake(player);

            _combat = _playerHost.AddComponent<CombatController>();
            _combat.Player = player; // Hand 留 null：挥手动画 null 安全跳过
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Reset();
            BlockInteraction.InputLocked = false;
            UiCursorGate.Reset();
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            if (_playerHost != null) Object.DestroyImmediate(_playerHost);
            if (_ctxHost != null) Object.DestroyImmediate(_ctxHost);
        }

        [Test]
        public void 右键持对应饲料命中mob_喂食登记扣1并让出右键()
        {
            var wheat = _items.GetById("wheat");
            var cow = SpawnMobAhead(MobKind.Cow, 2f, entityId: 31); // 牛的饲料是 wheat
            HoldItem(wheat.NumericId, 3);

            bool fed = _combat.TryFeedMobInCrosshair();

            Assert.That(fed, Is.True, "手持小麦 + 准星 2m 内是牛，右键应喂食");
            Assert.That(_ctx.BreedingSystem.FedEntityIds, Does.Contain(31),
                "BreedingSystem.TryFeed 应被调（发情名单出现该实体）");
            Assert.That(_ctx.Inventory.CountOf(wheat.NumericId), Is.EqualTo(2),
                "喂食应扣 1 个饲料（3 → 2）");
            Assert.That(cow.Health.Current, Is.EqualTo(15f), "喂食不伤 mob（15 血牛原样）");
            Assert.That(BlockInteraction.InputLocked, Is.True,
                "喂食让出的右键应置 InputLocked——BlockInteraction 本帧不得再吃 beet/mung_bean 第二份");

            InvokeLateUpdate(_combat);
            Assert.That(BlockInteraction.InputLocked, Is.False,
                "LateUpdate（所有 Update 之后）应释放让位锁——窗口恰好一帧");

            // 已发情重复喂：TryFeed 拒掉 → 右键不消费、不扣第二份（落回既有路由）
            bool again = _combat.TryFeedMobInCrosshair();
            Assert.That(again, Is.False, "已发情的 mob 重复喂应被 TryFeed 拒掉");
            Assert.That(_ctx.Inventory.CountOf(wheat.NumericId), Is.EqualTo(2), "被拒的喂食不扣饲料");
        }

        [Test]
        public void 饲料不匹配无mob或超距离_右键不喂不扣()
        {
            var wheat = _items.GetById("wheat");
            HoldItem(wheat.NumericId, 3);

            // 无 mob：右键不归喂食
            Assert.That(_combat.TryFeedMobInCrosshair(), Is.False, "准星前没有 mob 不应喂食");
            Assert.That(_ctx.BreedingSystem.FedEntityIds.Count, Is.EqualTo(0), "不应登记发情");
            Assert.That(_ctx.Inventory.CountOf(wheat.NumericId), Is.EqualTo(3), "不应扣物品");

            // 饲料不匹配：鸡要麦种（seeds_wheat），手持小麦喂不动
            SpawnMobAhead(MobKind.Chicken, 2f, entityId: 32);
            var chickenHost = _spawned[_spawned.Count - 1];
            Assert.That(_combat.TryFeedMobInCrosshair(), Is.False,
                "小麦不是鸡的饲料，右键不应被喂食消费（落回 BlockInteraction 既有路由）");
            Assert.That(_ctx.BreedingSystem.FedEntityIds.Count, Is.EqualTo(0), "不匹配不应登记发情");
            Assert.That(_ctx.Inventory.CountOf(wheat.NumericId), Is.EqualTo(3), "不匹配不应扣物品");
            Assert.That(BlockInteraction.InputLocked, Is.False, "未消费的右键不得置让位锁");

            // 超距离：先清掉 2m 的鸡（别挡射线），AttackRange=4 外的牛（6m）够不着
            _spawned.Remove(chickenHost);
            Object.DestroyImmediate(chickenHost);
            UnityEngine.Physics.SyncTransforms();
            SpawnMobAhead(MobKind.Cow, 6f, entityId: 33);
            Assert.That(_combat.TryFeedMobInCrosshair(), Is.False, "6m 外超出准星 4m 射程，不应喂食");
            Assert.That(_ctx.BreedingSystem.FedEntityIds.Count, Is.EqualTo(0), "超距离不应登记发情");
        }

        [Test]
        public void 模态UI开着不喂_与攻击同款指针门()
        {
            var beet = _items.GetById("beet");
            SpawnMobAhead(MobKind.Pig, 2f, entityId: 34); // 猪的饲料恰是 beet
            HoldItem(beet.NumericId, 2);
            UiCursorGate.Open(); // 背包/工作台等模态 UI 打开（指针解锁可见）

            try
            {
                Assert.That(_combat.TryFeedMobInCrosshair(), Is.False,
                    "模态 UI 开着时不应隔着界面喂 mob");
                Assert.That(_ctx.BreedingSystem.FedEntityIds.Count, Is.EqualTo(0), "不应登记发情");
                Assert.That(_ctx.Inventory.CountOf(beet.NumericId), Is.EqualTo(2), "不应扣甜菜");
            }
            finally
            {
                UiCursorGate.Reset();
            }
        }

        [Test]
        public void 左键攻击不受喂食路由影响_喂完照常挥击()
        {
            var beet = _items.GetById("beet");
            var pig = SpawnMobAhead(MobKind.Pig, 2f, entityId: 35); // 猪的饲料恰是 beet
            HoldItem(beet.NumericId, 2);

            Assert.That(_combat.TryFeedMobInCrosshair(), Is.True, "前置：先喂一口");
            InvokeLateUpdate(_combat); // 释放让位锁（运行时由 LateUpdate 自动做）

            // 左键攻击照常：甜菜非武器 → 空手 1 伤（ResolveAttackDamage 三态的第三态）
            _combat.LastAttackTime -= CombatController.AttackCooldown; // EditMode 时间注入
            bool hit = _combat.TryAttack();

            Assert.That(hit, Is.True, "喂食路由加入后左键攻击应照常命中");
            Assert.That(pig.Health.Current, Is.EqualTo(9f), "空手 1 伤：10 → 9（甜菜非武器）");
            Assert.That(_ctx.Inventory.CountOf(beet.NumericId), Is.EqualTo(1),
                "攻击不消耗食物（beet 非工具，不进耐久磨损分支）：喂食扣 1 后保持 1");
            Assert.That(_ctx.BreedingSystem.FedEntityIds, Does.Contain(35),
                "挥击不影响已登记的发情");
        }
    }
}
#endif
