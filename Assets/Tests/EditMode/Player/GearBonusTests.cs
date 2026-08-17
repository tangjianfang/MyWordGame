using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// m10 C1：手持装备三属性（防御 / 移速 / 生命上限）的 Core 纯计算。
    /// 简化模型：只算选中物品一件——手持即生效、切走即失效（spec §3 不做穿戴栏）。
    /// Unity 侧（PlayerContext 刷新 / TakeDamage 减伤 / PlayerMotor 乘速度）只接线，
    /// 行为断言在 EditMode 的 GearBonusEquipTests。
    /// </summary>
    [TestFixture]
    public class GearBonusTests
    {
        // ─── 减伤：max(1, damage - Defense)，保底 1 伤不无敌 ────────────────────

        [Test]
        public void 减伤_防御点数从伤害中扣除()
        {
            Assert.That(GearBonusMath.MitigateDamage(5f, 1), Is.EqualTo(4f), "5 伤害 - 1 防御 = 4");
            Assert.That(GearBonusMath.MitigateDamage(10f, 3), Is.EqualTo(7f), "10 伤害 - 3 防御 = 7");
        }

        [Test]
        public void 减伤_下限一点_防御再高也不无敌()
        {
            Assert.That(GearBonusMath.MitigateDamage(2f, 5), Is.EqualTo(1f),
                "防御超过伤害时保底 1 伤——穿防御装不是无敌");
            Assert.That(GearBonusMath.MitigateDamage(0.5f, 1), Is.EqualTo(1f),
                "碎块 0.5 伤害撞上防御同样保底 1（max(1, damage - Defense) 的字面语义）");
        }

        [Test]
        public void 减伤_零防御时原样通过_小数伤害不被抬高()
        {
            // m10 B2 碎块扎脚是 0.5 伤害——防御为 0（没拿防御装备）时必须原样通过，
            // 否则「保底 1 伤」会把无装备玩家的碎块伤害从 0.5 抬到 1，破坏 B2 契约
            Assert.That(GearBonusMath.MitigateDamage(0.5f, 0), Is.EqualTo(0.5f),
                "零防御不做任何改写：下限只防「减穿到 0」，不是全场取整");
            Assert.That(GearBonusMath.MitigateDamage(7f, 0), Is.EqualTo(7f), "摔落 7 点伤害原样通过");
        }

        [Test]
        public void 减伤_非正输入原样返回()
        {
            // 调用方（TakeDamage）对 amount <= 0 早退，这里只是纯函数兜底：
            // 不能把 0 / 负数洗成 1 点伤害
            Assert.That(GearBonusMath.MitigateDamage(0f, 3), Is.EqualTo(0f));
            Assert.That(GearBonusMath.MitigateDamage(-2f, 3), Is.EqualTo(-2f));
        }

        // ─── 三属性计算：从物品定义读 gearBonus ────────────────────────────────

        [Test]
        public void 三属性_防御装备_只出防御()
        {
            var def = new ItemDefinition { Id = "iron_sword", GearStat = GearStat.Defense, GearAmount = 1f };
            var b = GearBonuses.FromDefinition(def);
            Assert.That(b.Defense, Is.EqualTo(1), "铁系装备 gearBonus = defense 1");
            Assert.That(b.MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(b.MaxHealthBonus, Is.EqualTo(0));
        }

        [Test]
        public void 三属性_移速装备_只出移速比例()
        {
            var def = new ItemDefinition { Id = "summer_alloy_sword", GearStat = GearStat.MoveSpeed, GearAmount = 0.05f };
            var b = GearBonuses.FromDefinition(def);
            Assert.That(b.Defense, Is.EqualTo(0));
            Assert.That(b.MoveSpeedBonus, Is.EqualTo(0.05f).Within(1e-6f), "合金系 gearBonus = moveSpeed 0.05（+5%）");
            Assert.That(b.MaxHealthBonus, Is.EqualTo(0));
        }

        [Test]
        public void 三属性_生命上限装备_只出上限点数()
        {
            var def = new ItemDefinition { Id = "machine_essence_sword", GearStat = GearStat.MaxHealth, GearAmount = 2f };
            var b = GearBonuses.FromDefinition(def);
            Assert.That(b.Defense, Is.EqualTo(0));
            Assert.That(b.MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(b.MaxHealthBonus, Is.EqualTo(2), "机元系 gearBonus = maxHealth 2");
        }

        [Test]
        public void 三属性_无加成物品或空手_全零()
        {
            Assert.That(GearBonuses.FromDefinition(null).Defense, Is.EqualTo(0), "空手（无选中物品）= 无加成");
            var plain = new ItemDefinition { Id = "plank" }; // 没写 gearBonus 的普通物品
            Assert.That(GearBonuses.FromDefinition(plain).MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(GearBonuses.FromDefinition(plain).MaxHealthBonus, Is.EqualTo(0));
            Assert.That(GearBonuses.None.Defense, Is.EqualTo(0), "None 默认值全零");
        }

        [Test]
        public void 三属性_负加成按零处理()
        {
            // JSON 加载层已拦负数 amount（ItemDatabase 抛），这里再兜一层：
            // 任何路径构造出的 GearBonuses 都不可能让玩家变弱
            var b = new GearBonuses(-5, -0.5f, -3);
            Assert.That(b.Defense, Is.EqualTo(0));
            Assert.That(b.MoveSpeedBonus, Is.EqualTo(0f));
            Assert.That(b.MaxHealthBonus, Is.EqualTo(0));
        }

        // ─── 生命上限叠加与钳制 ────────────────────────────────────────────────

        [Test]
        public void 有效血上限_基础加成()
        {
            Assert.That(GearBonusMath.EffectiveMaxHealth(20f, 0), Is.EqualTo(20f), "无加成 = 基础 20");
            Assert.That(GearBonusMath.EffectiveMaxHealth(20f, 2), Is.EqualTo(22f), "机元 +2 → 22");
        }

        [Test]
        public void 血量钳制_超上限收回_未超不动()
        {
            Assert.That(GearBonusMath.ClampCurrentToEffectiveMax(22f, 20f, 0), Is.EqualTo(20f),
                "切走生命上限装备：22 血当场收回 2 点（血量刷新处钳制）");
            Assert.That(GearBonusMath.ClampCurrentToEffectiveMax(21f, 20f, 2), Is.EqualTo(21f),
                "持有装备时 21 ≤ 22，不裁");
            Assert.That(GearBonusMath.ClampCurrentToEffectiveMax(19f, 20f, 2), Is.EqualTo(19f),
                "低于上限的血不动——钳制只收不加");
        }

        // ─── 移速乘算：PlayerMotor 目标速度 × (1 + MoveSpeedBonus) ─────────────

        private World _world;
        private WorldSolidSource _source;
        private PlayerMotorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _settings = PlayerMotorSettings.Default;
            for (var x = -16; x < 16; x++)
            for (var z = -16; z < 16; z++) _world.SetBlock(x, 63, z, BlockIds.Stone);
            _source = new WorldSolidSource(_world, BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true }"
            }));
        }

        private PlayerState Run(PlayerState state, PlayerInput input, int frames)
        { for (var i = 0; i < frames; i++) state = PlayerMotor.Step(_source, state, input, _settings, 1f / 60f); return state; }

        [Test]
        public void 移速加成_目标速度按比例上浮()
        {
            _settings.MoveSpeedBonus = 0.05f;
            PlayerState boosted = Run(new PlayerState(new Float3(0f, 64f, 0f), default, true),
                new PlayerInput(0f, 1f, false, false), 120);

            // 2 秒足够把水平速度收敛到目标值（GroundFriction=12，τ≈0.08s）
            Assert.That(boosted.Velocity.Z, Is.GreaterThan(_settings.WalkSpeed * 1.04f),
                "收敛后的水平速度应达到 WalkSpeed × 1.05");
            Assert.That(boosted.Velocity.Z, Is.LessThan(_settings.WalkSpeed * 1.06f),
                "加成是 5% 就该是 5%，不该更多");
        }

        [Test]
        public void 移速加成_同输入走得比无加成远()
        {
            PlayerState plain = Run(new PlayerState(new Float3(0f, 64f, 0f), default, true),
                new PlayerInput(0f, 1f, false, false), 60);
            _settings.MoveSpeedBonus = 0.05f;
            PlayerState boosted = Run(new PlayerState(new Float3(0f, 64f, 0f), default, true),
                new PlayerInput(0f, 1f, false, false), 60);

            Assert.That(boosted.Position.Z, Is.GreaterThan(plain.Position.Z),
                "手持合金剑走同样 1 秒应当更远（简化模型：手持即生效）");
        }

        [Test]
        public void 移速加成_默认零_行为与旧版一致()
        {
            PlayerState state = Run(new PlayerState(new Float3(0f, 64f, 0f), default, true),
                new PlayerInput(0f, 1f, false, false), 60);
            Assert.That(state.Position.Z, Is.GreaterThan(_settings.WalkSpeed * 0.9f),
                "未加成时按原 WalkSpeed 收敛（回归守卫：默认值不改变旧手感）");
        }
    }
}
