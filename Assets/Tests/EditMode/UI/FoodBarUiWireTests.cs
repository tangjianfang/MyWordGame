#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;
using MyWorld.Core.Player;

namespace MyWorld.Core.Tests.UI
{
    public class FoodBarUiWireTests
    {
        [Test]
        public void FoodBarUI_ReflectsHungerSystemValue()
        {
            var go = new GameObject("FoodBarTest");
            var fb = go.AddComponent<FoodBarUI>();
            var hs = new HungerSystem { Hunger = 10 };
            fb.Bind(hs);
            fb.TickForTest();
            Assert.That(fb.CurrentHunger, Is.EqualTo(10), "UI 跟随 HungerSystem");
            Object.DestroyImmediate(go);
        }
    }
}
#endif
