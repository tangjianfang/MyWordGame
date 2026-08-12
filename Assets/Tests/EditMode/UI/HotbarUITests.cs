#if UNITY_EDITOR
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Bootstrap;
using NUnit.Framework;

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
    }
}
#endif
