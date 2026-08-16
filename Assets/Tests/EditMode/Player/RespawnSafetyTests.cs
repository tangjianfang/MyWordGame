#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// m7 A1：安全重生（死亡循环修复）。旧版复活点 = 死亡位置（DeathScreenUi 把
    /// LastDeathPosition 传回 Respawn），僵尸守尸时玩家原地复活 → 立刻再被围殴 →
    /// 14 秒死亡循环。新语义：
    /// <list type="number">
    /// <item>复活一律回 <see cref="PlayerController.Bind"/> 记录的世界出生点</item>
    /// <item>复活同时回满血 / 饥饿（沿用 Respawn 既有逻辑，不双恢复）</item>
    /// <item>复活后 <see cref="PlayerController.InvincibleSeconds"/> 秒无敌：
    ///     <see cref="PlayerController.TakeDamage"/> 在无敌期内整个忽略伤害</item>
    /// </list>
    /// </summary>
    public class RespawnSafetyTests
    {
        private GameObject _go;
        private PlayerContext _ctx;
        private PlayerController _player;

        /// <summary>测试用出生点：与 WorldBootstrap 的 SurfaceHeightAt+2 无关，
        /// 这里只验证「Bind 存下的位置就是复活位置」。构造 World 走绑定态分支
        /// （重建 _state 而非写未绑定的 fallback 字段），与实机路径一致。</summary>
        private static readonly Float3 Spawn = new Float3(10f, 70f, -5f);

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("玩家");
            _ctx = _go.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，显式初始化
            // （与 PlayerPickupDamageTests 一致）：Health 默认 Current=0，
            // 不初始化的话「死亡」前置就不成立。
            _ctx.Inventory = new PlayerInventory();
            _ctx.HungerSystem = new HungerSystem();
            _ctx.Health = new Health(20f);
            _player = _go.AddComponent<PlayerController>();
            _player.Bind(new World(), null, Spawn);
        }

        [TearDown]
        public void TearDown()
        {
            // CombatEvents 是 static event，EditMode 下 DestroyImmediate 不触发
            // MobManager.OnDisable 退订，不 Reset 会把订阅泄漏给后续测试。
            CombatEvents.Reset();
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void 复活回到出生点而非死亡位置()
        {
            // 玩家跑到远处死亡（模拟守尸场景：死亡点 != 出生点）
            _player.RestoreCoreState(PlayerState.AtRest(new Float3(50f, 80f, 50f)));
            _player.TakeDamage(999, null);
            Assert.That(_ctx.Health.IsDead, Is.True, "前置：玩家应已死亡");

            _player.RespawnAtSpawn();

            Assert.That(_go.transform.position,
                Is.EqualTo(new Vector3(Spawn.X, Spawn.Y, Spawn.Z)).Within(0.001f),
                "复活应传送回 Bind 记录的出生点 (10,70,-5)，而不是死亡位置 (50,80,50)");
            Assert.That(_player.State.Position.X, Is.EqualTo(Spawn.X).Within(0.001f),
                "Core 运动状态也应同步回出生点（否则下一帧 Tick 会把玩家拉回死亡点）");
            Assert.That(_player.State.Position.Z, Is.EqualTo(Spawn.Z).Within(0.001f),
                "Core 运动状态也应同步回出生点（否则下一帧 Tick 会把玩家拉回死亡点）");
        }

        [Test]
        public void 复活回满血与饥饿()
        {
            // 扣到残血 / 半饥饿再复活
            _ctx.Health.Damage(15f);
            _ctx.HungerSystem.Hunger = 3;
            _ctx.HungerSystem.Saturation = 0f;

            _player.RespawnAtSpawn();

            Assert.That(_ctx.Health.Current, Is.EqualTo(_ctx.Health.Max).Within(0.001f),
                "复活后血量应回满（PlayerContext.Health 是唯一真源）");
            Assert.That(_ctx.HungerSystem.Hunger, Is.EqualTo(HungerSystem.MaxHunger),
                "复活后饥饿应回满，不给守尸的饥饿伤害补刀机会");
        }

        [Test]
        public void 无敌帧期间伤害被忽略_过期后正常结算()
        {
            _player.RestoreCoreState(PlayerState.AtRest(new Float3(50f, 80f, 50f)));
            _player.TakeDamage(999, null);
            _player.RespawnAtSpawn();
            float hpAfterRespawn = _ctx.Health.Current;
            Assert.That(hpAfterRespawn, Is.EqualTo(_ctx.Health.Max).Within(0.001f),
                "前置：复活后应满血");

            // 无敌帧内：伤害必须整个被吞掉（不进 Health）
            // 注：本命名空间下 Time 会被解析成兄弟命名空间 MyWorld.Core.Tests.Time，
            // 必须 UnityEngine.Time 全限定。
            Assert.That(_player.InvincibleUntil, Is.GreaterThan(UnityEngine.Time.time),
                "复活应设置未来的无敌截止时刻");
            _player.TakeDamage(10, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(hpAfterRespawn).Within(0.001f),
                "3 秒无敌帧内 TakeDamage 应被忽略");

            // 无敌过期后（测试直接把截止时刻改到过去，避免真实等待 3 秒）
            _player.InvincibleUntil = UnityEngine.Time.time - 0.01f;
            _player.TakeDamage(10, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(hpAfterRespawn - 10f).Within(0.001f),
                "无敌帧过期后伤害应正常结算");
        }

        [Test]
        public void 无敌帧内怪物近战不掉血_结束后正常结算()
        {
            // 走真实事件链：僵尸近战 / 苦力怕爆炸都由 MobAI 发 CombatEvents.OnDamageTaken
            // （victim=0 即玩家），MobManager.HandleDamageTaken 订阅后结算。
            // EditMode 下生命周期不自动跑：PlayerContext.Instance 由 Awake 建立、
            // MobManager 的订阅在 OnEnable，都用反射显式触发（与 DeathScreenUiWireTests
            // 的 InvokeAwake 同法）。
            InvokeAwake(_ctx);
            var mgr = _go.AddComponent<MobManager>();
            mgr.Bind(null, null, _go.transform);
            InvokeOnEnable(mgr);

            _player.RespawnAtSpawn(); // 进入 3 秒无敌
            float hp = _ctx.Health.Current;

            CombatEvents.RaiseTaken(new DamageEvent(
                DamageSource.Melee, 5f, attacker: 7, victim: 0, default));
            Assert.That(_ctx.Health.Current, Is.EqualTo(hp).Within(0.001f),
                "无敌帧内僵尸近战（CombatEvents → MobManager 链路）不应掉血——"
                + "A1 要解决的核心威胁就是守尸僵尸，这条链路直写 Health 会绕过无敌帧");

            _player.InvincibleUntil = UnityEngine.Time.time - 0.01f;
            CombatEvents.RaiseTaken(new DamageEvent(
                DamageSource.Melee, 5f, attacker: 7, victim: 0, default));
            Assert.That(_ctx.Health.Current, Is.EqualTo(hp - 5f).Within(0.001f),
                "无敌结束后同一链路应正常掉血");
        }

        /// <summary>EditMode 下 AddComponent 不会触发 MonoBehaviour.OnEnable
        /// （订阅 CombatEvents 的入口在那里），用反射显式调用。</summary>
        private static void InvokeOnEnable(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 OnEnable");
            method.Invoke(mb, null);
        }

        /// <summary>EditMode 下 AddComponent 不会触发 MonoBehaviour.Awake
        /// （PlayerContext.Instance 单例在这里建立），用反射显式调用。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }
    }
}
#endif
