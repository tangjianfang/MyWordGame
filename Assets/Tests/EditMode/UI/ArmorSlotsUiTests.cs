#if UNITY_EDITOR
// m11 W2-1：穿戴栏 UI——点空槽自动穿上背包里第一件对应部位盔甲、点已穿槽脱下，
// 穿戴中槽位高亮、面板给三属性汇总提示。EditMode 测试与 OnGUI 共用 ClickSlot 入口
//（与 CraftingInventoryUi.ClickGridCell 同款模式），不驱动真实 IMGUI 事件。
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
    public class ArmorSlotsUiTests
    {
        private GameObject _go;
        private PlayerContext _ctx;
        private ArmorSlotsUi _ui;
        private ItemDatabase _db;

        [SetUp]
        public void SetUp()
        {
            // 指针门是静态计数，跨夹具残留会污染开关断言——SetUp/TearDown 都复位
            //（与 UiCursorGateTests 同款）
            UiCursorGate.Reset();
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            _db = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));

            _go = new GameObject("穿戴栏UI");
            _ctx = _go.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，显式初始化
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _ctx.Items = _db;
            _ui = _go.AddComponent<ArmorSlotsUi>();
            _ui.Bind(_ctx); // 测试显式注入，绕过 PlayerContext.Instance（EditMode 不回调 Awake）
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            UiCursorGate.Reset();
        }

        private void PutInBag(string itemId, int slot)
        {
            _ctx.Inventory.SetSlot(slot, new ItemStack(_db.GetById(itemId).NumericId, 1));
        }

        [Test]
        public void 点空槽_背上第一件对应部位盔甲自动穿上()
        {
            PutInBag("plank", 3);          // 干扰项：木板不是盔甲，扫描必须跳过
            PutInBag("iron_helmet", 9);    // 背包区第一件头盔
            PutInBag("iron_chest", 10);

            bool ok = _ui.ClickSlot(0);

            Assert.That(ok, Is.True, "点头部空槽应穿上头盔");
            Assert.That(_ctx.ArmorSlots.GetSlot(0).ItemId,
                Is.EqualTo(_db.GetById("iron_helmet").NumericId), "穿上的是背包里第一件头盔");
            Assert.That(_ctx.Inventory.GetSlot(9), Is.EqualTo(ItemStack.Empty), "源格清空");
            Assert.That(_ctx.Inventory.GetSlot(10).IsEmpty, Is.False, "胸甲还在背包（部位不匹配不动）");
            Assert.That(_ui.IsSlotWorn(0), Is.True, "穿上后槽位高亮");
        }

        [Test]
        public void 点已穿槽_脱下回背包_高亮熄灭()
        {
            PutInBag("iron_chest", 5);
            _ui.ClickSlot(1);
            Assert.That(_ui.IsSlotWorn(1), Is.True, "前置：穿着铁胸甲");

            bool ok = _ui.ClickSlot(1);

            Assert.That(ok, Is.True, "点已穿槽应脱下");
            Assert.That(_ctx.ArmorSlots.GetSlot(1), Is.EqualTo(ItemStack.Empty), "穿戴槽清空");
            Assert.That(_ctx.Inventory.CountOf(_db.GetById("iron_chest").NumericId), Is.EqualTo(1),
                "胸甲回到背包");
            Assert.That(_ui.IsSlotWorn(1), Is.False, "空槽不高亮");
        }

        [Test]
        public void 点空槽_背包没有对应部位_无事发生()
        {
            PutInBag("iron_helmet", 0); // 只有头盔

            bool ok = _ui.ClickSlot(3); // 点脚部槽

            Assert.That(ok, Is.False, "没有靴子可穿");
            Assert.That(_ctx.ArmorSlots.GetSlot(3), Is.EqualTo(ItemStack.Empty), "脚槽保持空");
            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.False, "头盔留在背包，不误穿");
        }

        [Test]
        public void 点空槽_背包里只有非盔甲_拒绝()
        {
            PutInBag("plank", 0);

            Assert.That(_ui.ClickSlot(0), Is.False, "木板穿不进任何槽");
            Assert.That(_ctx.ArmorSlots.GetSlot(0), Is.EqualTo(ItemStack.Empty));
        }

        [Test]
        public void 穿脱后_三属性即时刷新()
        {
            // 选中槽挪到空格：脱下的头盔会回到背包第一个空位（0 槽）——若 0 槽正是
            // 选中槽，手持源会按「拿着未穿的盔甲照样生效」语义接力 +1，
            // 那就不是「穿戴源归零」的干净观测了
            _ctx.Inventory.SelectedHotbarIndex = 7;
            PutInBag("iron_helmet", 0);
            _ui.ClickSlot(0);
            Assert.That(_ctx.Defense, Is.EqualTo(1), "穿上当场刷新（不等下一帧 Update）");

            _ui.ClickSlot(0);
            Assert.That(_ctx.Defense, Is.EqualTo(0), "脱下当场归零");
        }

        [Test]
        public void 汇总提示_穿铁甲后含防御字样_空穿戴提示无加成()
        {
            PutInBag("iron_helmet", 0);
            PutInBag("iron_chest", 1);

            _ui.ClickSlot(0);
            _ui.ClickSlot(1);

            string summary = _ui.BonusSummaryText();
            Assert.That(summary, Does.Contain("防御"), "穿戴生效提示要点出防御： " + summary);
            Assert.That(summary, Does.Contain("3"), "头1+胸2 = 防御 3： " + summary);
        }

        [Test]
        public void 汇总提示_空穿戴_零加成文案()
        {
            StringAssert.DoesNotContain("防御", _ui.BonusSummaryText(), "零防御不显示防御项");
        }

        [Test]
        public void 开关面板_登记指针门_关闭释放()
        {
            _ui.SetOpen(true);
            Assert.That(UiCursorGate.IsOpen, Is.True, "穿戴栏是模态 UI，打开必须解锁指针");

            _ui.SetOpen(false);
            Assert.That(UiCursorGate.IsOpen, Is.False, "关闭必须把门位还回去（计数不泄漏）");
        }
    }
}
#endif
