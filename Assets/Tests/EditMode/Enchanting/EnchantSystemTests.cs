using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Enchanting
{
    /// <summary>
    /// m11 W2-2：附魔系统（<see cref="MyWorld.Core.Enchanting.EnchantSystem"/>）纯函数与流程测试。
    /// <para>
    /// 三种附魔（锋利/效率/耐久）效果数值、经验消耗 5/级、等级封顶 3、附魔书的
    /// Metadata 编码与装备附魔的「独立字典存储」——耐久编码不撞是 a1fb425 教训的守卫：
    /// <see cref="ItemStack"/> 的 16 位 Metadata 全部归耐久（bits 0-7 当前 / bits 8-15 上限），
    /// 装备附魔<b>绝不写 Metadata</b>，只进 <see cref="MyWorld.Core.Enchanting.EnchantStore"/>；
    /// 附魔书自己的 Metadata 只用低 8 位（bits 8-15 恒 0 → HasDurability 恒 false）。
    /// </para>
    /// <para>
    /// 纯 Core 无 Unity 依赖，dotnet 与 EditMode 双链同跑（BlockGatingTests 同模式）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class EnchantSystemTests
    {
        private const int IronPickaxeId = 1402;
        private const int DirtId = 1002;
        private const int BookId = 1601;
        private const int EnchantedBookId = 1602;

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""dirt"", ""numericId"": 1002 }",
                @"{ ""id"": ""iron_pickaxe"", ""numericId"": 1402, ""maxStack"": 1,
                    ""isTool"": true, ""miningLevel"": 3, ""toolTier"": 3, ""maxDurability"": 250 }",
                @"{ ""id"": ""iron_sword"", ""numericId"": 1410, ""maxStack"": 1,
                    ""isTool"": true, ""attackDamage"": 5 }",
                @"{ ""id"": ""book"", ""numericId"": 1601 }",
                @"{ ""id"": ""enchanted_book"", ""numericId"": 1602, ""maxStack"": 1 }",
                @"{ ""id"": ""lapis"", ""numericId"": 1603 }",
            });
        }

        // ─── 经验消耗：5/级、封顶 3 ────────────────────────────────────────

        [TestCase(1, 5, "1 级附魔消耗 5 经验")]
        [TestCase(2, 10, "2 级附魔消耗 10 经验")]
        [TestCase(3, 15, "3 级附魔消耗 15 经验（封顶）")]
        public void ExpCostFor_LevelTimes5(int level, int expected, string reason)
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.ExpCostFor(level), Is.EqualTo(expected),
                $"{level} 级附魔的经验消耗应为 {expected}（{reason}）");
        }

        [TestCase(0, false)]
        [TestCase(-1, false)]
        [TestCase(4, false, "封顶 3：4 级不合法")]
        [TestCase(99, false)]
        [TestCase(1, true)]
        [TestCase(3, true)]
        public void IsValidLevel_CapAtThree(int level, bool expected, string reason = "")
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.IsValidLevel(level), Is.EqualTo(expected),
                $"等级 {level} 的合法性应为 {expected}（附魔封顶 3 级）");
        }

        // ─── Enchant：扣经验、写 store、不动装备 Metadata ──────────────────

        [Test]
        public void Enchant_SharpnessLevel1_SpendsFiveExp_WritesStore_KeepsMetadataUntouched()
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(IronPickaxeId, 1));
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            var xp = new Experience(current: 30, level: 0);
            ushort metadataBefore = inventory.GetSlot(0).Metadata;

            var result = MyWorld.Core.Enchanting.EnchantSystem.Enchant(
                store, inventory, ref xp, items, 0, EnchantmentType.Sharpness, 1);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.EnchantResult.Ok), "30 经验够付 5，应成功");
            Assert.That(xp.Current, Is.EqualTo(25), "经验 30 - 5 = 25");
            Assert.That(store.TryGet(0, IronPickaxeId, out var kind, out int level), Is.True,
                "附魔后应能按（槽位, 物品）查到附魔");
            Assert.That(kind, Is.EqualTo(EnchantmentType.Sharpness));
            Assert.That(level, Is.EqualTo(1));
            // a1fb425 教训的守卫：附魔绝不当耐久编码的第三条初始化路径
            Assert.That(inventory.GetSlot(0).Metadata, Is.EqualTo(metadataBefore),
                "附魔不得改写装备 Metadata——16 位全部归耐久（锋利存独立字典）");
            Assert.That(inventory.GetSlot(0).ItemId, Is.EqualTo(IronPickaxeId), "装备本体不动");
        }

        [Test]
        public void Enchant_AlreadyEncodedDurabilityTool_MetadataByteUnchanged()
        {
            // 已磨损的镐（Metadata 已落耐久编码）附魔后编码一位不变——
            // 「附魔不得挤占/重写耐久位」的另一半：不只对新工具，对存量编码同样只读
            var items = BuildItems();
            var inventory = new PlayerInventory();
            var worn = new ItemStack(IronPickaxeId, 1).WithMaxDurability(250).WithDurabilityUsed(250);
            inventory.SetSlot(5, worn);
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            var xp = new Experience(current: 100, level: 2);

            var result = MyWorld.Core.Enchanting.EnchantSystem.Enchant(
                store, inventory, ref xp, items, 5, EnchantmentType.Unbreaking, 3);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.EnchantResult.Ok));
            Assert.That(inventory.GetSlot(5).Metadata, Is.EqualTo(worn.Metadata),
                "已落耐久编码的镐附魔后 Metadata 必须一位不变");
            Assert.That(store.TryGet(5, IronPickaxeId, out _, out int level) && level == 3, Is.True,
                "耐久 III 写进 store");
        }

        [Test]
        public void Enchant_NotEnoughExp_Rejected_NoSideEffects()
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(IronPickaxeId, 1));
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            var xp = new Experience(current: 4, level: 0); // 4 < 5

            var result = MyWorld.Core.Enchanting.EnchantSystem.Enchant(
                store, inventory, ref xp, items, 0, EnchantmentType.Sharpness, 1);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.EnchantResult.NotEnoughExp),
                "经验不够应整单失败");
            Assert.That(xp.Current, Is.EqualTo(4), "失败不扣经验");
            Assert.That(store.Count, Is.EqualTo(0), "失败不写 store");
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(-2)]
        public void Enchant_InvalidLevel_Rejected(int level)
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(IronPickaxeId, 1));
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            var xp = new Experience(current: 100, level: 5);

            var result = MyWorld.Core.Enchanting.EnchantSystem.Enchant(
                store, inventory, ref xp, items, 0, EnchantmentType.Sharpness, level);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.EnchantResult.InvalidLevel),
                $"等级 {level} 越界（封顶 3）应拒绝");
            Assert.That(store.Count, Is.EqualTo(0), "拒绝时不得写入");
        }

        [TestCase(DirtId, "泥土不是装备")]
        [TestCase(EnchantedBookId, "附魔书本身不可再附魔")]
        public void Enchant_NonEnchantableTarget_Rejected(int itemId, string reason)
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(itemId, 1));
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            var xp = new Experience(current: 100, level: 0);

            var result = MyWorld.Core.Enchanting.EnchantSystem.Enchant(
                store, inventory, ref xp, items, 0, EnchantmentType.Sharpness, 1);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.EnchantResult.NotEnchantable),
                $"目标 {itemId}：{reason}");
        }

        [Test]
        public void Enchant_EmptySlot_Rejected()
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            var xp = new Experience(current: 100, level: 0);

            var result = MyWorld.Core.Enchanting.EnchantSystem.Enchant(
                store, inventory, ref xp, items, 0, EnchantmentType.Sharpness, 1);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.EnchantResult.SlotEmpty), "空槽不可附魔");
        }

        // ─── TrySpendExperience：跨级扣（一级换 99，m10 占位 UI 同语义） ────

        [Test]
        public void TrySpendExperience_CrossesLevelDown()
        {
            var xp = new Experience(current: 3, level: 1); // 总 103

            bool spent = MyWorld.Core.Enchanting.EnchantSystem.TrySpendExperience(ref xp, 10);

            Assert.That(spent, Is.True);
            Assert.That(xp.Level, Is.EqualTo(0), "扣穿当前级要降级");
            Assert.That(xp.Current, Is.EqualTo(93),
                "3 点不够 → 破级换 99（每破一级损耗 1 点，与 m10 占位 UI 同语义），再付剩余 6 → 93");
        }

        [Test]
        public void TrySpendExperience_NotEnough_ReturnsFalse_Untouched()
        {
            var xp = new Experience(current: 3, level: 0);

            bool spent = MyWorld.Core.Enchanting.EnchantSystem.TrySpendExperience(ref xp, 10);

            Assert.That(spent, Is.False, "总经验不够返回 false");
            Assert.That(xp.Current, Is.EqualTo(3), "失败时经验原样");
        }

        // ─── 效果三件套数值 ────────────────────────────────────────────────

        [TestCase(EnchantmentType.Sharpness, 1, 1f)]
        [TestCase(EnchantmentType.Sharpness, 2, 2f)]
        [TestCase(EnchantmentType.Sharpness, 3, 3f)]
        [TestCase(EnchantmentType.Efficiency, 3, 0f, "效率不加攻击")]
        [TestCase(EnchantmentType.Unbreaking, 2, 0f, "耐久不加攻击")]
        public void AttackBonus_SharpnessPlusOnePerLevel(EnchantmentType kind, int level, float expected,
            string reason = "锋利每级 +1 攻击")
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.AttackBonus(kind, level),
                Is.EqualTo(expected).Within(1e-5f), $"{kind} Lv{level}：{reason}");
        }

        [TestCase(EnchantmentType.Efficiency, 1, 0.83333f, "效率 I：时间 × 1/1.2")]
        [TestCase(EnchantmentType.Efficiency, 2, 0.69444f, "效率 II：时间 × 1/1.44")]
        [TestCase(EnchantmentType.Efficiency, 3, 0.57870f, "效率 III：时间 × 1/1.728（挖速 ×1.2/级）")]
        [TestCase(EnchantmentType.Sharpness, 3, 1f, "锋利不改挖速")]
        [TestCase(EnchantmentType.Unbreaking, 3, 1f, "耐久不改挖速")]
        public void DigTimeMultiplier_EfficiencyDividesBy12PerLevel(
            EnchantmentType kind, int level, float expected, string reason)
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.DigTimeMultiplier(kind, level),
                Is.EqualTo(expected).Within(1e-4f), $"{kind} Lv{level}：{reason}");
        }

        [TestCase(EnchantmentType.None, 999, true, "无附魔：每次照常磨损（调用方零改动路径）")]
        [TestCase(EnchantmentType.Sharpness, 3, true, "锋利不干预耐久")]
        [TestCase(EnchantmentType.Efficiency, 3, true, "效率不干预耐久")]
        public void ShouldWearDurability_NonUnbreaking_AlwaysTrue(
            EnchantmentType kind, int level, bool expected, string reason)
        {
            for (int salt = 0; salt < 50; salt++)
            {
                Assert.That(
                    MyWorld.Core.Enchanting.EnchantSystem.ShouldWearDurability(kind, level, salt),
                    Is.EqualTo(expected), $"{kind} Lv{level} salt={salt}：{reason}");
            }
        }

        /// <summary>
        /// 耐久附魔的磨损概率：1/(1+0.2L)——Lv1/2/3 即 5/6、5/7、5/8。
        /// 期望耐用 = max × (1 + 0.2L)（+20%/级），上限 255 不动、按比例减缓扣减
        /// 而非扩上限（a1fb425 的教训）。整数字符哈希掷骰（不持随机数对象）。
        /// </summary>
        [TestCase(1, 5f / 6f, "Lv1：5/6 概率磨损 → 期望耐用 ×1.2")]
        [TestCase(2, 5f / 7f, "Lv2：5/7 概率磨损 → 期望耐用 ×1.4")]
        [TestCase(3, 5f / 8f, "Lv3：5/8 概率磨损 → 期望耐用 ×1.6")]
        public void ShouldWearDurability_Unbreaking_WearRatioMatches(int level, float expected, string reason)
        {
            const int samples = 1200;
            int wore = 0;
            for (int salt = 0; salt < samples; salt++)
            {
                if (MyWorld.Core.Enchanting.EnchantSystem.ShouldWearDurability(
                        EnchantmentType.Unbreaking, level, salt))
                {
                    wore++;
                }
            }

            Assert.That((float)wore / samples, Is.EqualTo(expected).Within(0.06f),
                $"耐久 Lv{level} 的磨损频率应接近 {expected:0.###}（{reason}）");
        }

        [Test]
        public void ShouldWearDurability_SameSalt_Deterministic()
        {
            bool first = MyWorld.Core.Enchanting.EnchantSystem.ShouldWearDurability(
                EnchantmentType.Unbreaking, 2, 12345);
            bool second = MyWorld.Core.Enchanting.EnchantSystem.ShouldWearDurability(
                EnchantmentType.Unbreaking, 2, 12345);

            Assert.That(second, Is.EqualTo(first), "同 salt 同等级必须同结果（确定性哈希，不持随机状态）");
        }

        // ─── 附魔书编码：低 8 位（bits 0-2 类型 / bits 3-4 等级），不撞耐久位 ──

        [TestCase(EnchantmentType.Sharpness, 1)]
        [TestCase(EnchantmentType.Sharpness, 3)]
        [TestCase(EnchantmentType.Efficiency, 2)]
        [TestCase(EnchantmentType.Unbreaking, 3)]
        public void BookEncoding_Roundtrip_AndNeverTouchesDurabilityBits(
            EnchantmentType kind, int level)
        {
            ushort encoded = MyWorld.Core.Enchanting.EnchantSystem.EncodeBook(kind, level);

            Assert.That(encoded & ItemStack.MaxDurabilityMask, Is.EqualTo(0),
                $"{kind} Lv{level} 的书编码 bits 8-15 必须恒 0——那是耐久上限位，绝不占用");
            Assert.That(new ItemStack(EnchantedBookId, 1, encoded).HasDurability, Is.False,
                "带附魔编码的书不能被误判成「有耐久编码」");

            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.TryDecodeBook(encoded, out var decodedKind, out int decodedLevel),
                Is.True, "合法编码必须可解码");
            Assert.That(decodedKind, Is.EqualTo(kind));
            Assert.That(decodedLevel, Is.EqualTo(level));
        }

        [TestCase(EnchantmentType.None, 1, "None 不是可编码的附魔类型")]
        [TestCase(EnchantmentType.LuckOfTheSea, 1, "占位类型不进书编码")]
        [TestCase(EnchantmentType.Sharpness, 0, "等级 0 不合法")]
        [TestCase(EnchantmentType.Sharpness, 4, "等级封顶 3")]
        public void EncodeBook_InvalidInput_ReturnsZero(EnchantmentType kind, int level, string reason)
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.EncodeBook(kind, level), Is.EqualTo(0),
                $"{kind} Lv{level}：{reason}——编码失败返回 0（= 未鉴定书）");
        }

        [Test]
        public void TryDecodeBook_ZeroMetadata_ReturnsFalse()
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.TryDecodeBook(0, out _, out _), Is.False,
                "Metadata=0 是刚合成的「未鉴定书」，没有预置类型（融合时掷骰）");
        }

        // ─── RollBookKind：确定性 1/3 选类型 ──────────────────────────────

        [Test]
        public void RollBookKind_OnlyThreeKinds_AllReachable()
        {
            var seen = new HashSet<EnchantmentType>();
            for (int salt = 0; salt < 300; salt++)
            {
                EnchantmentType kind = MyWorld.Core.Enchanting.EnchantSystem.RollBookKind(salt);
                Assert.That(kind == EnchantmentType.Sharpness
                            || kind == EnchantmentType.Efficiency
                            || kind == EnchantmentType.Unbreaking,
                    Is.True, $"salt={salt} 掷出 {kind}，只允许三种附魔类型之一");
                seen.Add(kind);
            }

            Assert.That(seen, Is.EquivalentTo(new[]
            {
                EnchantmentType.Sharpness, EnchantmentType.Efficiency, EnchantmentType.Unbreaking,
            }), "300 个 salt 里三种类型都必须出现（均匀性弱断言：谁都可达）");
        }

        [Test]
        public void RollBookKind_SameSalt_Deterministic()
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantSystem.RollBookKind(777),
                Is.EqualTo(MyWorld.Core.Enchanting.EnchantSystem.RollBookKind(777)),
                "同 salt 同类型（确定性哈希）");
        }

        // ─── Fuse：手持附魔书右键 → 书消失、装备带魔 ───────────────────────

        [Test]
        public void Fuse_PlainBook_IronPickaxeInBag_BookConsumed_GearEnchanted()
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(EnchantedBookId, 1));  // 手持（未鉴定书）
            inventory.SetSlot(5, new ItemStack(IronPickaxeId, 1));    // 背包里的铁镐
            ushort pickaxeMetadataBefore = inventory.GetSlot(5).Metadata;
            var store = new MyWorld.Core.Enchanting.EnchantStore();

            var result = MyWorld.Core.Enchanting.EnchantSystem.Fuse(
                store, inventory, items, bookSlot: 0, salt: 42,
                out int targetSlot, out var kind, out int level);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.FuseResult.Ok), "书 + 背包装备应融合成功");
            Assert.That(targetSlot, Is.EqualTo(5), "目标应是背包里第一件可附魔装备（槽位序最小）");
            Assert.That(inventory.GetSlot(0).IsEmpty, Is.True, "书消失（maxStack=1，整格清空）");
            Assert.That(inventory.GetSlot(5).ItemId, Is.EqualTo(IronPickaxeId), "装备本体还在");
            Assert.That(store.TryGet(5, IronPickaxeId, out var gotKind, out int gotLevel), Is.True,
                "装备带魔：store 按（槽位, 物品）可查");
            Assert.That(gotKind, Is.EqualTo(kind), "掷出的类型与写入 store 的一致");
            Assert.That(gotLevel, Is.EqualTo(level), "等级一致");
            Assert.That(gotLevel, Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(3),
                "未鉴定书融合出的等级在 1..3");
            Assert.That(inventory.GetSlot(5).Metadata, Is.EqualTo(pickaxeMetadataBefore),
                "融合同样不改装备 Metadata（附魔只进 store）");
        }

        [Test]
        public void Fuse_EncodedBook_UsesDecodedKindAndLevel_NoRoll()
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            ushort encoded = MyWorld.Core.Enchanting.EnchantSystem.EncodeBook(EnchantmentType.Efficiency, 3);
            inventory.SetSlot(0, new ItemStack(EnchantedBookId, 1, encoded));
            inventory.SetSlot(2, new ItemStack(1410, 1)); // iron_sword
            var store = new MyWorld.Core.Enchanting.EnchantStore();

            var result = MyWorld.Core.Enchanting.EnchantSystem.Fuse(
                store, inventory, items, bookSlot: 0, salt: 999999,
                out int targetSlot, out var kind, out int level);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.FuseResult.Ok));
            Assert.That(kind, Is.EqualTo(EnchantmentType.Efficiency), "带编码的书直接用编码类型（不掷骰）");
            Assert.That(level, Is.EqualTo(3), "等级照编码的 III");
            Assert.That(targetSlot, Is.EqualTo(2));
        }

        [Test]
        public void Fuse_NoEnchantableGear_BookUntouched()
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(EnchantedBookId, 1));
            inventory.SetSlot(3, new ItemStack(DirtId, 32)); // 背包只有泥土
            var store = new MyWorld.Core.Enchanting.EnchantStore();

            var result = MyWorld.Core.Enchanting.EnchantSystem.Fuse(
                store, inventory, items, bookSlot: 0, salt: 1,
                out _, out _, out _);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.FuseResult.NoEnchantableGear),
                "没有可附魔装备应失败");
            Assert.That(inventory.GetSlot(0).IsEmpty, Is.False, "失败不消耗书");
            Assert.That(store.Count, Is.EqualTo(0), "失败不写 store");
        }

        [Test]
        public void Fuse_NotHoldingBook_Fails()
        {
            var items = BuildItems();
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(BookId, 3)); // 普通书（不是附魔书）
            inventory.SetSlot(5, new ItemStack(IronPickaxeId, 1));
            var store = new MyWorld.Core.Enchanting.EnchantStore();

            var result = MyWorld.Core.Enchanting.EnchantSystem.Fuse(
                store, inventory, items, bookSlot: 0, salt: 1,
                out _, out _, out _);

            Assert.That(result, Is.EqualTo(MyWorld.Core.Enchanting.FuseResult.NotHoldingBook),
                "手持的不是 enchanted_book 不能融合");
        }

        // ─── EnchantStore：换物品失效 + 存档字典往返 ──────────────────────

        [Test]
        public void EnchantStore_ItemSwappedOut_EnchantNoLongerApplies()
        {
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            store.Set(5, IronPickaxeId, EnchantmentType.Sharpness, 2);

            Assert.That(store.TryGet(5, IronPickaxeId, out _, out _), Is.True, "前置：原物品可查");
            Assert.That(store.TryGet(5, DirtId, out _, out _), Is.False,
                "同一槽换了别的物品，附魔不应作用到新物品上（按 itemId 守卫，防串魔）");
        }

        [Test]
        public void EnchantStore_SaveDictionaryRoundtrip()
        {
            var store = new MyWorld.Core.Enchanting.EnchantStore();
            store.Set(0, IronPickaxeId, EnchantmentType.Sharpness, 3);
            store.Set(5, 1410, EnchantmentType.Efficiency, 1);
            store.Set(12, IronPickaxeId, EnchantmentType.Unbreaking, 2);

            Dictionary<string, string> saved = store.ToSaveDictionary();
            var restored = MyWorld.Core.Enchanting.EnchantStore.FromSaveDictionary(saved);

            Assert.That(restored.TryGet(0, IronPickaxeId, out var k0, out int l0), Is.True);
            Assert.That(k0, Is.EqualTo(EnchantmentType.Sharpness));
            Assert.That(l0, Is.EqualTo(3));
            Assert.That(restored.TryGet(5, 1410, out var k5, out int l5), Is.True);
            Assert.That(k5, Is.EqualTo(EnchantmentType.Efficiency));
            Assert.That(l5, Is.EqualTo(1));
            Assert.That(restored.TryGet(12, IronPickaxeId, out var k12, out int l12), Is.True);
            Assert.That(k12, Is.EqualTo(EnchantmentType.Unbreaking));
            Assert.That(l12, Is.EqualTo(2));
        }

        [Test]
        public void EnchantStore_FromSaveDictionary_ToleratesGarbageRows()
        {
            var saved = new Dictionary<string, string>
            {
                { "3", "sharpness:2:" + IronPickaxeId }, // 好行
                { "not-a-slot", "sharpness:1:1402" },      // 槽位不是数字 → 跳过
                { "7", "freedom:9:1402" },                 // 未知类型 → 跳过
                { "9", "no-colons-here" },                 // 格式坏 → 跳过
                { "11", "efficiency:not-a-level:1402" },   // 等级不是数字 → 跳过
            };

            var restored = MyWorld.Core.Enchanting.EnchantStore.FromSaveDictionary(saved);

            Assert.That(restored.Count, Is.EqualTo(1), "坏行全部跳过，好行保留（读容忍，坏档不炸）");
            Assert.That(restored.TryGet(3, IronPickaxeId, out var kind, out int level), Is.True);
            Assert.That(kind, Is.EqualTo(EnchantmentType.Sharpness));
            Assert.That(level, Is.EqualTo(2));
        }

        [Test]
        public void EnchantStore_FromSaveDictionary_NullOrEmpty_ReturnsEmptyStore()
        {
            Assert.That(MyWorld.Core.Enchanting.EnchantStore.FromSaveDictionary(null).Count, Is.EqualTo(0),
                "null（旧档没这字段）= 空附魔，全新开始");
            Assert.That(MyWorld.Core.Enchanting.EnchantStore.FromSaveDictionary(
                new Dictionary<string, string>()).Count, Is.EqualTo(0));
        }
    }
}
