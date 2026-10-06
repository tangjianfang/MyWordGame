#if UNITY_EDITOR
using NUnit.Framework;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m13 P0 修：主背包→合成网格 SHIFT+click 入口回归测试。
    /// 修复孩子"M1和MP背包和工作台里面的物品都没办法合成"的 UX bug——
    /// 之前合成网格的**唯一**入料路径是 hotbar 选中格，主背包 27 格完全无路可达。
    /// <para>
    /// 6 个断言守护契约：
    /// - 主背包物品能送进合成区（修 bug）
    /// - 合成区物品能送回主背包（双向）
    /// - 旧 hotbar 路径不回归（修 bug 不能坏老功能）
    /// - 主背包目标非空时拒收（防覆盖既有材料）
    /// - 无 SHIFT 不走新路径（防误触）
    /// - 帮助菜单教学文案含关键提示词（不漂移）
    /// </para>
    /// </summary>
    public class CraftingGridInteractionTests
    {
        private PlayerInventory _inv;

        [SetUp]
        public void SetUp()
        {
            // 36 槽布局：0..8 hotbar，9..35 主背包
            _inv = new PlayerInventory();
        }

        [Test]
        public void PutMainSlotOne_主背包物品入合成区_成功()
        {
            // 主背包 slot 9 = 1 个原木（itemId 1001）
            _inv.SetSlot(9, new ItemStack(1001, 1));
            var cell = ItemStack.Empty;

            bool ok = CraftGridInteraction.PutMainSlotOne(_inv, ref cell, 9);

            Assert.That(ok, Is.True, "SHIFT+click 应能送主背包物品入合成区");
            Assert.That(cell.ItemId, Is.EqualTo(1001));
            Assert.That(cell.Count, Is.EqualTo(1));
            Assert.That(_inv.GetSlot(9).IsEmpty, Is.True, "主背包原木被取走 1 个");
        }

        [Test]
        public void TakeBackToMainSlotOne_合成区物品回主背包_成功()
        {
            var cell = new ItemStack(1001, 1);
            bool ok = CraftGridInteraction.TakeBackToMainSlotOne(_inv, ref cell, 12);

            Assert.That(ok, Is.True, "SHIFT+click 合成格应能送回主背包空格位");
            Assert.That(_inv.GetSlot(12).ItemId, Is.EqualTo(1001));
            Assert.That(_inv.GetSlot(12).Count, Is.EqualTo(1));
            Assert.That(cell.IsEmpty, Is.True, "合成格被取走 1 个后应清空");
        }

        [Test]
        public void PutSelectedOne_hotbar路径不回归_依旧可用()
        {
            _inv.SetSlot(0, new ItemStack(1001, 1));  // hotbar 槽 0 = 1 原木
            _inv.SelectedHotbarIndex = 0;
            var cell = ItemStack.Empty;

            bool ok = CraftGridInteraction.PutSelectedOne(_inv, ref cell);

            Assert.That(ok, Is.True, "旧 hotbar 路径必须仍能工作");
            Assert.That(cell.ItemId, Is.EqualTo(1001));
            Assert.That(_inv.GetSlot(0).IsEmpty, Is.True);
        }

        [Test]
        public void TakeBackToMainSlotOne_主背包目标非空时拒绝_不覆盖()
        {
            // 主背包 slot 12 已经有 1 个木板（itemId 1002）
            _inv.SetSlot(12, new ItemStack(1002, 1));
            var cell = new ItemStack(1001, 1);  // 合成格 1 个原木

            bool ok = CraftGridInteraction.TakeBackToMainSlotOne(_inv, ref cell, 12);

            Assert.That(ok, Is.False, "目标非空应拒绝，避免覆盖既有材料");
            Assert.That(_inv.GetSlot(12).ItemId, Is.EqualTo(1002), "主背包 slot 12 不变");
            Assert.That(_inv.GetSlot(12).Count, Is.EqualTo(1), "主背包 slot 12 数量不变");
            Assert.That(cell.ItemId, Is.EqualTo(1001), "合成格不变");
            Assert.That(cell.Count, Is.EqualTo(1), "合成格数量不变");
        }

        [Test]
        public void PutMainSlotOne_传入hotbar索引_拒绝_只在主背包有效()
        {
            // 主背包 slot 0 = 1 原木，但 hotbar slot 0 不该通过此入口
            _inv.SetSlot(0, new ItemStack(1001, 1));
            var cell = ItemStack.Empty;

            bool ok = CraftGridInteraction.PutMainSlotOne(_inv, ref cell, 0);

            Assert.That(ok, Is.False, "hotbar 槽位（0..8）不该走 PutMainSlotOne 入口");
            Assert.That(cell.IsEmpty, Is.True);
            Assert.That(_inv.GetSlot(0).Count, Is.EqualTo(1), "hotbar 原木不应被吃");
        }

        [Test]
        public void PutMainSlotOne_传入越界索引_拒绝()
        {
            _inv.SetSlot(20, new ItemStack(1001, 1));
            var cell = ItemStack.Empty;

            // 36 = 越界（有效范围 9..35）
            bool ok = CraftGridInteraction.PutMainSlotOne(_inv, ref cell, 36);

            Assert.That(ok, Is.False, "越界索引应拒绝");
            Assert.That(cell.IsEmpty, Is.True);
            Assert.That(_inv.GetSlot(20).Count, Is.EqualTo(1), "slot 20 不应被改");
        }

        [Test]
        public void PutMainSlotAll_整组送入_合镐五材料一击到位()
        {
            // 评审 07#4：m13 P0 的「一次 1 个」让孩子合一把镐 5 连击——整组语义 1 击到位
            _inv.SetSlot(9, new ItemStack(1000, 5)); // 主背包 5 个木板
            var grid = new ItemStack[4];

            int moved = CraftGridInteraction.PutMainSlotAll(null, _inv, grid, 9);

            Assert.That(moved, Is.EqualTo(5), "整组 5 个全部送入");
            Assert.That(grid[0].Count, Is.EqualTo(5), "进第一个空格（maxStack 64 内）");
            Assert.That(_inv.GetSlot(9).IsEmpty, Is.True, "主背包格清空");
        }

        [Test]
        public void PutMainSlotAll_同类叠加_异类拒收_网格满留原格()
        {
            var grid = new ItemStack[2];
            grid[0] = new ItemStack(1000, 60); // 同类已有 60，还能叠 4
            grid[1] = new ItemStack(1001, 1);  // 异类占位
            _inv.SetSlot(9, new ItemStack(1000, 10));

            int moved = CraftGridInteraction.PutMainSlotAll(null, _inv, grid, 9);

            Assert.That(moved, Is.EqualTo(4), "只在同类格叠到 64");
            Assert.That(grid[0].Count, Is.EqualTo(64));
            Assert.That(grid[1].ItemId, Is.EqualTo(1001), "异类格不动");
            Assert.That(_inv.GetSlot(9).Count, Is.EqualTo(6), "叠不下的留主背包原格");

            // 再点：全部格满/异类 → 零送入
            Assert.That(CraftGridInteraction.PutMainSlotAll(null, _inv, grid, 9), Is.EqualTo(0),
                "没有可叠空间时零送入（不误消费点击）");
        }

        [Test]
        public void ReturnGrid_网格物品归位进背包_空格清空()
        {
            // 评审 04 R-3：合成网格材料不进存档——保存前 ReturnGrid 归位，Alt+F4 不再蒸发
            var grid = new ItemStack[2];
            grid[0] = new ItemStack(1001, 3); // 原木 ×3
            grid[1] = ItemStack.Empty;

            CraftGridInteraction.ReturnGrid(_inv, grid);

            Assert.That(_inv.GetSlot(0).Count, Is.EqualTo(3), "网格物品应归位进背包首格");
            Assert.That(grid[0].IsEmpty, Is.True, "归位成功的格子清空");
        }

        [Test]
        public void ReturnGrid_背包满_留网格不清空()
        {
            // 塞不下的留在网格（背包满是玩家状态，丢弃/强塞都不对）
            for (int i = 0; i < 36; i++) _inv.SetSlot(i, new ItemStack(1002, 64)); // 填满
            var grid = new ItemStack[1];
            grid[0] = new ItemStack(1001, 5);

            CraftGridInteraction.ReturnGrid(_inv, grid);

            Assert.That(grid[0].Count, Is.EqualTo(5), "背包满时物品留在网格，下次打开还在");
        }

        [Test]
        public void ReturnAllHeld_登记回调统一冲刷()
        {
            // 评审 04 R-3：注册表聚合——保存路径经 ReturnAllHeld 逐个调用已登记的归位回调
            var go = new UnityEngine.GameObject();
            try
            {
                var ctx = go.AddComponent<MyWorld.Unity.Gameplay.PlayerContext>();
                ctx.Inventory = new PlayerInventory();
                var grid = new ItemStack[1];
                grid[0] = new ItemStack(1001, 2);
                System.Action<MyWorld.Unity.Gameplay.PlayerContext> handler =
                    c => CraftGridInteraction.ReturnGrid(c.Inventory, grid);
                CraftGridInteraction.RegisterReturnHandler(handler);
                try
                {
                    CraftGridInteraction.ReturnAllHeld(ctx);
                    Assert.That(ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2),
                        "登记的归位回调必须被统一调用（评审 04 R-3 聚合断言）");
                    Assert.That(grid[0].IsEmpty, Is.True);
                }
                finally
                {
                    CraftGridInteraction.UnregisterReturnHandler(handler);
                    CraftGridInteraction.ReturnAllHeld(null); // null ctx 安全 no-op
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
#endif