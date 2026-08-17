#if UNITY_EDITOR
// m10 终审修 I1：附魔占位（X 键 EnchantingUi）不得改写工具耐久上限——
// 耐久 max 唯一来源是 items/*.json 的 maxDurability，附魔路径不碰 Metadata。
// 依赖 MyWorld.Unity 的 EnchantingUi / PlayerContext 与真实 items/*.json，
// dotnet 链跑不动，整文件 #if UNITY_EDITOR 包裹（与 GearBonusEquipTests 同款）。
using System.IO;
using System.Linq;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    [TestFixture]
    public class EnchantingUiTests
    {
        private GameObject _go;
        private PlayerContext _ctx;
        private ItemDatabase _db;

        [SetUp]
        public void SetUp()
        {
            // 真实物品表：铁镐 maxDurability=250 的断言要用真 JSON，
            // 不在本测试里手拼（手拼就测不到数据文件本身）
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            _db = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));

            _go = new GameObject("附魔");
            _ctx = _go.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，显式初始化（与 GearBonusEquipTests 同款）
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _ctx.Items = _db;
            _ctx.Experience = new Experience(30, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        /// <summary>把刚合成的铁镐（Metadata=0，尚未落耐久编码）放进 0 号热键格，
        /// 青金石放进 1 号格付消耗。</summary>
        private void PrepareIronPickaxe()
        {
            var pick = _db.GetById("iron_pickaxe");
            _ctx.Inventory.SetSlot(0, new ItemStack(pick.NumericId, 1));
            _ctx.Inventory.SelectedHotbarIndex = 0;
            var lapis = _db.GetById("lapis");
            _ctx.Inventory.SetSlot(1, new ItemStack(lapis.NumericId, 64));
        }

        [Test]
        public void 附魔新铁镐_不写耐久Metadata_首耗仍按表内250落编码()
        {
            PrepareIronPickaxe();
            var pick = _db.GetById("iron_pickaxe");
            Assert.That(pick.MaxDurability, Is.EqualTo(250), "前置：items 表铁镐 maxDurability = 250");

            EnchantingUi.DoEnchant(_ctx, _ctx.Inventory.GetSlot(0), EnchantingTable.CostForLevel(1), 1);

            var enchanted = _ctx.Inventory.GetSlot(0);
            Assert.That(enchanted.ItemId, Is.EqualTo(pick.NumericId), "工具本体留在原格");
            Assert.That(enchanted.Metadata, Is.EqualTo(0),
                "附魔占位不写 Metadata——终审 I1：旧实现在这里给新工具写死 max=100，铁镐 250 被静默砍");

            // 关键：附魔后的铁镐首次磨损仍按 items 表 250 落编码，不是 100
            var worn = enchanted.WithDurabilityUsed(pick.MaxDurability);
            Assert.That(worn.HasDurability, Is.True, "首耗按表内 max 落编码");
            Assert.That(worn.MaxDurability, Is.EqualTo(250), "耐久上限 = items 表 250（唯一来源）");
            Assert.That(worn.CurrentDurability, Is.EqualTo(249), "首耗扣 1");

            // 消耗照扣（占位语义保留）：Lv1 耗 1 青金石、5 经验
            Assert.That(_ctx.Inventory.GetSlot(1).Count, Is.EqualTo(63), "青金石扣 1");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(25), "经验扣 5");
        }

        [Test]
        public void 附魔已磨损镐_耐久位原样保留()
        {
            PrepareIronPickaxe();
            var pick = _db.GetById("iron_pickaxe");
            // 预先磨损 3 次：max 250 / cur 247（走唯一的消耗初始化路径）
            var worn = new ItemStack(pick.NumericId, 1);
            for (int i = 0; i < 3; i++) worn = worn.WithDurabilityUsed(pick.MaxDurability);
            _ctx.Inventory.SetSlot(0, worn);
            Assert.That(worn.MaxDurability, Is.EqualTo(250), "前置：磨损后 max=250");
            Assert.That(worn.CurrentDurability, Is.EqualTo(247), "前置：磨损 3 点");

            EnchantingUi.DoEnchant(_ctx, _ctx.Inventory.GetSlot(0), EnchantingTable.CostForLevel(3), 3);

            var enchanted = _ctx.Inventory.GetSlot(0);
            Assert.That(enchanted.Metadata, Is.EqualTo(worn.Metadata),
                "已落耐久编码的镐经附魔后 Metadata 一位不变（上限/剩余都原样保留）");
        }
    }
}
#endif
