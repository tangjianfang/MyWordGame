#if UNITY_EDITOR
// m9 A1：玩家攻击基础——空手可打 + 判定分流 + 冷却（修 milestone-9 诊断的断环①：
// 旧 CombatController 攻击被「选中物品 attackDamage>0」门禁，空手/木板对动物完全无效）。
// 依赖 MyWorld.Unity.Combat.CombatController，dotnet 链跑不动，
// 整个文件用 #if UNITY_EDITOR 包裹（与 MobDeathDropTests 同款）。
using System.Collections.Generic;
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// m9 A1 契约：
    /// 1) <see cref="CombatController.ResolveAttackDamage"/> 三态：空手/非武器返 1，武器返物品表 attackDamage。
    /// 2) <see cref="CombatController.TryAttack"/> 空手也能打：10 血猪 -1（不再被武器门禁拦下）。
    /// 3) 冷却节流：0.5s 内第二次挥击无效果（EditMode 下 Time.time 冻结 + LastAttackTime 字段直改注入）。
    /// 4) 超距离无伤：mob 在 AttackRange=4m 外时挥空。
    /// 5) 判定分流：<see cref="CombatController.IsMobInCrosshair"/>——mob 命中优先于挖掘的信号。
    /// </summary>
    [TestFixture]
    public class PlayerAttackTests
    {
        private GameObject _ctxHost;
        private GameObject _playerHost;
        private GameObject _eyeChild;
        private PlayerContext _ctx;
        private CombatController _combat;
        private ItemDatabase _items;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake，
        /// 用反射显式调用（与 MobDeathDropTests / PlayerControllerEyeTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        /// <summary>
        /// 在准星正前方 <paramref name="dist"/> 米处造一头猪（mobTypeId 6，10 血）。
        /// 攻击判定只认「host collider + MobView.Mob」（部位 cube 的 collider 已被 MobAssembly
        /// 移除），所以测试不走 MobView.Attach 的部位拼装，直接挂 BoxCollider + MobView。
        /// </summary>
        private GameObject SpawnPigAhead(float dist)
        {
            var host = new GameObject("Pig@" + dist);
            _spawned.Add(host);
            host.transform.position = _eyeChild.transform.position
                                      + _eyeChild.transform.forward * dist;
            host.AddComponent<BoxCollider>(); // 默认 1×1×1
            var view = host.AddComponent<MobView>();
            view.Mob = Mob.Create(6, new Float3(
                host.transform.position.x, host.transform.position.y, host.transform.position.z));
            // EditMode 下 transform 改动不自动同步物理世界（全限定：裸 Physics 会被
            // 解析成 MyWorld.Core.Physics 命名空间）
            UnityEngine.Physics.SyncTransforms();
            return host;
        }

        [SetUp]
        public void SetUp()
        {
            _ctxHost = new GameObject("PlayerAttackTestCtx");
            _ctx = _ctxHost.AddComponent<PlayerContext>();
            InvokeAwake(_ctx); // 显式触发 Awake，让 Instance = _ctx
            _items = ItemDatabaseLoader.Load(); // 真物品表（StreamingAssets/items）
            _ctx.Items = _items;

            _playerHost = new GameObject("PlayerAttackTestPlayer");
            _eyeChild = new GameObject("相机"); // PlayerController.Awake 会把它绑成 Eye
            _eyeChild.transform.SetParent(_playerHost.transform);
            _eyeChild.transform.position = Vector3.zero;
            _eyeChild.transform.rotation = Quaternion.identity; // 朝 +Z，mob 都放在 +Z 轴上
            var player = _playerHost.AddComponent<PlayerController>();
            InvokeAwake(player);

            _combat = _playerHost.AddComponent<CombatController>();
            _combat.Player = player; // Hand 留 null：TryAttack 对 null Hand 安全跳过
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Reset(); // 清掉本 fixture 期间可能挂上的战斗事件订阅
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            if (_playerHost != null) Object.DestroyImmediate(_playerHost);
            if (_ctxHost != null) Object.DestroyImmediate(_ctxHost);
        }

        // ─── 伤害解析：纯函数三态 ───────────────────────────────────────────

        [Test]
        public void ResolveAttackDamage_EmptyHand_Returns1()
        {
            Assert.That(CombatController.ResolveAttackDamage(null, _items), Is.EqualTo(1),
                "null 栈（空手）应返 1");
            Assert.That(CombatController.ResolveAttackDamage(ItemStack.Empty, _items), Is.EqualTo(1),
                "空栈应返 1");
            Assert.That(CombatController.ResolveAttackDamage(new ItemStack(1100, 1), null), Is.EqualTo(1),
                "无物品表时无从谈武器，按空手 1 兜底");
        }

        [Test]
        public void ResolveAttackDamage_Plank_Returns1()
        {
            // 木板：plank.json 没有 attackDamage 字段——旧门禁下这正是断环①的现场（握木板打不动猪）
            var plank = _items.GetById("plank");
            Assume.That(plank.AttackDamage, Is.Null, "前置：plank.json 不应有 attackDamage（非武器）");
            Assert.That(
                CombatController.ResolveAttackDamage(new ItemStack(plank.NumericId, 1), _items),
                Is.EqualTo(1), "非武器物品手握时伤害应为 1（空手等价）");
        }

        [Test]
        public void ResolveAttackDamage_WoodenSword_ReturnsWeaponDamage()
        {
            // 真物品表数值：wooden_sword.json attackDamage=4（numericId 1100）
            var sword = _items.GetById("wooden_sword");
            Assume.That(sword.AttackDamage, Is.EqualTo(4f), "前置：wooden_sword.attackDamage 应为 4");
            Assert.That(
                CombatController.ResolveAttackDamage(new ItemStack(sword.NumericId, 1), _items),
                Is.EqualTo(4), "武器伤害应取物品表 attackDamage");
        }

        // ─── TryAttack 行为 ─────────────────────────────────────────────────

        [Test]
        public void TryAttack_EmptyHand_DamagesMob()
        {
            var pig = SpawnPigAhead(2f).GetComponent<MobView>().Mob;
            Assume.That(pig.Health.Current, Is.EqualTo(10f), "前置：猪满血 10");

            bool hit = _combat.TryAttack();

            Assert.That(hit, Is.True, "2m 内（< AttackRange=4）准星瞄着猪，TryAttack 应命中");
            Assert.That(pig.Health.Current, Is.EqualTo(9f), "空手伤害 1：10 血猪应剩 9（断环①修复）");
        }

        [Test]
        public void TryAttack_WithWoodenSword_DealsWeaponDamage()
        {
            var sword = _items.GetById("wooden_sword");
            _ctx.Inventory.SetSlot(_ctx.Inventory.SelectedHotbarIndex,
                new ItemStack(sword.NumericId, 1));
            var pig = SpawnPigAhead(2f).GetComponent<MobView>().Mob;

            _combat.TryAttack();

            Assert.That(pig.Health.Current, Is.EqualTo(6f), "木剑 attackDamage=4：10 血猪应剩 6");
        }

        [Test]
        public void TryAttack_CooldownThrottles()
        {
            var pig = SpawnPigAhead(2f).GetComponent<MobView>().Mob;

            bool first = _combat.TryAttack();
            Assert.That(first, Is.True, "首击不应被冷却拦截（LastAttackTime 初始为 -∞）");
            Assert.That(pig.Health.Current, Is.EqualTo(9f), "首击空手伤害 1");

            // EditMode 下 Time.time 冻结：距上次挥击 0s < 0.5s 冷却 → 节流
            bool second = _combat.TryAttack();
            Assert.That(second, Is.False, "0.5s 冷却内的第二次挥击应被节流");
            Assert.That(pig.Health.Current, Is.EqualTo(9f), "被节流的挥击不应再扣血");

            // 时间注入（字段直改）：把上次挥击时刻拨回冷却之前 → 第三击应生效
            _combat.LastAttackTime -= CombatController.AttackCooldown;
            bool third = _combat.TryAttack();
            Assert.That(third, Is.True, "冷却过去后应能再次命中");
            Assert.That(pig.Health.Current, Is.EqualTo(8f), "第三击空手伤害 1：9 → 8");
        }

        [Test]
        public void TryAttack_BeyondRange_NoDamage()
        {
            var pig = SpawnPigAhead(6f).GetComponent<MobView>().Mob; // brief：mob 在 5m 外

            bool hit = _combat.TryAttack();

            Assert.That(hit, Is.False, "6m 外超出 AttackRange=4，射线不应命中");
            Assert.That(pig.Health.Current, Is.EqualTo(10f), "超距离挥空不应扣血");
        }

        // ─── 判定分流：mob 命中优先于挖掘 ──────────────────────────────────

        [Test]
        public void IsMobInCrosshair_WithinRange_True()
        {
            SpawnPigAhead(2f);
            Assert.That(CombatController.IsMobInCrosshair(_eyeChild.transform), Is.True,
                "4m 内准星瞄着猪应判命中（BlockInteraction 据此抑制同帧挖矿）");
        }

        [Test]
        public void IsMobInCrosshair_BeyondRange_False()
        {
            SpawnPigAhead(6f);
            Assert.That(CombatController.IsMobInCrosshair(_eyeChild.transform), Is.False,
                "6m 外不在攻击射程内，不应抑制挖矿");
        }

        [Test]
        public void IsMobInCrosshair_NoMob_False()
        {
            Assert.That(CombatController.IsMobInCrosshair(_eyeChild.transform), Is.False,
                "准星前没有 mob 时不应抑制挖矿");
        }
    }
}
#endif
