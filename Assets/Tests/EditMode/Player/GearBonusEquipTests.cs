#if UNITY_EDITOR
// m10 C1：手持装备生效——PlayerContext 三属性刷新 / TakeDamage 减伤 /
// PlayerMotor 移速接线 / Respawn 回满到有效上限。
// 依赖 MyWorld.Unity 的 PlayerContext / PlayerController 与真实 items/*.json，
// dotnet 链跑不动，整个文件用 #if UNITY_EDITOR 包裹（与 PlayerPickupDamageTests 同款）；
// Core 纯计算在 GearBonusTests（双链跑）。
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class GearBonusEquipTests
    {
        private GameObject _go;
        private PlayerController _player;
        private PlayerContext _ctx;
        private ItemDatabase _db;

        [SetUp]
        public void SetUp()
        {
            // 真实物品表：四系装备的 gearBonus / attackDamage 断言要用真 JSON，
            // 不在本测试里手拼（手拼就测不到数据文件本身）
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            _db = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));

            _go = new GameObject("玩家");
            _ctx = _go.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，显式初始化（与 PlayerPickupDamageTests 同款）
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _ctx.Items = _db;
            _player = _go.AddComponent<PlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        /// <summary>把物品放进 0 号热键格并选中（简化模型：加成只看选中格）。</summary>
        private void Hold(string itemId)
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(_db.GetById(itemId).NumericId, 1));
            _ctx.Inventory.SelectedHotbarIndex = 0;
        }

        /// <summary>清空选中格（切走装备 = 空手）。</summary>
        private void Sheathe()
        {
            _ctx.Inventory.SetSlot(0, ItemStack.Empty);
        }

        // ─── 手持即生效 / 切走失效 ────────────────────────────────────────────

        [Test]
        public void 手持铁剑_防御一点_其余为零()
        {
            Hold("iron_sword");
            _ctx.RefreshGearBonuses();

            Assert.That(_ctx.Defense, Is.EqualTo(1), "铁系装备 gearBonus = defense +1");
            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0f), "铁剑不带移速加成");
            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(0), "铁剑不带生命上限加成");
        }

        [Test]
        public void 切走装备_三属性归零()
        {
            Hold("iron_sword");
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(1), "前置：手持时有加成");

            Sheathe();
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(0), "切走即失效——加成挂在选中物品上");
            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(0));
        }

        [Test]
        public void 手持合金剑_移速加成百分之五()
        {
            Hold("summer_alloy_sword");
            _ctx.RefreshGearBonuses();

            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0.05f).Within(1e-6f),
                "合金系装备 gearBonus = moveSpeed +5%");
            Assert.That(_ctx.Defense, Is.EqualTo(0), "合金剑不带防御");
        }

        [Test]
        public void 手持机元件_生命上限加二_超上限钳制()
        {
            Hold("machine_essence_sword");
            _ctx.RefreshGearBonuses();

            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(2), "机元系装备 gearBonus = maxHealth +2");
            Assert.That(_ctx.EffectiveMaxHealth, Is.EqualTo(22f), "有效血上限 = 20 + 2");

            // 钳制发生在血量刷新处：当前血超有效上限当场收回
            _ctx.Health.Current = 25f;
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Health.Current, Is.EqualTo(22f), "25 血钳回 22");

            // 切走装备：上限回到 20，多出来的 2 点也收回
            Sheathe();
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.EffectiveMaxHealth, Is.EqualTo(20f));
            Assert.That(_ctx.Health.Current, Is.EqualTo(20f), "切走后 22 血钳回 20");
        }

        [Test]
        public void 空手或非装备物品_三属性为零()
        {
            _ctx.RefreshGearBonuses(); // 什么都没拿
            Assert.That(_ctx.Defense, Is.EqualTo(0));
            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(0));

            Hold("plank"); // 普通物品没有 gearBonus
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(0), "木板不是装备，不产生加成");
        }

        // ─── TakeDamage 减伤接线 ──────────────────────────────────────────────

        [Test]
        public void TakeDamage_按防御减伤_最低一点()
        {
            Hold("iron_sword");
            _ctx.RefreshGearBonuses();

            _player.TakeDamage(5f, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(16f), "20 - max(1, 5-1) = 16");

            _player.TakeDamage(1f, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(15f), "1 伤害撞 1 防御保底 1 伤，不无敌");
        }

        [Test]
        public void TakeDamage_无防御_伤害原样进入()
        {
            Sheathe();
            _ctx.RefreshGearBonuses();

            _player.TakeDamage(0.5f, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(19.5f).Within(1e-4f),
                "零防御不碰小数伤害——m10 B2 碎块 0.5 扎脚语义不变");
        }

        // ─── PlayerMotor 移速接线 ────────────────────────────────────────────

        [Test]
        public void Tick_把移速加成同步进运动参数_切走归零()
        {
            // 绑一个小世界（地面一层石头），让 Tick 走到运动解算前的装备同步分支
            var world = new World();
            for (var x = -16; x < 16; x++)
            for (var z = -16; z < 16; z++) world.SetBlock(x, 63, z, BlockIds.Stone);
            var registry = BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true }"
            });
            _player.Bind(world, registry, new Float3(0f, 64f, 0f));

            Hold("summer_alloy_sword");
            _ctx.RefreshGearBonuses();
            _player.Tick(PlayerInput.None, 1f / 60f);
            Assert.That(_player.Settings.MoveSpeedBonus, Is.EqualTo(0.05f).Within(1e-6f),
                "手持合金剑：PlayerMotor 目标速度乘 (1 + 5%)");

            Sheathe();
            _ctx.RefreshGearBonuses();
            _player.Tick(PlayerInput.None, 1f / 60f);
            Assert.That(_player.Settings.MoveSpeedBonus, Is.EqualTo(0f), "切走后同步回 0");
        }

        // ─── Respawn 回满到有效上限 ──────────────────────────────────────────

        [Test]
        public void Respawn_回满到有效上限_机元件复活多两点血()
        {
            Hold("machine_essence_sword");
            _ctx.RefreshGearBonuses();
            _ctx.Health.Current = 1f; // 濒死

            _player.Respawn(Vector3.zero);
            Assert.That(_ctx.Health.Current, Is.EqualTo(22f),
                "手持机元件复活 = 回满到有效上限 22，不是基础 20");

            Sheathe();
            _player.Respawn(Vector3.zero);
            Assert.That(_ctx.Health.Current, Is.EqualTo(20f), "空手复活回基础 20");
        }

        // ─── 真实 items/*.json 的四系装备数值守卫 ─────────────────────────────

        [Test]
        public void 真实物品表_四系装备加成字段齐全()
        {
            // 铁系（既有两件补字段）：防御 +1
            foreach (string id in new[] { "iron_sword", "iron_pickaxe" })
            {
                var def = _db.GetById(id);
                Assert.That(def.GearStat, Is.EqualTo(GearStat.Defense), $"{id} 应带 defense 加成");
                Assert.That(def.GearAmount, Is.EqualTo(1f), $"{id} 防御 +1");
            }

            // 合金系（新建剑+镐）：移速 +5%
            foreach (string id in new[] { "summer_alloy_sword", "summer_alloy_pickaxe" })
            {
                var def = _db.GetById(id);
                Assert.That(def.GearStat, Is.EqualTo(GearStat.MoveSpeed), $"{id} 应带 moveSpeed 加成");
                Assert.That(def.GearAmount, Is.EqualTo(0.05f).Within(1e-6f), $"{id} 移速 +5%");
            }

            // 机元系（新建剑+镐）：生命上限 +2
            foreach (string id in new[] { "machine_essence_sword", "machine_essence_pickaxe" })
            {
                var def = _db.GetById(id);
                Assert.That(def.GearStat, Is.EqualTo(GearStat.MaxHealth), $"{id} 应带 maxHealth 加成");
                Assert.That(def.GearAmount, Is.EqualTo(2f), $"{id} 生命上限 +2");
            }
        }

        [Test]
        public void 真实物品表_金系从零建_攻击走attackDamage且门槛耐久对齐()
        {
            // A3 fix1 勘误：spec §3「既有金装备」不实——金剑/金镐由本 task 从零建。
            // 金系属性 = 攻击，走既有 attackDamage 通道，不写 gearBonus
            var sword = _db.GetById("gold_sword");
            Assert.That(sword.AttackDamage, Is.EqualTo(5f),
                "金剑攻击 = 木剑 4 + 1（spec §3「+1/件 叠加在武器 attackDamage 上」）");
            Assert.That(sword.GearStat, Is.EqualTo(GearStat.None), "金系不走 gearBonus 通道");

            var pickaxe = _db.GetById("gold_pickaxe");
            Assert.That(pickaxe.ToolTier, Is.EqualTo(2), "金镐门槛 = 石镐级（MC 金镐等同石镐）");
            Assert.That(pickaxe.MaxDurability, Is.EqualTo(32), "金镐耐久 32（MC 值）");
            Assert.That(pickaxe.GearStat, Is.EqualTo(GearStat.None));
        }
    }
}
#endif
