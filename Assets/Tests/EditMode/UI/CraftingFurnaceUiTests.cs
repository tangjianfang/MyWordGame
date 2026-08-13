#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;
using MyWorld.Core.Items;

namespace MyWorld.Core.Tests.UI
{
    public class CraftingFurnaceUiTests
    {
        [Test]
        public void Bind_ShowsProgress()
        {
            var go = new GameObject("FurnaceUI");
            var ui = go.AddComponent<CraftingFurnaceUi>();
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            f.AddInput(new ItemStack(1, 1));
            f.AddFuel(new ItemStack(10, 1));
            ui.Bind(f);
            f.Tick(0.5f);
            ui.TickForTest();
            Assert.That(ui.CurrentProgress, Is.EqualTo(0.5f).Within(0.01f));
            Object.DestroyImmediate(go);
        }
    }
}
#endif
