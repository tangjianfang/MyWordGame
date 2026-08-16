#if UNITY_EDITOR
// m9 B1：战斗手感四件套——受击闪红 0.15s / 击退 1.5m 冲量（衰减非瞬移）/ 死亡缩小 0.3s /
// 命中打击音（PlayerAudioSystem.PlayHit，音频缺失静默）。
// 依赖 MyWorld.Unity.Combat.MobHitFeedback，dotnet 链跑不动，
// 整个文件用 #if UNITY_EDITOR 包裹（与 PlayerAttackTests / MobDeathDropTests 同款）。
// 时间注入：全部经 MobHitFeedback.TickFeedback(dt) 直调步长驱动（EditMode 下
// Update 不回调、Time.deltaTime 冻结，与 CombatController.LastAttackTime 字段直改同思路）。
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Unity.Audio;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// m9 B1 契约（spec §4「手感四件套」）：
    /// 1) 闪红：FlashRed() 对宿主全部 Renderer 的 MPB tint 涂红（非白/非原色），
    ///    0.15s 后过期 Clear 复原；时长与 Core MobAI.HitFlashDuration 同值（两通道不漂移）。
    /// 2) 击退：ApplyKnockback(dir) 首帧位移沿 dir 水平分量、远小于总量（非瞬移），
    ///    速度指数衰减，累计位移恰好 1.5m，烧完后位置冻结。
    /// 3) 缩小：PlayDeathShrink() 后 scale 从原值插值 0.3s 到 0（等比、幂等），
    ///    且 0.3s < MobAI.DefaultDeathTimer——「到 0 才走既有移除」。
    /// 4) 命中音：PlayerAudioSystem.PlayHit() 缺 clip 不抛（PlayFootstep 防御模式）。
    /// 5) 接线：CombatController 近战命中触发闪红 + 击退，致死一击武装缩小。
    /// </summary>
    [TestFixture]
    public class MobHitFeedbackTests
    {
        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake，
        /// 用反射显式调用（与 PlayerAttackTests / MobDeathDropTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        /// <summary>读渲染器 MPB 实例色（与 MobViewPerKindTests.ReadColor 同款）。</summary>
        private static Color ReadColor(Renderer r)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        /// <summary>按 MobManager.SpawnMob 同款手法造一个带反馈组件的拼装猪宿主。</summary>
        private static MobHitFeedback BuildPigHost(out GameObject host, out Mob mob)
        {
            host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            host.name = "MobHitFeedback_PigHost";
            mob = Mob.Create(6, new Float3(0f, 70f, 0f)); // mobTypeId 6 = 猪（部位表拼装）
            MobView.Attach(host, mob);
            return MobHitFeedback.Attach(host, mob);
        }

        // ─── 闪红 ─────────────────────────────────────────────────────────

        /// <summary>
        /// FlashRed 应把宿主全部渲染器（含拼装部位）的 MPB 实例色涂成红——
        /// 红 ≠ 白也 ≠ 部位原色（猪身粉），且同步刷 Core 计时器（MobView 的每帧
        /// 红闪渲染通道与本组件同窗，见 MobHitFeedback 类注释）。
        /// </summary>
        [Test]
        public void FlashRed_TintsAllRenderersRed_AndSyncsCoreTimer()
        {
            var fb = BuildPigHost(out var host, out var mob);
            try
            {
                var renderers = host.GetComponentsInChildren<Renderer>();
                Assume.That(renderers.Length, Is.GreaterThan(1), "拼装猪应有多个部位渲染器");
                foreach (var r in renderers)
                {
                    Assume.That(ReadColor(r), Is.Not.EqualTo(Color.red), "前置：未受击时部位不应是纯红");
                }

                fb.FlashRed();

                foreach (var r in renderers)
                {
                    Assert.That(ReadColor(r), Is.EqualTo(Color.red),
                        r.name + " 受击瞬间应被 MPB tint 涂红（非白/非原色）");
                }
                Assert.That(mob.HitFlashTimer, Is.EqualTo(MobHitFeedback.FlashDuration),
                    "FlashRed 应同步刷新 Core 计时器——MobView 每帧红闪渲染由它驱动");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 闪红 0.15s 后过期：MPB 上的红 tint 被 Clear 掉（实例色 override 消失，
        /// 材质底色透出来——职业染色不被破坏）。时间经 TickFeedback 注入。
        /// </summary>
        [Test]
        public void FlashRed_ClearsTintAfterDuration_TimeInjected()
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mob = Mob.Create(1, new Float3(0f, 70f, 0f)); // 旧 Passive：单 cube 路径
                MobView.Attach(host, mob);
                var fb = MobHitFeedback.Attach(host, mob);
                var renderer = host.GetComponent<Renderer>();

                fb.FlashRed();
                Assert.That(ReadColor(renderer), Is.EqualTo(Color.red), "闪红即时生效");

                fb.TickFeedback(0.1f); // 0.1s < 0.15s：仍在闪
                Assert.That(ReadColor(renderer), Is.EqualTo(Color.red),
                    "0.15s 未到不应提前复原");

                fb.TickFeedback(0.1f); // 累计 0.2s ≥ 0.15s：过期清 override
                Assert.That(ReadColor(renderer), Is.Not.EqualTo(Color.red),
                    "0.15s 过期后应 Clear 掉红 tint（复原为材质底色）");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 闪红时长常量对齐：spec §4 规定 0.15s，且 Unity 侧 FlashDuration 与 Core
        /// MobAI.HitFlashDuration 必须同值——MobView（Core 计时器驱动）与本组件
        /// （自有计时器驱动）是同一个红闪的两条渲染通道，漂移就会出现一个红一个不红。
        /// 这也是 A2 评审 Minor 1 预告的一处对齐（0.2s → 0.15s）。
        /// </summary>
        [Test]
        public void FlashDuration_IsPointOneFive_AndMatchesCoreConstant()
        {
            Assert.That(MobHitFeedback.FlashDuration, Is.EqualTo(0.15f), "spec §4：闪红 0.15s");
            Assert.That(MobHitFeedback.FlashDuration, Is.EqualTo(MobAI.HitFlashDuration),
                "Unity 侧与 Core 侧闪红时长必须同值（两通道同窗）");
        }

        // ─── 击退 ─────────────────────────────────────────────────────────

        /// <summary>
        /// ApplyKnockback 首帧：位移沿传入方向的水平分量（纵向垃圾分量丢弃、归一化），
        /// 位移量级明显（有冲量感）但远小于总量 1.5m——衰减冲量，不是瞬移。
        /// </summary>
        [Test]
        public void ApplyKnockback_FirstFrameMovesAlongHorizontalDirection()
        {
            var host = new GameObject("MobHitFeedback_KbHost");
            try
            {
                var mob = Mob.Create(6, new Float3(10f, 70f, 20f));
                var fb = MobHitFeedback.Attach(host, mob);

                fb.ApplyKnockback(new Vector3(3f, 99f, 4f)); // (3,*,4) → 水平归一 (0.6, 0, 0.8)
                fb.TickFeedback(1f / 60f);

                var delta = new Vector3(mob.Position.X - 10f, 0f, mob.Position.Z - 20f);
                var dir = new Vector3(0.6f, 0f, 0.8f);
                Assert.That(mob.Position.Y, Is.EqualTo(70f), "击退不改变 Y（体素世界 mob 不离地）");
                Assert.That(delta.magnitude, Is.GreaterThan(0.1f), "首帧应有明显冲量位移");
                Assert.That(delta.magnitude, Is.LessThan(0.5f), "冲量非瞬移——首帧远小于总量 1.5m");
                Assert.That(Vector3.Dot(delta, dir), Is.EqualTo(delta.magnitude).Within(0.001f),
                    "首帧位移方向应沿击退方向（水平归一后）");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 击退速度应随时间衰减：前 0.1s 窗口的位移显著大于后 0.1s 窗口
        /// （指数衰减——猛推一下然后快速失速，而不是匀速滑走）。
        /// </summary>
        [Test]
        public void ApplyKnockback_SpeedDecaysOverTime()
        {
            var host = new GameObject("MobHitFeedback_KbDecayHost");
            try
            {
                var mob = Mob.Create(6, new Float3(0f, 70f, 0f));
                var fb = MobHitFeedback.Attach(host, mob);
                fb.ApplyKnockback(Vector3.forward);

                float DisplacementOver(int ticks)
                {
                    float x0 = mob.Position.X, z0 = mob.Position.Z;
                    for (int i = 0; i < ticks; i++) fb.TickFeedback(1f / 60f);
                    return Mathf.Sqrt(
                        (mob.Position.X - x0) * (mob.Position.X - x0) +
                        (mob.Position.Z - z0) * (mob.Position.Z - z0));
                }

                float firstWindow = DisplacementOver(6);  // [0s, 0.1s]
                float secondWindow = DisplacementOver(6); // [0.1s, 0.2s]
                Assert.That(firstWindow, Is.GreaterThan(secondWindow * 1.5f),
                    $"击退速度应衰减：前 0.1s 位移 {firstWindow:F3} 应明显大于后 0.1s {secondWindow:F3}");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 击退总量恰好 1.5m（spec §4）：烧完后 KnockbackRemaining 归零、位置冻结，
        /// 且全程方向不漂（位移向量与击退方向同向）。
        /// </summary>
        [Test]
        public void ApplyKnockback_TotalDisplacementReachesOnePointFiveMeters_ThenFreezes()
        {
            var host = new GameObject("MobHitFeedback_KbTotalHost");
            try
            {
                var mob = Mob.Create(6, new Float3(0f, 70f, 0f));
                var fb = MobHitFeedback.Attach(host, mob);
                fb.ApplyKnockback(Vector3.right);

                for (int i = 0; i < 600; i++) fb.TickFeedback(1f / 60f); // 10s：冲量早烧完

                float totalX = mob.Position.X;
                Assert.That(totalX, Is.EqualTo(1.5f).Within(0.01f),
                    $"击退累计位移应恰好 1.5m（实际 {totalX:F4}）");
                Assert.That(mob.Position.Z, Is.EqualTo(0f).Within(1e-4f), "方向不漂——只在击退方向上动");
                Assert.That(fb.KnockbackRemaining, Is.EqualTo(0f).Within(1e-4f), "冲量烧完后剩余量归零");

                fb.TickFeedback(1f / 60f);
                Assert.That(mob.Position.X, Is.EqualTo(totalX).Within(1e-5f),
                    "冲量耗尽后位置冻结，不再移动");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>零向量方向的兜底：与 MobAI 逃跑重合同款——任取 +X，不除零不 NaN。</summary>
        [Test]
        public void ApplyKnockback_ZeroDirection_FallsBackToUnitX()
        {
            var host = new GameObject("MobHitFeedback_KbZeroHost");
            try
            {
                var mob = Mob.Create(6, new Float3(0f, 70f, 0f));
                var fb = MobHitFeedback.Attach(host, mob);

                fb.ApplyKnockback(Vector3.zero);
                fb.TickFeedback(1f / 60f);

                Assert.That(mob.Position.X, Is.GreaterThan(0f), "零方向应兜底沿 +X 击退");
                Assert.That(mob.Position.Z, Is.EqualTo(0f).Within(1e-5f), "Z 不动");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ─── 死亡缩小 ─────────────────────────────────────────────────────

        /// <summary>
        /// PlayDeathShrink：scale 从原值等比插值 0.3s 到 0（中点约一半），
        /// 幂等（中途再调不重置进度），到 0 后 IsShrinkComplete 为真。
        /// </summary>
        [Test]
        public void PlayDeathShrink_InterpolatesScaleToZero_OverPointThreeSeconds()
        {
            var host = new GameObject("MobHitFeedback_ShrinkHost");
            try
            {
                var mob = Mob.Create(6, new Float3(0f, 70f, 0f));
                var fb = MobHitFeedback.Attach(host, mob);
                var startScale = new Vector3(0.8f, 1f, 1.2f); // 旧单 cube mob 的体型
                host.transform.localScale = startScale;

                fb.PlayDeathShrink();
                Assert.That(fb.IsShrinkComplete, Is.False, "武装后未完成");

                fb.TickFeedback(0.15f); // 中点：等比缩一半
                var mid = host.transform.localScale;
                Assert.That(mid.x, Is.EqualTo(startScale.x * 0.5f).Within(0.01f), "0.15s 时 X 约为一半");
                Assert.That(mid.y, Is.EqualTo(startScale.y * 0.5f).Within(0.01f), "0.15s 时 Y 约为一半");
                Assert.That(mid.z, Is.EqualTo(startScale.z * 0.5f).Within(0.01f), "0.15s 时 Z 约为一半");
                Assert.That(fb.IsShrinkComplete, Is.False, "0.15s 只走了一半");

                fb.PlayDeathShrink(); // 幂等：不应重置进度
                fb.TickFeedback(0.16f); // 累计 0.31s ≥ 0.3s：走完
                Assert.That(host.transform.localScale, Is.EqualTo(Vector3.zero),
                    "0.3s 走完后 scale 应精确到 0（若中途重置则到不了 0）");
                Assert.That(fb.IsShrinkComplete, Is.True, "走完后 IsShrinkComplete 为真");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 「到 0 才走既有移除」的不变量：缩小 0.3s 必须短于 MobManager 的 Dying
        /// 倒计时 0.5s——scale 归零之后尸体才被 RemoveMobAt 销毁，动画不会被腰斩。
        /// </summary>
        [Test]
        public void ShrinkDuration_CompletesBeforeDyingRemoval()
        {
            Assert.That(MobHitFeedback.ShrinkDuration, Is.EqualTo(0.3f), "spec §4：缩小 0.3s");
            Assert.That(MobHitFeedback.ShrinkDuration, Is.LessThan(MobAI.DefaultDeathTimer),
                "缩小必须先于 Dying 倒计时走完——到 0 才走既有移除");
        }

        // ─── 命中音 ───────────────────────────────────────────────────────

        /// <summary>命中打击音缺 clip 也不抛（nice-to-have，PlayFootstep 防御模式同款）。</summary>
        [Test]
        public void PlayHit_DoesNotThrowWhenClipMissing()
        {
            var go = new GameObject("PlayerAudio_HitTest");
            try
            {
                var audio = go.AddComponent<PlayerAudioSystem>();
                Assert.DoesNotThrow(() => audio.PlayHit(),
                    "hit clip 缺失时也不应抛异常（nice-to-have 不能阻断战斗）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ─── 接线：CombatController 命中触发四件套 ────────────────────────

        /// <summary>
        /// 近战命中应触发反馈链：闪红（MPB 涂红）+ 击退（沿远离玩家方向把 Core
        /// Position 推开）；致死一击（空手 1 伤 × 鸡 4 血 = 4 下）武装缩小动画。
        /// </summary>
        [Test]
        public void CombatController_MeleeHit_TriggersFlashKnockbackAndShrink()
        {
            var ctxHost = new GameObject("MobHitFeedback_Ctx");
            var playerHost = new GameObject("MobHitFeedback_Player");
            GameObject mobHost = null;
            try
            {
                BlockInteraction.InputLocked = false;
                UiCursorGate.Reset();

                var ctx = ctxHost.AddComponent<PlayerContext>();
                InvokeAwake(ctx); // Instance = ctx；Inventory 由 Awake 兜底创建

                var eye = new GameObject("相机"); // PlayerController.Awake 按名绑成 Eye
                eye.transform.SetParent(playerHost.transform);
                eye.transform.position = Vector3.zero;
                eye.transform.rotation = Quaternion.identity; // 面朝 +Z，mob 放 +Z 轴上
                var player = playerHost.AddComponent<PlayerController>();
                InvokeAwake(player);
                var combat = playerHost.AddComponent<CombatController>();
                combat.Player = player; // Hand 留 null：TryAttack 对 null Hand 安全跳过

                mobHost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mobHost.name = "MobHitFeedback_Chicken";
                mobHost.transform.position = new Vector3(0f, 0f, 2.5f);
                var view = mobHost.AddComponent<MobView>();
                view.Mob = Mob.Create(8, new Float3(0f, 0f, 2.5f)); // 鸡 4 血
                var fb = MobHitFeedback.Attach(mobHost, view.Mob);
                UnityEngine.Physics.SyncTransforms(); // EditMode 下 transform 不自动同步物理

                // 空手 1 伤 × 4 血：四次挥击杀鸡。冷却用 LastAttackTime 字段直改注入
                // （EditMode 下 Time.time 冻结，与 PlayerAttackTests 同款手法）
                for (int i = 0; i < 4; i++)
                {
                    combat.LastAttackTime = float.NegativeInfinity;
                    Assert.That(combat.TryAttack(), Is.True, $"第 {i + 1} 挥应命中（2.5m < 4m 射程）");
                }
                Assert.That(view.Mob.State, Is.EqualTo(MobState.Dying), "4 伤打死 4 血鸡");
                Assert.That(fb.IsShrinkComplete, Is.False, "致死一击应武装缩小动画（尚未走完）");
                Assert.That(ReadColor(mobHost.GetComponent<Renderer>()), Is.EqualTo(Color.red),
                    "命中瞬间 MPB tint 应已涂红");

                // 烧时间：闪红过期（>0.15s）、击退累计推开（0.5s 内烧掉大半冲量）、缩小走完
                float z0 = view.Mob.Position.Z;
                for (int i = 0; i < 30; i++) fb.TickFeedback(1f / 60f); // 0.5s

                Assert.That(view.Mob.Position.Z - z0, Is.GreaterThan(0.8f),
                    "0.5s 内应被沿远离玩家方向（+Z）推开（冲量总量 1.5m）");
                Assert.That(ReadColor(mobHost.GetComponent<Renderer>()), Is.Not.EqualTo(Color.red),
                    "0.5s 后闪红早已过期复原");
                Assert.That(mobHost.transform.localScale, Is.EqualTo(Vector3.zero),
                    "0.3s 缩小动画应已走完（scale 到 0）");
            }
            finally
            {
                CombatEvents.Reset();
                if (mobHost != null) Object.DestroyImmediate(mobHost);
                Object.DestroyImmediate(playerHost);
                Object.DestroyImmediate(ctxHost);
            }
        }
    }
}
#endif
