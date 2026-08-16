#if UNITY_EDITOR
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// Task C1：验证默认 hotbar slot 0 预填一个基础物品。
    /// 实际预填逻辑在 <see cref="MyWorld.Unity.Bootstrap.WorldBootstrap"/> 的 Awake 里，
    /// 这里用最小可行路径直接测 <see cref="PlayerInventory.SetSlot"/> 的契约，避免
    /// 在 EditMode 下把整个世界/玩家管线拉起来（那条链要 World / ItemDatabase / BlockRegistry
    /// 等，单测不划算）。
    /// </summary>
    [TestFixture]
    public class HotbarUITests
    {
        [Test]
        public void SetSlot_Zero_WithItemStack_RoundTrips()
        {
            var inv = new PlayerInventory();
            var stack = new ItemStack(itemId: 1001, count: 64);

            inv.SetSlot(0, stack);

            var read = inv.GetSlot(0);
            Assert.That(read.IsEmpty, Is.False, "set 之后 slot 0 不应为空");
            Assert.That(read.ItemId, Is.EqualTo(1001), "ItemId 必须原样回读");
            Assert.That(read.Count, Is.EqualTo(64), "数量必须是 64");
        }

        [Test]
        public void SetSlot_Empty_DoesNotThrow_AndRoundTripsEmpty()
        {
            var inv = new PlayerInventory();
            inv.SetSlot(0, new ItemStack(1001, 64));
            inv.SetSlot(0, ItemStack.Empty);

            var read = inv.GetSlot(0);
            Assert.That(read.IsEmpty, Is.True, "再 set 空之后 slot 0 应恢复为空");
        }

        [Test]
        public void ItemDatabase_Plank_IsRegistered_WithNumericId1001()
        {
            // 预填逻辑依赖 plank 物品已注册，这里做一道契约保险：
            // 万一以后有人重命名 plank，这里立即 fail，提醒同步 WorldBootstrap 的预填逻辑。
            var items = ItemDatabaseLoader.Load();
            Assert.That(items.TryGetById("plank", out var def), Is.True,
                "plank 物品必须在物品库里（WorldBootstrap 依赖它做默认热键栏预填）");
            Assert.That(def.NumericId, Is.GreaterThan(0),
                "plank 必须有有效 numericId（auto-assign 应从 1000 起）");
        }

        // ─── m10 B1：选中格耐久条的纯计算（比例 + 绿→红配色） ────────────────
        // OnGUI 本身 EditMode 不跑，可测的部分全部收敛进两个纯静态。

        [Test]
        public void ComputeDurabilityRatio_FreshTool_MetadataZero_IsFull()
        {
            var def = new ItemDefinition { Id = "wooden_pickaxe", MaxDurability = 59 };
            var stack = new ItemStack(1400, 1); // Metadata=0：旧存档 / 刚合成

            Assert.That(HotbarUI.ComputeDurabilityRatio(stack, def), Is.EqualTo(1f),
                "Metadata=0 视为满耐久——旧存档/预填/刚合成的镐不炸、也不显示空条");
        }

        [Test]
        public void ComputeDurabilityRatio_Worn_IsCurrentOverMax()
        {
            var def = new ItemDefinition { Id = "wooden_pickaxe", MaxDurability = 59 };
            var stack = new ItemStack(1400, 1).WithMaxDurability(59);
            for (int i = 0; i < 30; i++)
            {
                stack = stack.DamageOnce(); // 剩 29
            }

            Assert.That(HotbarUI.ComputeDurabilityRatio(stack, def),
                Is.EqualTo(29f / 59f).Within(1e-4f),
                "条宽比例 = 剩余耐久 / 上限");
        }

        [Test]
        public void ComputeDurabilityRatio_NoDurabilityConcept_ReturnsNull()
        {
            var nonTool = new ItemDefinition { Id = "cobblestone", MaxDurability = 0 };
            Assert.That(HotbarUI.ComputeDurabilityRatio(new ItemStack(1003, 64), nonTool), Is.Null,
                "未声明耐久的物品不画条");
            Assert.That(HotbarUI.ComputeDurabilityRatio(ItemStack.Empty, nonTool), Is.Null,
                "空槽不画条");
            Assert.That(HotbarUI.ComputeDurabilityRatio(new ItemStack(1400, 1), null), Is.Null,
                "查不到物品定义（防御路径）也不画条");
        }

        [Test]
        public void DurabilityBarColor_FullGreen_EmptyRed_MidBetween()
        {
            Color full = HotbarUI.DurabilityBarColor(1f);
            Color empty = HotbarUI.DurabilityBarColor(0f);
            Color mid = HotbarUI.DurabilityBarColor(0.5f);

            Assert.That(full.g, Is.GreaterThan(full.r), "满耐久应偏绿");
            Assert.That(empty.r, Is.GreaterThan(empty.g), "耐久尽应偏红");
            Assert.That(mid.r, Is.GreaterThan(full.r), "半耐久比满耐久更红");
            Assert.That(mid.r, Is.LessThan(empty.r), "半耐久比耐久尽更不红（线性过渡）");
        }
    }
}
#endif
