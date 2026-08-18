using System;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using NUnit.Framework;

// JSON 驱动掉落表 (Entities) vs 静态 Items.ItemDropTable：测试用 alias 区分
// （与 MobAITests 同款约定）。
using JsonDropTable = MyWorld.Core.Entities.MobDropTable;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// m9 A3：死亡统一序列（修打猎断环③）——<see cref="MobAI.TakeHit"/> 致死时由 MobAI
    /// 内联转 Dying + 写 LastDrops，玩家击杀不再依赖 Unity 侧直置 Dying
    /// （旧 CombatController.DoAttack 路径绕过 MobAI，LastDrops 永远不可达 → 打死不掉肉）。
    /// 同时覆盖 A2 评审 Minor 2：尸体（Dying/Dead）再受击整体短路——不闪红、不重掷掉落。
    /// </summary>
    [TestFixture]
    public class MobDeathSequenceTests
    {
        [SetUp]
        public void SetUp()
        {
            // 测试间不互相污染：默认 null 走 legacy Items.ItemDropTable.Drop
            MobAI.DropTable = null;
        }

        [TearDown]
        public void TearDown()
        {
            MobAI.DropTable = null;
            DifficultyMode.ResetCache(); // m13 W2：宝宝模式静态 bool 跨测试隔离（清回默认 false）
        }

        private static string DropTablesJsonPath()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "mobs", "drop_tables.json");
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "mobs", "drop_tables.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("未能找到 Assets/StreamingAssets/mobs/drop_tables.json。");
#endif
        }

        /// <summary>
        /// 致死一击应同步走完死亡序列：Dying + 默认倒计时 + LastDrops 写入。
        /// 这是「击杀路径统一经 MobAI」的 Core 侧契约——不依赖下一帧 Tick 兜底，
        /// Unity 侧打死瞬间即可拿到 LastDrops 去 spawn 真实掉落。
        /// </summary>
        [Test]
        public void TakeHit_LethalHit_TransitionsToDying_AndWritesLastDrops()
        {
            var pig = Mob.Create(6, new Float3(10, 64, 0));
            Assume.That(pig.LastDrops, Is.Null, "前置：活着时 LastDrops 为 null");

            bool killed = MobAI.TakeHit(pig, new Float3(0, 64, 0), 999f);

            Assert.That(killed, Is.True, "致死一击 TakeHit 应返回 true（击杀信号）");
            Assert.That(pig.Health.Current, Is.EqualTo(0f), "超杀伤害应钳在 0");
            Assert.That(pig.State, Is.EqualTo(MobState.Dying),
                "致死一击应由 MobAI 死亡序列转 Dying（Unity 侧不再直置）");
            Assert.That(pig.DeathTimer, Is.EqualTo(MobAI.DefaultDeathTimer),
                "Dying 倒计时应取默认值 0.5s");
            Assert.That(pig.LastDrops, Is.Not.Null,
                "打死瞬间 LastDrops 应已写入（修断环③：掉肉必经此处）");
            Assert.That(pig.LastDrops.Length, Is.GreaterThan(0), "猪死亡应掉物品");
            Assert.That(pig.LastDrops[0].ItemId, Is.EqualTo(ItemDropTable.PorkchopItemId),
                "legacy 静态表兜底应给猪 porkchop (1008)");
        }

        /// <summary>致死不逃跑：死亡优先于受击逃跑（A2 已有 IsDead 早退，A3 保持语义不回归）。</summary>
        [Test]
        public void TakeHit_LethalHit_DoesNotEnterFleeing()
        {
            var chicken = Mob.Create(8, new Float3(10, 64, 0));

            MobAI.TakeHit(chicken, new Float3(0, 64, 0), 999f);

            Assert.That(chicken.State, Is.EqualTo(MobState.Dying),
                "被动动物致死一击应直接进死亡序列");
            Assert.That(chicken.State, Is.Not.EqualTo(MobState.FleeingFromAttacker),
                "致死不逃——尸体不该有逃跑状态");
            Assert.That(chicken.FleeUntil, Is.EqualTo(0f),
                "致死不置逃跑计时");
        }

        /// <summary>
        /// A2 评审 Minor 2：尸体（Dying/Dead）再受击整体短路——不重掷掉落、不刷新红闪、
        /// 不重开死亡序列。掉落表只掷一次是「打死掉肉」入账的前提（否则一帧多击会重复掉）。
        /// </summary>
        [Test]
        public void TakeHit_CorpseIsIdempotent()
        {
            var chicken = Mob.Create(8, new Float3(10, 64, 0));
            MobAI.TakeHit(chicken, new Float3(0, 64, 0), 999f);
            Assume.That(chicken.State, Is.EqualTo(MobState.Dying), "前置：已打死");
            var firstDrops = chicken.LastDrops;
            chicken.HitFlashTimer = 0f; // 烧掉致死一击的红闪，观察尸体再击是否刷新它

            bool secondKill = MobAI.TakeHit(chicken, new Float3(0, 64, 0), 999f);

            Assert.That(secondKill, Is.False, "尸体受击不应再报「致死」（击杀信号只发一次）");
            Assert.That(chicken.State, Is.EqualTo(MobState.Dying), "尸体状态不应改变");
            Assert.That(chicken.HitFlashTimer, Is.EqualTo(0f),
                "尸体再击不应刷新红闪（不闪红）");
            Assert.That(chicken.LastDrops, Is.SameAs(firstDrops),
                "尸体再击不应重掷掉落表（LastDrops 引用不变 = 不重掉）");
        }

        /// <summary>
        /// 非致死受击返回 false 且不触发死亡序列；同一 mob 第二次（致死）才返回 true。
        /// </summary>
        [Test]
        public void TakeHit_ReturnsTrueOnlyOnKillingBlow()
        {
            var pig = Mob.Create(6, new Float3(10, 64, 0)); // 10 血

            bool first = MobAI.TakeHit(pig, new Float3(0, 64, 0), 4f);
            Assert.That(first, Is.False, "非致死受击应返回 false");
            Assert.That(pig.State, Is.EqualTo(MobState.FleeingFromAttacker),
                "非致死受击照常走受击逃跑");

            bool second = MobAI.TakeHit(pig, new Float3(0, 64, 0), 999f);
            Assert.That(second, Is.True, "致死一击应返回 true");
            Assert.That(pig.State, Is.EqualTo(MobState.Dying), "致死一击转 Dying");
        }

        /// <summary>
        /// 死因标记：只有致死一击置 <see cref="Mob.KilledByPlayer"/>（Unity 侧 MobManager
        /// 据此入账击杀经验）；普通受击不动它。
        /// </summary>
        [Test]
        public void TakeHit_LethalHit_MarksKilledByPlayer()
        {
            var pig = Mob.Create(6, new Float3(10, 64, 0));

            MobAI.TakeHit(pig, new Float3(0, 64, 0), 4f);
            Assert.That(pig.KilledByPlayer, Is.False, "未击杀不应置死因标记");

            MobAI.TakeHit(pig, new Float3(0, 64, 0), 999f);
            Assert.That(pig.KilledByPlayer, Is.True, "致死一击应置死因标记（经验入账依据）");
        }

        /// <summary>
        /// 注入 JSON 驱动掉落表后，TakeHit 死亡序列应经 <see cref="JsonDropTable.RollAll"/>
        /// 掷真实掉落（鸡 100% 掉 1 个 chicken=1017）——与 MobAI.Tick 的 IsDead 兜底同一张表。
        /// </summary>
        [Test]
        public void TakeHit_LethalHit_UsesJsonDropTable_WhenInjected()
        {
            MobAI.DropTable = JsonDropTable.Load(DropTablesJsonPath());

            var chicken = Mob.Create(8, new Float3(3, 64, 7));
            MobAI.TakeHit(chicken, new Float3(0, 64, 0), 999f);

            Assert.That(chicken.LastDrops, Is.Not.Null, "JSON 表路径也应写 LastDrops");
            Assert.That(chicken.LastDrops.Length, Is.EqualTo(1),
                $"鸡死亡应只有 1 个掉落 stack（实际 {chicken.LastDrops.Length}）");
            Assert.That(chicken.LastDrops[0].ItemId, Is.EqualTo(ItemDropTable.ChickenItemId),
                "鸡应掉 chicken (1017)——真实 drop_tables 链路");
            Assert.That(chicken.LastDrops[0].Count, Is.EqualTo(1),
                "鸡的 countMin=countMax=1（drop_tables.json 当前值）");
        }
    }
}
