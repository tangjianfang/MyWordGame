#if UNITY_EDITOR
// m11 W2-1：穿戴栏模型——PlayerContext 三属性汇总改「穿戴盔甲 + 手持装备」双源。
// 依赖真实 items/*.json（含本卡注册的 16 件盔甲）与 PlayerContext MonoBehaviour，
// dotnet 链跑不动，整个文件 #if UNITY_EDITOR 包裹（与 GearBonusEquipTests 同款）；
// 穿戴栏 Core 纯逻辑在 ArmorInventoryTests（双链跑）。
using System.IO;
using System.Linq;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class ArmorWearTests
    {
        private GameObject _go;
        private PlayerContext _ctx;
        private ItemDatabase _db;

        [SetUp]
        public void SetUp()
        {
            // 真实物品表：穿戴叠加/部位守卫断言要打真 JSON 数据，不在测试里手拼
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            _db = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));

            _go = new GameObject("玩家");
            _ctx = _go.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，显式初始化（与 GearBonusEquipTests 同款）
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _ctx.Items = _db;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        /// <summary>把物品放进背包 fromIndex 并穿进对应部位槽。</summary>
        private void Wear(string itemId, int fromIndex = 0)
        {
            _ctx.Inventory.SetSlot(fromIndex, new ItemStack(_db.GetById(itemId).NumericId, 1));
            Assert.That(_ctx.ArmorSlots.TryEquipFrom(_ctx.Inventory, fromIndex, _db),
                Is.True, $"前置：{itemId} 应可穿戴");
        }

        private void Hold(string itemId)
        {
            _ctx.Inventory.SetSlot(1, new ItemStack(_db.GetById(itemId).NumericId, 1));
            _ctx.Inventory.SelectedHotbarIndex = 1;
        }

        // ─── 双源汇总：穿戴盔甲逐件累计 ─────────────────────────────────

        [Test]
        public void 穿戴铁甲四件_防御按件求和()
        {
            Wear("iron_helmet", 0);
            Wear("iron_chest", 2);
            Wear("iron_legs", 3);
            Wear("iron_boots", 4);

            _ctx.RefreshGearBonuses();

            // 铁四件数值：头 1 / 胸 2 / 腿 2 / 脚 1（items/*.json 数据，守卫测试钉住）
            Assert.That(_ctx.Defense, Is.EqualTo(6), "穿戴防御 = 四件逐件求和，不再只看手持一件");
            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0f), "铁甲不带移速");
            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(0), "铁甲不带生命上限");
        }

        [Test]
        public void 穿戴与手持双源_同帧并存()
        {
            Wear("summer_alloy_boots", 0);        // 穿：移速 +0.02
            Hold("machine_essence_sword");        // 手持：生命上限 +2（m10 旧模型原样）

            _ctx.RefreshGearBonuses();

            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0.02f).Within(1e-6f), "穿戴源：合金靴移速 +2%");
            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(2), "手持源：机元件血上限 +2 语义不变");
            Assert.That(_ctx.Defense, Is.EqualTo(0), "两件都不带防御");
        }

        [Test]
        public void 手持迁移语义不变_拿着未穿的头盔照样生效()
        {
            // m10 简化模型「手持带 gearBonus 即生效」必须原样保留——
            // 手里拿着一件**没穿**的盔甲，它的 gearBonus 仍从手持源计入
            Hold("iron_helmet");
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(1), "手持头盔防御 +1（手持源不因穿戴模型退役）");

            // 穿上身（手上没了）：加成改走穿戴源，数值不变——玩家无感迁移
            _ctx.ArmorSlots.TryEquipFrom(_ctx.Inventory, 1, _db);
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(1), "穿上后同数值改由穿戴源提供");
        }

        [Test]
        public void 脱下盔甲_对应加成当场收回()
        {
            Wear("iron_chest", 0);
            // 选中槽挪到空格：脱下的胸甲会回到背包第一个空位（0 槽）——若 0 槽正是
            // 选中槽，手持源会按「拿着未穿的盔甲照样生效」语义接力 +2，那就不是
            // 「穿戴源收回」的干净观测了（手持语义由另一条测试单独守）
            _ctx.Inventory.SelectedHotbarIndex = 5;
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(2), "前置：穿着铁胸甲防御 2");

            Assert.That(_ctx.ArmorSlots.TryUnequipTo(_ctx.Inventory, 1), Is.True);
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(0), "脱下即失效");
        }

        [Test]
        public void 穿戴机元胸甲_血上限提高_脱下钳回()
        {
            Wear("machine_essence_chest", 0);
            // 同上：选中槽挪空，脱下的胸甲回背包后不经手持源接力
            _ctx.Inventory.SelectedHotbarIndex = 5;
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.EffectiveMaxHealth, Is.EqualTo(22f), "机元胸甲血上限 +2");

            _ctx.Health.Current = 25f;
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Health.Current, Is.EqualTo(22f), "超上限当场收回");

            _ctx.ArmorSlots.TryUnequipTo(_ctx.Inventory, 1);
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Health.Current, Is.EqualTo(20f), "脱下后 22 血钳回 20");
        }

        [Test]
        public void 金系盔甲无gearBonus_穿戴不加任何属性()
        {
            // 金系加成是攻击（走手持 attackDamage），盔甲无攻击属性 → 纯装饰/收藏件
            Wear("gold_helmet", 0);
            Wear("gold_chest", 2);
            Wear("gold_legs", 3);
            Wear("gold_boots", 4);
            _ctx.RefreshGearBonuses();

            Assert.That(_ctx.Defense, Is.EqualTo(0), "金甲不带防御");
            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(0));
        }

        [Test]
        public void 空穿戴_三属性与m10空手一致()
        {
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.Defense, Is.EqualTo(0));
            Assert.That(_ctx.MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(_ctx.MaxHealthBonus, Is.EqualTo(0));
        }

        // ─── 真实物品表守卫：16 件盔甲部位/材料属性齐全 ──────────────────

        [Test]
        public void 真实物品表_十六件盔甲全部注册_部位正确()
        {
            string[] helmets = { "iron_helmet", "gold_helmet", "summer_alloy_helmet", "machine_essence_helmet" };
            string[] chests = { "iron_chest", "gold_chest", "summer_alloy_chest", "machine_essence_chest" };
            string[] legs = { "iron_legs", "gold_legs", "summer_alloy_legs", "machine_essence_legs" };
            string[] boots = { "iron_boots", "gold_boots", "summer_alloy_boots", "machine_essence_boots" };

            foreach (string id in helmets)
                Assert.That(_db.GetById(id).ArmorPart, Is.EqualTo(ArmorPart.Helmet), $"{id} 应是头盔");
            foreach (string id in chests)
                Assert.That(_db.GetById(id).ArmorPart, Is.EqualTo(ArmorPart.Chest), $"{id} 应是胸甲");
            foreach (string id in legs)
                Assert.That(_db.GetById(id).ArmorPart, Is.EqualTo(ArmorPart.Legs), $"{id} 应是护腿");
            foreach (string id in boots)
                Assert.That(_db.GetById(id).ArmorPart, Is.EqualTo(ArmorPart.Boots), $"{id} 应是靴子");

            // 盔甲不可堆叠（每槽一件），maxStack 必须 1
            foreach (string id in helmets.Concat(chests).Concat(legs).Concat(boots))
                Assert.That(_db.GetById(id).MaxStack, Is.EqualTo(1), $"{id} 盔甲 maxStack 必须 1");
        }

        [Test]
        public void 真实物品表_材料属性身份_铁防合金速机元血()
        {
            // 铁系四件：defense（头1/胸2/腿2/脚1，全套 6）
            AssertGear("iron_helmet", GearStat.Defense, 1f);
            AssertGear("iron_chest", GearStat.Defense, 2f);
            AssertGear("iron_legs", GearStat.Defense, 2f);
            AssertGear("iron_boots", GearStat.Defense, 1f);

            // 夏季合金四件：moveSpeed（头0.02/胸0.03/腿0.03/脚0.02，全套 +10%）
            AssertGear("summer_alloy_helmet", GearStat.MoveSpeed, 0.02f);
            AssertGear("summer_alloy_chest", GearStat.MoveSpeed, 0.03f);
            AssertGear("summer_alloy_legs", GearStat.MoveSpeed, 0.03f);
            AssertGear("summer_alloy_boots", GearStat.MoveSpeed, 0.02f);

            // 机元四件：maxHealth（头1/胸2/腿2/脚1，全套 +6）
            AssertGear("machine_essence_helmet", GearStat.MaxHealth, 1f);
            AssertGear("machine_essence_chest", GearStat.MaxHealth, 2f);
            AssertGear("machine_essence_legs", GearStat.MaxHealth, 2f);
            AssertGear("machine_essence_boots", GearStat.MaxHealth, 1f);

            // 金系四件：无 gearBonus（金系加成是攻击，走手持 attackDamage——材料身份铁律）
            foreach (string id in new[] { "gold_helmet", "gold_chest", "gold_legs", "gold_boots" })
            {
                Assert.That(_db.GetById(id).GearStat, Is.EqualTo(GearStat.None),
                    $"{id} 金系攻击走手持 attackDamage，盔甲不写 gearBonus");
            }
        }

        private void AssertGear(string id, GearStat stat, float amount)
        {
            var def = _db.GetById(id);
            Assert.That(def.GearStat, Is.EqualTo(stat), $"{id} 材料属性身份");
            Assert.That(def.GearAmount, Is.EqualTo(amount).Within(1e-6f), $"{id} 加成量");
        }
    }
}
#endif
