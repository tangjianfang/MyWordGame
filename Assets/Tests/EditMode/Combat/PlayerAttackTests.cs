#if UNITY_EDITOR
// m9 A1：玩家攻击基础——空手可打 + 判定分流 + 冷却（修 milestone-9 诊断的断环①：
// 旧 CombatController 攻击被「选中物品 attackDamage>0」门禁，空手/木板对动物完全无效）。
// 依赖 MyWorld.Unity.Combat.CombatController，dotnet 链跑不动，
// 整个文件用 #if UNITY_EDITOR 包裹（与 MobDeathDropTests 同款）。
using System.Collections.Generic;
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
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

        // m9 A3：击杀掉肉（SpawnDropsForMob）与经验入账（GrantKillExperience）都住在 MobManager
        private MobManager _mgr;

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
        private GameObject SpawnPigAhead(float dist) => SpawnMobAhead(6, dist);

        /// <summary>
        /// m9 A3：<see cref="SpawnPigAhead"/> 的通用版——准星正前方造指定 mobTypeId 的 mob
        /// （鸡 8 / 僵尸 9 等），击杀链路测试用。
        /// </summary>
        private GameObject SpawnMobAhead(int mobTypeId, float dist)
        {
            var host = new GameObject("Mob" + mobTypeId + "@" + dist);
            _spawned.Add(host);
            host.transform.position = _eyeChild.transform.position
                                      + _eyeChild.transform.forward * dist;
            host.AddComponent<BoxCollider>(); // 默认 1×1×1
            var view = host.AddComponent<MobView>();
            view.Mob = Mob.Create(mobTypeId, new Float3(
                host.transform.position.x, host.transform.position.y, host.transform.position.z));
            // EditMode 下 transform 改动不自动同步物理世界（全限定：裸 Physics 会被
            // 解析成 MyWorld.Core.Physics 命名空间）
            UnityEngine.Physics.SyncTransforms();
            return host;
        }

        /// <summary>最小方块注册表：air + stone（solid）。视线遮挡测试只用到实心方块，
        /// 不必走 BlockRegistryLoader 读全表（与 BlockBreakDropTests.BuildRegistry 同款）。</summary>
        private static BlockRegistry BuildMinimalRegistry()
        {
            return BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            // fix1：两个指针门是静态状态，防别的 fixture 泄漏进来（也防本 fixture 泄漏出去）
            BlockInteraction.InputLocked = false;
            UiCursorGate.Reset();

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

            // m9 A3：MobManager 挂独立 host（world/time 留 null——测试不驱动 Update，
            // 只直调 SpawnDropsForMob / GrantKillExperience，与 MobDeathDropTests 同款）
            var mgrHost = new GameObject("PlayerAttackTestMobMgr");
            _spawned.Add(mgrHost);
            _mgr = mgrHost.AddComponent<MobManager>();
            _mgr.Bind(world: null, time: null, player: _playerHost.transform,
                generator: null, rules: null);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Reset(); // 清掉本 fixture 期间可能挂上的战斗事件订阅
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
        // world/registry 传 null = 未绑定世界（视线复核跳过），隔离纯射线行为

        [Test]
        public void IsMobInCrosshair_WithinRange_True()
        {
            SpawnPigAhead(2f);
            Assert.That(CombatController.IsMobInCrosshair(_eyeChild.transform, null, null), Is.True,
                "4m 内准星瞄着猪应判命中（BlockInteraction 据此抑制同帧挖矿）");
        }

        [Test]
        public void IsMobInCrosshair_BeyondRange_False()
        {
            SpawnPigAhead(6f);
            Assert.That(CombatController.IsMobInCrosshair(_eyeChild.transform, null, null), Is.False,
                "6m 外不在攻击射程内，不应抑制挖矿");
        }

        [Test]
        public void IsMobInCrosshair_NoMob_False()
        {
            Assert.That(CombatController.IsMobInCrosshair(_eyeChild.transform, null, null), Is.False,
                "准星前没有 mob 时不应抑制挖矿");
        }

        // ─── fix1（I1）：模态 UI 开着时不攻击（与 BlockInteraction 同款指针门） ──

        [Test]
        public void TryAttack_UiCursorGateOpen_NoEffect()
        {
            var pig = SpawnPigAhead(2f).GetComponent<MobView>().Mob;
            UiCursorGate.Open(); // 背包/工作台等模态 UI 打开（指针解锁可见）

            try
            {
                bool hit = _combat.TryAttack();
                Assert.That(hit, Is.False, "模态 UI 开着时不应能攻击（点 UI 格子不该误伤准星后的 mob）");
                Assert.That(pig.Health.Current, Is.EqualTo(10f), "UI 开着时的攻击不应扣血");
            }
            finally
            {
                UiCursorGate.Reset();
            }
        }

        [Test]
        public void TryAttack_HelpMenuInputLocked_NoEffect()
        {
            var pig = SpawnPigAhead(2f).GetComponent<MobView>().Mob;
            BlockInteraction.InputLocked = true; // HelpMenuUi 打开期间置位（帮助菜单滑条）

            try
            {
                bool hit = _combat.TryAttack();
                Assert.That(hit, Is.False, "InputLocked（帮助菜单）开着时不应能攻击");
                Assert.That(pig.Health.Current, Is.EqualTo(10f), "InputLocked 时的攻击不应扣血");
            }
            finally
            {
                BlockInteraction.InputLocked = false;
            }
        }

        // ─── fix1（I2）：视线遮挡——chunk mesh 没有 Physics collider，穿墙判定 ──

        [Test]
        public void TryAttack_WallBetween_BlocksAttack_WallRemoved_Hits()
        {
            // eye 在 (0,0,0) 朝 +Z；墙放在 (0,0,1)（玩家与 2m 处的猪之间）；猪 collider z∈[1.5,2.5]
            var world = new World();
            var registry = BuildMinimalRegistry();
            world.SetBlock(0, 0, 1, BlockIds.Stone);
            _combat.World = world;
            _combat.Registry = registry;
            var pig = SpawnPigAhead(2f).GetComponent<MobView>().Mob;

            // chunk mesh 无 collider → physics 射线穿墙仍会碰到猪的 collider；
            // 视线复核必须用体素射线把这次命中拦下来
            bool blocked = _combat.TryAttack();
            Assert.That(blocked, Is.False, "墙挡视线：physics 射线穿墙碰到 mob 也不应命中");
            Assert.That(pig.Health.Current, Is.EqualTo(10f), "隔墙攻击不应扣血");

            // 拆墙后视线通畅 → 命中（第一次挥击已消耗冷却，注入拨回）
            world.SetBlock(0, 0, 1, BlockIds.Air);
            _combat.LastAttackTime -= CombatController.AttackCooldown;
            bool hit = _combat.TryAttack();
            Assert.That(hit, Is.True, "拆墙后视线通畅，应命中");
            Assert.That(pig.Health.Current, Is.EqualTo(9f), "拆墙后空手伤害 1：10 → 9");
        }

        // ─── m9 A3：死亡统一序列——打死掉肉 + 击杀经验（修断环③，打猎闭环合龙） ──

        /// <summary>EditMode 下 Time.time 冻结，连续攻击前把冷却拨回（A1 同款时间注入）。</summary>
        private void RewindCooldown()
        {
            _combat.LastAttackTime -= CombatController.AttackCooldown;
        }

        /// <summary>
        /// 空手 4 下打死鸡（4 血 × 1 伤）→ 真实 drop_tables 链路掉生鸡肉：
        /// TakeHit 死亡分支写 LastDrops（chicken=1017）→ SpawnDropsForMob →
        /// PlayerContext.ItemDrops 出现可拾取的真物品。打死瞬间 LastDrops 非空即
        /// 「CombatController_NoDirectDying」的行为断言——击杀必经 MobAI 序列，
        /// 不再是旧 DoAttack 的直置 Dying（那条路 LastDrops 永远不可达）。
        /// </summary>
        [Test]
        public void KillAnimal_DropsMeat()
        {
            // 注入生产同源的 JSON 掉落表（WorldBootstrap 从 StreamingAssets 加载的那张）
            MobAI.DropTable = MobDropTable.Load(System.IO.Path.Combine(
                Application.streamingAssetsPath, "mobs", "drop_tables.json"));
            try
            {
                var chicken = SpawnMobAhead(8, 2f).GetComponent<MobView>().Mob; // 鸡 4 血
                Assume.That(chicken.Health.Current, Is.EqualTo(4f), "前置：鸡满血 4");

                for (int i = 0; i < 4; i++)
                {
                    RewindCooldown();
                    Assert.That(_combat.TryAttack(), Is.True, $"第 {i + 1} 击应命中");
                }

                Assert.That(chicken.Health.IsDead, Is.True, "空手 4 × 1 伤应打死 4 血鸡");
                Assert.That(chicken.State, Is.EqualTo(MobState.Dying),
                    "致死应由 MobAI.TakeHit 死亡分支转 Dying（CombatController 不再直置）");
                Assert.That(chicken.LastDrops, Is.Not.Null,
                    "打死瞬间 LastDrops 应非空——击杀路径必经 MobAI 死亡序列（断环③修复）");
                bool meatInDrops = false;
                foreach (var stack in chicken.LastDrops)
                {
                    if (stack.ItemId == ItemDropTable.ChickenItemId) meatInDrops = true;
                }
                Assert.That(meatInDrops, Is.True,
                    "LastDrops 应含 chicken (1017)——真实 drop_tables 链路");

                // 掉肉落场：LastDrops → SpawnDropsForMob → PlayerContext.ItemDrops
                _mgr.SpawnDropsForMob(chicken);
                bool meatOnGround = false;
                foreach (var d in _ctx.ItemDrops)
                {
                    if (d.Content.HasValue && d.Content.Value.ItemId == ItemDropTable.ChickenItemId)
                    {
                        meatOnGround = true;
                    }
                }
                Assert.That(meatOnGround, Is.True,
                    "打死鸡后地上应有可拾取的生鸡肉（打猎→掉肉→吃的闭环合龙）");
            }
            finally
            {
                MobAI.DropTable = null; // 静态注入不外泄给其它 fixture
            }
        }

        /// <summary>杀猪 +3 经验：木剑（4 伤）3 下打死 10 血猪，前两下不击杀不入账。</summary>
        [Test]
        public void KillGrantsExperience_Pig_Plus3()
        {
            var sword = _items.GetById("wooden_sword");
            Assume.That(sword.AttackDamage, Is.EqualTo(4f), "前置：wooden_sword.attackDamage=4");
            _ctx.Inventory.SetSlot(_ctx.Inventory.SelectedHotbarIndex,
                new ItemStack(sword.NumericId, 1));
            var pig = SpawnPigAhead(2f).GetComponent<MobView>().Mob;
            Assume.That(_ctx.Experience.Current, Is.EqualTo(0), "前置：初始经验 0");

            RewindCooldown(); _combat.TryAttack();
            RewindCooldown(); _combat.TryAttack();
            Assert.That(pig.Health.Current, Is.EqualTo(2f), "木剑 2 下：10 → 2");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(0), "未击杀不入账经验");
            Assert.That(pig.KilledByPlayer, Is.False, "未击杀不置死因标记");

            RewindCooldown();
            Assert.That(_combat.TryAttack(), Is.True, "第三击应命中");
            Assert.That(pig.State, Is.EqualTo(MobState.Dying), "木剑 3 下应打死猪");
            Assert.That(pig.KilledByPlayer, Is.True, "致死一击应置死因标记（经验入账依据）");

            _mgr.GrantKillExperience(pig);
            Assert.That(_ctx.Experience.Current, Is.EqualTo(3), "杀猪应 +3 经验");
        }

        /// <summary>杀僵尸 +10 经验：铁剑（10 伤）2 下打死 20 血僵尸。</summary>
        [Test]
        public void KillGrantsExperience_Zombie_Plus10()
        {
            var sword = _items.GetById("iron_sword");
            Assume.That(sword.AttackDamage, Is.EqualTo(10f), "前置：iron_sword.attackDamage=10");
            _ctx.Inventory.SetSlot(_ctx.Inventory.SelectedHotbarIndex,
                new ItemStack(sword.NumericId, 1));
            var zombie = SpawnMobAhead(9, 2f).GetComponent<MobView>().Mob; // 僵尸 20 血
            Assume.That(zombie.Health.Current, Is.EqualTo(20f), "前置：僵尸满血 20");

            RewindCooldown(); _combat.TryAttack();
            RewindCooldown(); Assert.That(_combat.TryAttack(), Is.True, "第二击应命中");

            Assert.That(zombie.State, Is.EqualTo(MobState.Dying), "铁剑 2 下应打死僵尸");
            Assert.That(zombie.KilledByPlayer, Is.True, "致死一击应置死因标记");

            _mgr.GrantKillExperience(zombie);
            Assert.That(_ctx.Experience.Current, Is.EqualTo(10), "杀僵尸应 +10 经验");
        }

        /// <summary>
        /// A1 评审 O1 直测（挖矿分流方向 2）：准星与 mob 之间有实心方块 →
        /// <see cref="CombatController.IsMobInCrosshair"/> 为 false，BlockInteraction
        /// 挖矿照常（正好挖那堵墙）；拆墙后信号恢复 true（与方向 1 对称）。
        /// </summary>
        [Test]
        public void Dig_WhenMobBehindWall_Proceeds()
        {
            var world = new World();
            var registry = BuildMinimalRegistry();
            world.SetBlock(0, 0, 1, BlockIds.Stone); // eye(0,0,0) 与 2m 处猪之间的墙
            SpawnPigAhead(2f);

            Assert.That(
                CombatController.IsMobInCrosshair(_eyeChild.transform, world, registry),
                Is.False, "墙后有 mob：视线被实心方块挡住，不应抑制挖矿（正好挖那堵墙）");

            world.SetBlock(0, 0, 1, BlockIds.Air);
            Assert.That(
                CombatController.IsMobInCrosshair(_eyeChild.transform, world, registry),
                Is.True, "拆墙后视线通畅，mob 信号应恢复 true（方向 1 对称面）");
        }

        // ─── m10 B1：攻击路径的耐久初始化以 JSON maxDurability 为准 ──────────

        /// <summary>
        /// m10 B1 一致性：镐类已声明 maxDurability，攻击磨损的上限必须取 JSON 值；
        /// 未声明的旧工具（剑/斧/锹）沿用 m3 的 MiningLevel 档位表。
        /// 不统一的话：孩子先拿镐打一下怪再挖矿，镐上限就被 m3 旧表钉死
        /// （木镐 35 而不是 59），hotbar 耐久条与 JSON 永远对不上。
        /// </summary>
        [Test]
        public void TryAttack_WithPickaxe_InitializesDurabilityFromItemJson()
        {
            var pick = _items.GetById("wooden_pickaxe");
            Assume.That(pick.MaxDurability, Is.EqualTo(59),
                "前置：wooden_pickaxe.maxDurability 应为 59（MC 原值）");
            _ctx.Inventory.SetSlot(_ctx.Inventory.SelectedHotbarIndex,
                new ItemStack(pick.NumericId, 1));

            _combat.TryAttack(); // 没有 mob 也照走耐久分支（m3 起挥击即磨损）

            var stack = _ctx.Inventory.GetSelected();
            Assert.That(stack.HasDurability, Is.True, "挥击一次后耐久位应已写入（m3 行为）");
            Assert.That(stack.MaxDurability, Is.EqualTo(59),
                "上限应取 JSON maxDurability，不是 m3 旧档位表的 35");
            Assert.That(stack.CurrentDurability, Is.EqualTo(58), "挥击一次扣 1 点");
        }
    }
}
#endif
