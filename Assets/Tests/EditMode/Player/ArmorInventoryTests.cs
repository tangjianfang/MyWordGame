using System;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// m11 W2-1：穿戴栏（<see cref="ArmorInventory"/>）——4 槽只收对应部位盔甲、
    /// 穿脱交换语义、armorPart 字段解析。纯 Core，双链（dotnet / EditMode）都跑。
    /// </summary>
    [TestFixture]
    public class ArmorInventoryTests
    {
        private const string HelmetJson = @"{
            ""id"": ""iron_helmet"", ""displayName"": ""铁头盔"", ""numericId"": 1450,
            ""maxStack"": 1, ""texture"": ""helmet-iron"",
            ""armorPart"": ""helmet"", ""gearBonus"": { ""stat"": ""defense"", ""amount"": 1 } }";

        private const string ChestJson = @"{
            ""id"": ""iron_chest"", ""displayName"": ""铁胸甲"", ""numericId"": 1451,
            ""maxStack"": 1, ""texture"": ""chest-iron"",
            ""armorPart"": ""chest"", ""gearBonus"": { ""stat"": ""defense"", ""amount"": 2 } }";

        private const string BootsJson = @"{
            ""id"": ""alloy_boots"", ""displayName"": ""合金靴子"", ""numericId"": 1461,
            ""maxStack"": 1, ""texture"": ""boots-summer-alloy"",
            ""armorPart"": ""boots"", ""gearBonus"": { ""stat"": ""moveSpeed"", ""amount"": 0.02 } }";

        private const string GoldHelmetJson = @"{
            ""id"": ""gold_helmet"", ""displayName"": ""金头盔"", ""numericId"": 1454,
            ""maxStack"": 1, ""texture"": ""helmet-gold"", ""armorPart"": ""helmet"" }";

        private const string NotArmorJson = @"{
            ""id"": ""plank"", ""displayName"": ""木板"", ""numericId"": 1001,
            ""maxStack"": 64, ""texture"": ""plank"" }";

        private ItemDatabase _db;
        private PlayerInventory _inventory;
        private ArmorInventory _armor;

        [SetUp]
        public void SetUp()
        {
            _db = ItemDatabase.FromJson(new[] { HelmetJson, ChestJson, BootsJson, GoldHelmetJson, NotArmorJson });
            _inventory = new PlayerInventory();
            _armor = new ArmorInventory();
        }

        // ─── 部位 ↔ 槽位映射 ─────────────────────────────────────────────

        [Test]
        public void 部位映射_头零胸一腿二脚三_None无槽()
        {
            Assert.That(ArmorInventory.SlotIndexOf(ArmorPart.Helmet), Is.EqualTo(0), "头盔进 0 号槽");
            Assert.That(ArmorInventory.SlotIndexOf(ArmorPart.Chest), Is.EqualTo(1), "胸甲进 1 号槽");
            Assert.That(ArmorInventory.SlotIndexOf(ArmorPart.Legs), Is.EqualTo(2), "护腿进 2 号槽");
            Assert.That(ArmorInventory.SlotIndexOf(ArmorPart.Boots), Is.EqualTo(3), "靴子进 3 号槽");
            Assert.That(ArmorInventory.SlotIndexOf(ArmorPart.None), Is.EqualTo(-1), "非盔甲无槽可放");

            Assert.That(ArmorInventory.PartOf(0), Is.EqualTo(ArmorPart.Helmet), "映射必须可逆");
            Assert.That(ArmorInventory.PartOf(3), Is.EqualTo(ArmorPart.Boots));
            Assert.That(ArmorInventory.PartOf(-1), Is.EqualTo(ArmorPart.None), "越界读容忍返回 None");
            Assert.That(ArmorInventory.PartOf(4), Is.EqualTo(ArmorPart.None));
        }

        [Test]
        public void 槽数恒为四_初始全空()
        {
            Assert.That(ArmorInventory.SlotCount, Is.EqualTo(4), "头/胸/腿/脚 4 槽");
            for (int i = 0; i < ArmorInventory.SlotCount; i++)
            {
                Assert.That(_armor.GetSlot(i), Is.EqualTo(ItemStack.Empty), $"初始 {i} 号槽应为空");
            }
            Assert.That(_armor.GetSlot(99), Is.EqualTo(ItemStack.Empty), "越界读容忍，返回空不抛");
        }

        // ─── armorPart 字段解析 ──────────────────────────────────────────

        [Test]
        public void 解析_盔甲带部位_普通物品None()
        {
            Assert.That(_db.GetById("iron_helmet").ArmorPart, Is.EqualTo(ArmorPart.Helmet));
            Assert.That(_db.GetById("iron_chest").ArmorPart, Is.EqualTo(ArmorPart.Chest));
            Assert.That(_db.GetById("alloy_boots").ArmorPart, Is.EqualTo(ArmorPart.Boots));
            Assert.That(_db.GetById("plank").ArmorPart, Is.EqualTo(ArmorPart.None), "不写 armorPart = 非盔甲");
        }

        [Test]
        public void 解析_部位词表写错_加载即抛()
        {
            string bad = @"{ ""id"": ""x"", ""armorPart"": ""hat"" }";
            Assert.Throws<System.IO.InvalidDataException>(
                () => ItemDatabase.FromJson(new[] { bad }),
                "armorPart 是受控词表，与 toolTier / gearBonus.stat 同态度——写错立刻报");
        }

        [Test]
        public void 解析_四部位词表全覆盖()
        {
            var db = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""a"", ""armorPart"": ""helmet"" }",
                @"{ ""id"": ""b"", ""armorPart"": ""chest"" }",
                @"{ ""id"": ""c"", ""armorPart"": ""legs"" }",
                @"{ ""id"": ""d"", ""armorPart"": ""boots"" }",
            });
            Assert.That(db.GetById("a").ArmorPart, Is.EqualTo(ArmorPart.Helmet));
            Assert.That(db.GetById("b").ArmorPart, Is.EqualTo(ArmorPart.Chest));
            Assert.That(db.GetById("c").ArmorPart, Is.EqualTo(ArmorPart.Legs));
            Assert.That(db.GetById("d").ArmorPart, Is.EqualTo(ArmorPart.Boots));
        }

        // ─── 穿入：只收对应部位 ──────────────────────────────────────────

        [Test]
        public void 穿入_头盔落头部槽_不落别处()
        {
            _inventory.SetSlot(5, new ItemStack(_db.GetById("iron_helmet").NumericId, 1));

            bool ok = _armor.TryEquipFrom(_inventory, 5, _db);

            Assert.That(ok, Is.True, "盔甲穿入应成功");
            Assert.That(_armor.GetSlot(0).ItemId, Is.EqualTo(_db.GetById("iron_helmet").NumericId),
                "头盔按部位落 0 号槽");
            Assert.That(_inventory.GetSlot(5), Is.EqualTo(ItemStack.Empty), "背包源格已清空");
            for (int i = 1; i < ArmorInventory.SlotCount; i++)
            {
                Assert.That(_armor.GetSlot(i), Is.EqualTo(ItemStack.Empty), $"头盔不能落进 {i} 号槽");
            }
        }

        [Test]
        public void 穿入_非盔甲拒绝_背包不动()
        {
            _inventory.SetSlot(3, new ItemStack(_db.GetById("plank").NumericId, 12));

            bool ok = _armor.TryEquipFrom(_inventory, 3, _db);

            Assert.That(ok, Is.False, "木板没有部位，不是盔甲");
            Assert.That(_inventory.GetSlot(3).Count, Is.EqualTo(12), "背包原样不动");
            for (int i = 0; i < ArmorInventory.SlotCount; i++)
            {
                Assert.That(_armor.GetSlot(i), Is.EqualTo(ItemStack.Empty), "穿戴栏不能进非盔甲");
            }
        }

        [Test]
        public void 穿入_空格与越界格拒绝()
        {
            Assert.That(_armor.TryEquipFrom(_inventory, 4, _db), Is.False, "空背包格没东西可穿");
            Assert.That(_armor.TryEquipFrom(_inventory, -1, _db), Is.False, "越界格拒绝");
            Assert.That(_armor.TryEquipFrom(_inventory, 999, _db), Is.False);
            Assert.That(_armor.TryEquipFrom(null, 0, _db), Is.False, "null 容忍不抛");
            Assert.That(_armor.TryEquipFrom(_inventory, 0, null), Is.False, "无物品表无从判定部位");
        }

        [Test]
        public void 穿入_槽已占用_旧盔甲换回背包源格()
        {
            _inventory.SetSlot(2, new ItemStack(_db.GetById("iron_helmet").NumericId, 1, 0xABCD));
            _armor.TryEquipFrom(_inventory, 2, _db);
            int goldId = _db.GetById("gold_helmet").NumericId;
            _inventory.SetSlot(7, new ItemStack(goldId, 1));

            bool ok = _armor.TryEquipFrom(_inventory, 7, _db);

            Assert.That(ok, Is.True, "同部位换装应成功");
            Assert.That(_armor.GetSlot(0).ItemId, Is.EqualTo(goldId), "新头盔进头部槽");
            Assert.That(_inventory.GetSlot(7).ItemId, Is.EqualTo(_db.GetById("iron_helmet").NumericId),
                "旧头盔换回背包源格，不凭空消失");
            Assert.That(_inventory.GetSlot(7).Metadata, Is.EqualTo((ushort)0xABCD), "交换保留 Metadata");
        }

        // ─── 脱下：回背包，满则拒绝 ──────────────────────────────────────

        [Test]
        public void 脱下_回背包_槽清空()
        {
            _inventory.SetSlot(0, new ItemStack(_db.GetById("iron_chest").NumericId, 1));
            _armor.TryEquipFrom(_inventory, 0, _db);

            bool ok = _armor.TryUnequipTo(_inventory, 1);

            Assert.That(ok, Is.True, "脱下应成功");
            Assert.That(_armor.GetSlot(1), Is.EqualTo(ItemStack.Empty), "穿戴槽清空");
            Assert.That(_inventory.CountOf(_db.GetById("iron_chest").NumericId), Is.EqualTo(1),
                "盔甲回到背包");
        }

        [Test]
        public void 脱下_背包满_保持穿戴()
        {
            _inventory.SetSlot(0, new ItemStack(_db.GetById("iron_helmet").NumericId, 1));
            _armor.TryEquipFrom(_inventory, 0, _db);
            // 填满 36 格（每格 12 个木板），背包零空间
            int plankId = _db.GetById("plank").NumericId;
            for (int i = 0; i < 36; i++) _inventory.SetSlot(i, new ItemStack(plankId, 64));

            bool ok = _armor.TryUnequipTo(_inventory, 0);

            Assert.That(ok, Is.False, "背包装不下就不能脱");
            Assert.That(_armor.GetSlot(0).IsEmpty, Is.False, "盔甲保持穿戴，不悬空丢失");
        }

        [Test]
        public void 脱下_空槽与越界拒绝()
        {
            Assert.That(_armor.TryUnequipTo(_inventory, 0), Is.False, "空槽没东西可脱");
            Assert.That(_armor.TryUnequipTo(_inventory, -5), Is.False, "越界拒绝");
            Assert.That(_armor.TryUnequipTo(_inventory, 99), Is.False);
            Assert.That(_armor.TryUnequipTo(null, 0), Is.False, "null 容忍不抛");
        }
    }
}
