#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;
using MyWorld.Unity.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Core.Entities;
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

        // ─── m10 C2：右键复活（spec §4——按钮之外的等效触发，OnGUI 里路由） ─────
        // 注：走 raw 值入口（EventType + 按钮号）——EditMode 批处理下 Event.type 的
        // setter 不落值（new Event { type = MouseDown } 读回 Ignore），构造不出真右键事件。

        [Test]
        public void IsRespawnRightClick_OnlyAcceptsRightMouseDown()
        {
            Assert.That(DeathScreenUi.IsRespawnRightClick(EventType.MouseDown, 1), Is.True,
                "右键按下 = 复活");

            Assert.That(DeathScreenUi.IsRespawnRightClick(EventType.MouseDown, 0), Is.False, "左键不算");
            Assert.That(DeathScreenUi.IsRespawnRightClick(EventType.MouseUp, 1), Is.False,
                "右键抬起不算（只认按下）");
            Assert.That(DeathScreenUi.IsRespawnRightClick(EventType.Layout, 0), Is.False, "布局事件不算");
            Assert.That(DeathScreenUi.IsRespawnRightClick(EventType.MouseDown, 2), Is.False,
                "中键不算（只认右键 button=1）");
            Assert.That(DeathScreenUi.IsRespawnRightClick(null), Is.False,
                "Event 包装版对 null 事件（非 OnGUI 路径）应安全返回 false");
        }

        [Test]
        public void HandleRightClick_RespawningPhase_SkipsRespawnCountdown()
        {
            var holder = new GameObject("玩家-右键复活");
            var go = new GameObject("死亡画面-右键");
            try
            {
                var ctx = holder.AddComponent<PlayerContext>();
                InvokeAwake(ctx);
                ctx.Death = new DeathSystem();

                var ui = go.AddComponent<DeathScreenUi>();
                InvokeAwake(ui);

                // Dying 阶段：按钮尚不可点，右键同样不吃（与按钮同一道门）
                ctx.Death.OnDeath(new MyWorld.Core.Math.Float3(0f, 0f, 0f));
                Assert.That(ctx.Death.Phase, Is.EqualTo(DeathPhase.Dying));
                ui.HandleRightClickCore(EventType.MouseDown, 1);
                Assert.That(ctx.Death.PhaseTimer, Is.EqualTo(DeathSystem.DeathScreenDelay),
                    "Dying 阶段右键应为 no-op——倒计时原封不动");

                // 推进到 Respawning：右键清零倒计时，Update 检测 Respawning→Alive 后真正复活
                ctx.Death.Tick(DeathSystem.DeathScreenDelay + 0.1f);
                Assert.That(ctx.Death.Phase, Is.EqualTo(DeathPhase.Respawning));
                ui.HandleRightClickCore(EventType.MouseDown, 1);
                Assert.That(ctx.Death.PhaseTimer, Is.EqualTo(0f),
                    "Respawning 阶段右键应与按钮同效：清零 PhaseTimer，下一帧 Update 收尾复活");
            }
            finally
            {
                // 无论断言成败都拆干净：PlayerContext.Instance 残留 live 实例会让
                // 后续 fixture 的 Awake 走 Destroy(this)（EditMode 禁用）连环红
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(holder);
            }
        }
    }
}
#endif