// m11 W3-3：机元守卫 Boss——建档参数 / 三招状态机 / 掉落链 / 召唤检测 / 不自然刷。
// 纯 Core（零 UnityEngine），dotnet 与 EditMode 双链都跑；Unity 侧的模型守卫在
// Visual/MobModelBossTests、右键召唤路由在 Player/BossSummonInteractionTests（均 UNITY_EDITOR 包裹）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Entities;
using MyWorld.Core.Enchanting;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class MachineGuardianTests
    {
        private readonly List<DamageEvent> _taken = new List<DamageEvent>();
        private readonly List<(Mob Mob, int Index)> _summons = new List<(Mob, int)>();

        [SetUp]
        public void SetUp()
        {
            _taken.Clear();
            _summons.Clear();
            CombatEvents.OnDamageTaken += OnTaken;
            MobAI.OnBossSummon += OnSummon;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.OnDamageTaken -= OnTaken;
            CombatEvents.Reset();
            MobAI.OnBossSummon -= OnSummon;
            MobAI.DropTable = null; // 本 fixture 不注入真表（掉落链直接调 RollAll），还场防外泄
            DifficultyMode.ResetCache(); // m13 W2：宝宝模式静态 bool 跨测试隔离（清回默认 false）
        }

        private void OnTaken(DamageEvent ev) => _taken.Add(ev);

        private void OnSummon(Mob mob, int index) => _summons.Add((mob, index));

        private static Mob NewBoss(int entityId = 1)
        {
            var boss = Mob.Create((int)MobKind.MachineGuardian, new Float3(0f, 64f, 0f));
            boss.EntityId = entityId;
            return boss;
        }

        private static readonly TimeOfDay Noon = new TimeOfDay { CurrentTick = 6000 };

        // ─── 建档 ───────────────────────────────────────────────────────────

        [Test]
        public void MobKind_MachineGuardian_枚举值固定27()
        {
            Assert.That((int)MobKind.MachineGuardian, Is.EqualTo(27),
                "枚举值固定 27（15-26 是第 1 波 12 生物，27 起 Boss——存档 spawn 数据按值序列化，不得改动）");
        }

        [Test]
        public void Mob_Create27_Boss建档_血60伤6速35追击20()
        {
            var boss = Mob.Create(27, new Float3(3f, 64f, 3f));
            Assert.That(boss.Kind, Is.EqualTo(MobKind.MachineGuardian), "typeId 27 建档为 Boss kind");
            Assert.That(boss.Health.Max, Is.EqualTo(60f), "Boss 血 60（卡片数值）");
            Assert.That(boss.Health.Current, Is.EqualTo(60f), "出生满血");
            Assert.That(boss.AttackDamage, Is.EqualTo(6f), "近身冲撞伤 6");
            Assert.That(boss.MoveSpeed, Is.EqualTo(3.5f), "常速 3.5（与僵尸同档）");
            Assert.That(boss.AttackRange, Is.EqualTo(4f), "近身攻击距离 4");
            Assert.That(boss.ChaseRadius, Is.EqualTo(20f), "追击半径 20（与僵尸同档）");
            Assert.That(boss.BossSummoned, Is.False, "半血召唤未发");
            Assert.That(boss.BossChargeTimer, Is.EqualTo(0f), "不在冲撞中");
        }

        // ─── 三招状态机 ─────────────────────────────────────────────────────

        [Test]
        public void BossAI_贴脸近身_伤6且进2秒冷却()
        {
            // 3m < AttackRange 4：震荡波冷却中（隔离近身分支）→ 只发近身
            var boss = NewBoss();
            boss.BossShockwaveCooldown = 99f;
            MobAI.Tick(boss, new Float3(3f, 64f, 0f), null, Noon, 0.1f);

            Assert.That(_taken.Count, Is.EqualTo(1), "贴脸应发一次攻击");
            Assert.That(_taken[0].Amount, Is.EqualTo(6f), "近身冲撞伤 6");
            Assert.That(boss.AttackCooldown, Is.EqualTo(MobAI.BossMeleeCooldown),
                "近身冷却 2s（伤 6 的重击不能连发）");
        }

        [Test]
        public void BossAI_六米震荡波_伤3且进冷却_原地脉冲()
        {
            // 5m ∈ (4, 6]：冲撞冷却中 + 近身冷却中（隔离震荡波分支）→ 只发震荡波
            var boss = NewBoss();
            boss.BossChargeCooldown = 99f;
            boss.AttackCooldown = 99f;
            MobAI.Tick(boss, new Float3(5f, 64f, 0f), null, Noon, 0.1f);

            Assert.That(_taken.Count, Is.EqualTo(1), "6m 内应发震荡波");
            Assert.That(_taken[0].Amount, Is.EqualTo(MobAI.BossShockwaveDamage),
                $"震荡波 AOE 伤 {MobAI.BossShockwaveDamage}");
            Assert.That(boss.BossShockwaveCooldown, Is.EqualTo(MobAI.BossShockwaveCooldown),
                "震荡波进 6s 冷却");
            Assert.That(boss.Velocity.X, Is.EqualTo(0f), "震荡波原地点发（本帧不走位）");
        }

        [Test]
        public void BossAI_震荡波半径外_不伤玩家()
        {
            // 6.5m > BossShockwaveRadius 6，且 < AttackRange 的近身分支也不满足 → 无伤
            var boss = NewBoss();
            boss.BossChargeCooldown = 99f; // 冲撞也压掉，纯验证震荡波半径
            MobAI.Tick(boss, new Float3(6.5f, 64f, 0f), null, Noon, 0.1f);

            Assert.That(_taken.Count, Is.EqualTo(0), "震荡波半径外不该有伤害事件");
        }

        [Test]
        public void BossAI_冲撞起手_开窗口且速度翻倍()
        {
            // 7m ∈ (4, 8]：震荡波冷却中 → 冲撞起手。起手置 0.6s，同一 tick 的
            // 移动段已扣 dt=0.1 → 观察 0.5
            var boss = NewBoss();
            boss.BossShockwaveCooldown = 99f;
            MobAI.Tick(boss, new Float3(7f, 64f, 0f), null, Noon, 0.1f);

            Assert.That(boss.BossChargeTimer, Is.EqualTo(MobAI.BossChargeDuration - 0.1f).Within(0.0001f),
                "冲撞窗口开启（起手 0.6s，本帧已烧 0.1s）");
            Assert.That(boss.BossChargeCooldown, Is.EqualTo(MobAI.BossChargeCooldown), "冲撞进 4s 冷却");
            Assert.That(boss.Velocity.X, Is.EqualTo(MobAI.BossChargeSpeed),
                $"冲撞窗口内速度 = {MobAI.BossChargeSpeed}（常速的两倍）");
        }

        [Test]
        public void BossAI_多招重叠带_确定性哈希变招_同输入同序列()
        {
            // 5m 处震荡波与冲撞同时可用 → 哈希二选一。两条不变量：
            // ① 同一 Boss（同 EntityId）重放同序列；② 扫过一批 EntityId 两种招都出现
            int shock = 0, charge = 0;
            for (int id = 1; id <= 30; id++)
            {
                var boss = NewBoss(id);
                MobAI.Tick(boss, new Float3(5f, 64f, 0f), null, Noon, 0.1f);
                if (boss.BossShockwaveCooldown > 0f) shock++;
                else if (boss.BossChargeTimer > 0f) charge++;
                else Assert.Fail($"EntityId={id} 在重叠带两招都没发（变招分支漏发）");
            }
            Assert.That(shock, Is.GreaterThan(0), "30 个 EntityId 里应出现震荡波（哈希均匀）");
            Assert.That(charge, Is.GreaterThan(0), "30 个 EntityId 里应出现冲撞（哈希均匀）");

            // 重放一致：同 EntityId 两个全新 Boss，第一招必然相同
            for (int id = 1; id <= 10; id++)
            {
                Assert.That(FirstMoveIsShockwave(NewBoss(id)), Is.EqualTo(FirstMoveIsShockwave(NewBoss(id))),
                    $"EntityId={id} 重放第一招必须一致（确定性哈希，replay-safe）");
            }
        }

        private static bool FirstMoveIsShockwave(Mob boss)
        {
            MobAI.Tick(boss, new Float3(5f, 64f, 0f), null, Noon, 0.1f);
            return boss.BossShockwaveCooldown > 0f;
        }

        [Test]
        public void BossAI_半血召唤_恰两只骷髅且终身一次()
        {
            var boss = NewBoss();
            MobAI.Tick(boss, new Float3(50f, 64f, 0f), null, Noon, 0.1f); // 追击半径外：满血不召
            Assert.That(_summons.Count, Is.EqualTo(0), "满血不召唤");

            boss.Health.Damage(31f); // 60 → 29（≤ 半血线 30）
            MobAI.Tick(boss, new Float3(50f, 64f, 0f), null, Noon, 0.1f);
            Assert.That(_summons.Count, Is.EqualTo(MobAI.BossSummonCount),
                $"半血应一次性召唤 {MobAI.BossSummonCount} 只（每只一条事件，序号 0 起）");
            Assert.That(_summons[0].Index, Is.EqualTo(0), "第一只序号 0");
            Assert.That(_summons[1].Index, Is.EqualTo(1), "第二只序号 1");

            MobAI.Tick(boss, new Float3(50f, 64f, 0f), null, Noon, 0.1f);
            Assert.That(_summons.Count, Is.EqualTo(MobAI.BossSummonCount), "召唤终身一次，不再重发");
        }

        [Test]
        public void BossAI_半血线恰三十_边界判定含等号()
        {
            var boss = NewBoss();
            boss.Health.Damage(30f); // 恰 60 → 30：Current*2 = 60 = Max → 召唤
            MobAI.Tick(boss, new Float3(50f, 64f, 0f), null, Noon, 0.1f);
            Assert.That(_summons.Count, Is.EqualTo(MobAI.BossSummonCount),
                "恰半血（30/60）也在召唤线内（×2 比较避免浮点半血歧义）");
        }

        [Test]
        public void BossAI_不看昼夜_白天照打()
        {
            // isNight=false：自然敌对组白天回落 wander，Boss 是召唤对手局——白天照打
            var boss = NewBoss();
            boss.BossShockwaveCooldown = 99f;
            MobAI.Tick(boss, new Float3(3f, 64f, 0f), null, Noon, 0.1f, isNight: false);
            Assert.That(_taken.Count, Is.EqualTo(1), "Boss 白天（isNight=false）照样攻击");
            Assert.That(boss.State, Is.EqualTo(MobState.Chasing), "Boss 白天保持交战状态");
        }

        [Test]
        public void BossAI_追击半径外站定不动()
        {
            var boss = NewBoss();
            MobAI.Tick(boss, new Float3(50f, 64f, 0f), null, Noon, 0.1f); // 50 > ChaseRadius 20
            Assert.That(boss.State, Is.EqualTo(MobState.Idle), "追击半径外 Idle");
            Assert.That(boss.Velocity.X, Is.EqualTo(0f), "站定零速度");
        }

        [Test]
        public void Boss受击_不逃_敌对语义()
        {
            var boss = NewBoss();
            MobAI.TakeHit(boss, new Float3(0f, 64f, 0f), 5f);
            Assert.That(boss.State, Is.Not.EqualTo(MobState.FleeingFromAttacker),
                "Boss 受击不逃（敌对组语义）");
            Assert.That(boss.Health.Current, Is.EqualTo(55f), "受击照扣血");
        }

        // ─── 掉落链（真 drop_tables.json + 真 items 表） ─────────────────────

        [Test]
        public void 真掉落表_Boss掉netherite锭2至3加带编码附魔书1()
        {
            MobDropTable table = MobDropTable.Load(RealPath("mobs", "drop_tables.json"));
            var items = MyWorld.Core.Items.ItemDatabase.FromJson(DirectoryFiles("items"));
            int netherite = items.GetById("netherite_ingot").NumericId;
            int book = items.GetById("enchanted_book").NumericId;

            for (int seed = 0; seed < 50; seed++)
            {
                ItemStack[] drops = table.RollAll(MobKind.MachineGuardian, seed);
                ItemStack ingot = FindByItem(drops, netherite);
                ItemStack bookStack = FindByItem(drops, book);
                Assert.That(ingot.IsEmpty, Is.False, $"seed={seed}：netherite_ingot 必掉（chance=1.0）");
                Assert.That(ingot.Count, Is.InRange(2, 3),
                    $"seed={seed}：netherite_ingot 应掉 2-3（实际 {ingot.Count}）");
                Assert.That(bookStack.IsEmpty, Is.False, $"seed={seed}：附魔书必掉 ×1（chance=1.0）");
                Assert.That(bookStack.Count, Is.EqualTo(1), $"seed={seed}：附魔书恰 1 本");
                Assert.That(bookStack.Metadata, Is.EqualTo(25),
                    $"seed={seed}：书带编码 25（EncodeBook(锋利, 3)）");

                // 编码可解码回锋利 III（融合路径直接用编码，不掷骰）
                Assert.That(EnchantSystem.TryDecodeBook(bookStack.Metadata, out var kind, out int level), Is.True,
                    "编码 25 应是合法书编码");
                Assert.That(kind, Is.EqualTo(EnchantmentType.Sharpness), "Boss 书 = 锋利");
                Assert.That(level, Is.EqualTo(3), "Boss 书 = III 级（高级书只有 Boss 掉）");
            }
        }

        private static ItemStack FindByItem(ItemStack[] drops, int itemId)
        {
            foreach (var stack in drops)
            {
                if (stack.ItemId == itemId) return stack;
            }
            return ItemStack.Empty;
        }

        // ─── 不自然刷（spawn_rules 无条目） ─────────────────────────────────

        [Test]
        public void 真刷新规则_守卫不自然刷_全群系全光照零命中()
        {
            var rules = MobSpawnRules.Load(RealPath("mobs", "spawn_rules.json"));
            foreach (MyWorld.Core.WorldGen.Biome biome in System.Enum.GetValues(typeof(MyWorld.Core.WorldGen.Biome)))
            {
                foreach (int light in new[] { 0, 9, 15 })
                {
                    for (int seed = 0; seed < 100; seed++)
                    {
                        Assert.That(rules.ShouldSpawn(biome, MobKind.MachineGuardian, light, seed), Is.False,
                            $"Boss 不该自然刷：{biome} light={light} seed={seed}（spawn_rules 刻意无条目，只经图腾召唤）");
                    }
                }
            }
        }

        // ─── 图腾检测（Core 纯函数） ────────────────────────────────────────

        [Test]
        public void 图腾检测_2x2机元矿石_命中任一角都识别_锚点取最小角()
        {
            var world = new World();
            PlaceTotem(world, 8, 10, 8);

            // 命中 2×2 的四个角各自都能识别，锚点一律是 (8,10,8)
            foreach (var (dx, dz) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                bool ok = MachineGuardianSummon.TryDetectTotem(world, 8 + dx, 10, 8 + dz,
                    out int ax, out int ay, out int az, out Float3 spawn);
                Assert.That(ok, Is.True, $"命中角 ({8 + dx},10,{8 + dz}) 应识别出图腾");
                Assert.That(ax, Is.EqualTo(8), "锚点 x = 2×2 最小角");
                Assert.That(ay, Is.EqualTo(10), "锚点 y = 图腾所在层");
                Assert.That(az, Is.EqualTo(8), "锚点 z = 2×2 最小角");
                Assert.That(spawn.X, Is.EqualTo(9f), "Boss 落点 x = 图腾中心");
                Assert.That(spawn.Y, Is.EqualTo(11f), "Boss 脚底 = 图腾顶面上方一格");
                Assert.That(spawn.Z, Is.EqualTo(9f), "Boss 落点 z = 图腾中心");
            }
        }

        [Test]
        public void 图腾检测_缺角不成图腾_y超16无效_非矿石直接拒()
        {
            var broken = new World();
            PlaceTotem(broken, 8, 10, 8);
            broken.SetBlock(9, 10, 9, BlockIds.Stone); // 拆掉一角
            Assert.That(MachineGuardianSummon.TryDetectTotem(broken, 8, 10, 8, out _, out _, out _, out _), Is.False,
                "四角缺一不叫图腾");

            var shallow = new World();
            PlaceTotem(shallow, 8, 16, 8);
            Assert.That(MachineGuardianSummon.TryDetectTotem(shallow, 8, 16, 8, out _, out _, out _, out _), Is.False,
                "y=16 超门槛（机元矿生成层 y<16，表层凑图腾无效）");
            Assert.That(MachineGuardianSummon.TryDetectTotem(shallow, 8, 15, 8, out _, out _, out _, out _), Is.False,
                "y=15 处没有矿石（只摆了 y=16 一层），命中不是机元矿石直接拒");

            Assert.That(MachineGuardianSummon.TryDetectTotem(new World(), 0, 0, 0, out _, out _, out _, out _), Is.False,
                "空世界的命中格不是矿石 → false");
        }

        [Test]
        public void 图腾键_锚点逗号拼串_FarmStates同款风格()
        {
            Assert.That(MachineGuardianSummon.TotemKey(8, 10, 8), Is.EqualTo("8,10,8"),
                "键 = \"x,y,z\"（与 FarmStates/ChestContents 键风格一致）");
        }

        [Test]
        public void 图腾登记_MarkUsed防重_ExportImport全量往返_坏行容忍()
        {
            var state = new BossSummonState();
            Assert.That(state.IsUsed("8,10,8"), Is.False, "未登记不算已用");

            state.MarkUsed("8,10,8");
            Assert.That(state.IsUsed("8,10,8"), Is.True, "登记后已用");
            Assert.That(state.Count, Is.EqualTo(1));

            List<string> exported = state.Export();
            Assert.That(exported, Is.EqualTo(new[] { "8,10,8" }), "导出可回读");

            var restored = new BossSummonState();
            restored.Import(exported);
            Assert.That(restored.IsUsed("8,10,8"), Is.True, "往返后仍已用");

            restored.Import(null);
            Assert.That(restored.Count, Is.EqualTo(0), "null（旧档无字段）= 清空全新开始");

            restored.Import(new[] { "1,2,3", null, "", "4,5,6" });
            Assert.That(restored.Count, Is.EqualTo(2), "坏行（null/空串）跳过不炸");
        }

        [Test]
        public void 存档编解码_UsedBossTotems往返_旧档缺键归一空列表()
        {
            string path = Path.GetTempFileName();
            try
            {
                LevelDataCodec.Save(new LevelData
                {
                    Seed = 7,
                    UsedBossTotems = new List<string> { "8,10,8", "-4,3,9" },
                }, path);
                LevelData loaded = LevelDataCodec.Load(path);
                Assert.That(loaded.UsedBossTotems, Is.EqualTo(new[] { "8,10,8", "-4,3,9" }),
                    "图腾登记往返一致（负坐标键原样保留）");
            }
            finally
            {
                File.Delete(path);
            }

            // 旧档（m11 W3-3 之前的 level.dat）没有该键 → Newtonsoft 留 null → Codec 归一空列表
            string legacy = Path.GetTempFileName();
            try
            {
                File.WriteAllText(legacy, "{ \"Seed\": 7 }");
                LevelData old = LevelDataCodec.Load(legacy);
                Assert.That(old.UsedBossTotems, Is.Not.Null, "缺键归一为空列表（不 NPE）");
                Assert.That(old.UsedBossTotems.Count, Is.EqualTo(0), "旧档 = 无已用图腾");
            }
            finally
            {
                File.Delete(legacy);
            }
        }

        private static void PlaceTotem(World world, int ax, int y, int az)
        {
            for (int dx = 0; dx < 2; dx++)
            {
                for (int dz = 0; dz < 2; dz++)
                {
                    world.SetBlock(ax + dx, y, az + dz, BlockIds.MachineEssenceOre);
                }
            }
        }

        /// <summary>双链目录定位：dotnet 从测试输出目录向上爬，EditMode 用 streamingAssetsPath
        ///（MobKindWiringTests.RealSpawnRulesPath 同款）。</summary>
        private static string RealPath(params string[] parts)
        {
#if UNITY_EDITOR
            string root = UnityEngine.Application.streamingAssetsPath;
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            string root = null;
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets");
                if (Directory.Exists(candidate))
                {
                    root = candidate;
                    break;
                }
                directory = directory.Parent;
            }
            Assert.That(root, Is.Not.Null, "未能从测试输出目录向上找到 Assets/StreamingAssets");
#endif
            return Path.Combine(root, Path.Combine(parts));
        }

        private static IEnumerable<string> DirectoryFiles(string subdir)
        {
#if UNITY_EDITOR
            string dir = Path.Combine(UnityEngine.Application.streamingAssetsPath, subdir);
#else
            string dir = RealPath(subdir);
#endif
            // 读内容不是路径（GearUpgradeRecipeTests 同款）
            return Directory.GetFiles(dir, "*.json").Select(File.ReadAllText);
        }
    }
}
