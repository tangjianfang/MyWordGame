#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;
using MyWorld.Unity.Bootstrap;
using MyWorld.Core.Items;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// Task B6：验证 CraftingInventoryUi 接 RecipeDatabase。
    /// 2×2 网格能匹配配方并把输出写到 LastOutput；不匹配时返回 null。
    /// <para>
    /// 选用 planks_to_sticks（2 plank → 4 stick，shaped=false）作为正向断言：
    /// brief 原计划用 crafting_table，但该配方是 workbench 3×3 档，2×2 网格匹配不到；
    /// 保持 2×2 默认尺寸下，sticks / bowl 是唯一能跑通的最短路径。
    /// </para>
    /// </summary>
    public class CraftingInventoryUiTests
    {
        [Test]
        public void MatchTwoPlanksInGrid_ProducesSticks()
        {
            var go = new GameObject("CraftUI");
            var ui = go.AddComponent<CraftingInventoryUi>();
            var items = ItemDatabaseLoader.Load();
            var db = ItemDatabaseLoader.LoadRecipes(items);
            ui.Bind(db);

            // 2 个 plank 放 2x2 对角；planks_to_sticks shaped=false，只数 plank 总数=2
            ui.SetGridForTest(new int[] { 1001, 0, 0, 1001 });
            ui.CraftForTest();

            Assert.That(ui.LastOutput.HasValue, Is.True, "匹配到配方后 LastOutput 必须有值");
            Assert.That(ui.LastOutput.Value.ItemId, Is.EqualTo(1002), "stick itemId 应为 1002");
            Assert.That(ui.LastOutput.Value.Count, Is.EqualTo(4), "planks_to_sticks 输出 4 根");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void NoMatch_ProducesNothing()
        {
            var go = new GameObject("CraftUI");
            var ui = go.AddComponent<CraftingInventoryUi>();
            var items = ItemDatabaseLoader.Load();
            var db = ItemDatabaseLoader.LoadRecipes(items);
            ui.Bind(db);

            // itemId 1-4 物品库里没注册，配方 need 都拿不到 → 不匹配
            ui.SetGridForTest(new int[] { 1, 2, 3, 4 });
            ui.CraftForTest();

            Assert.That(ui.LastOutput, Is.Null);
            Object.DestroyImmediate(go);
        }
    }
}
#endif