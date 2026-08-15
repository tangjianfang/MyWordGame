#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;
using MyWorld.Unity.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Core.Player;

namespace MyWorld.Core.Tests.UI
{
    public class DeathScreenUiWireTests
    {
        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake
        /// （Unity 仅在 PlayMode / 场景加载时回调），用反射显式调用私有 Awake。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [Test]
        public void Respawn_ResetsHealthAndHunger()
        {
            var go = new GameObject("Player");
            // PlayerContext 必须先于 PlayerController 添加，与 WorldBootstrap.Awake 顺序一致。
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            var pc = go.AddComponent<PlayerController>();
            InvokeAwake(pc);

            pc.TakeDamage(100, null);  // 致死
            pc.Respawn(Vector3.zero);
            // m5 A2：伤害 / 重生统一走 PlayerContext.Health（唯一真源）
            Assert.That(ctx.Health.Current, Is.GreaterThan(0), "重生后 Health > 0");
            // HungerSystem 通过 PlayerContext 取
            Assert.That(ctx.HungerSystem, Is.Not.Null, "PlayerContext.Awake 应初始化 HungerSystem");
            Assert.That(ctx.HungerSystem.Hunger, Is.EqualTo(HungerSystem.MaxHunger), "重生后 Hunger 满");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DeathScreenUi_ShowsOnDeath()
        {
            var go = new GameObject("DeathUI");
            var ui = go.AddComponent<DeathScreenUi>();
            InvokeAwake(ui);
            ui.OnPlayerDied();
            Assert.That(ui.IsVisible, Is.True, "OnPlayerDied 后 IsVisible=true");
            Object.DestroyImmediate(go);
        }
    }
}
#endif