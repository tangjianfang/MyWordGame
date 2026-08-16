// Task D6: Villager 占位 mob kind。
// Villager 是中立、友好的「人形生物」placeholder——中立于玩家（不 flee、不 chase），
// 视觉上用棕色袍（Body + Head），与猪/牛/鸡/僵尸并列。
// 设计参照 D1-D5 的 Pig/Cow/Chicken/Zombie 路径，但 AI 走「原地不动」的最简分支。
// 整个文件用 #if UNITY_EDITOR 包裹视觉部分；Core 部分（kind/AI/JSON）走 dotnet 链。
using System.IO;
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.WorldGen;
using NUnit.Framework;
#if UNITY_EDITOR
using MyWorld.Unity.Combat;
using UnityEngine;
#endif

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// 验证 MobKind 增 Villager + MobAI 占位分支 + MobSpawnRules JSON 配置 +MobView 视觉。
    /// </summary>
    [TestFixture]
    public class MobKindVillagerTests
    {
        private static string SpawnRulesPath()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "mobs", "spawn_rules.json");
#else
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "mobs", "spawn_rules.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("未能找到 Assets/StreamingAssets/mobs/spawn_rules.json。");
#endif
        }

        private static Mob NewVillagerMob()
        {
            // mobTypeId=10 对应 Villager（D6 新增），由 Mob.Create 经过 MobSpawnRules.PickKind 路径被实例化。
            return new Mob
            {
                MobTypeId = 10,
                Kind = MobKind.Villager,
                Health = new Health(20),
                Position = new Float3(0, 64, 0),
                WanderCooldown = 2f,
                MoveSpeed = 0f,
            };
        }

        [Test]
        public void Villager_EnumExists_AndHasDistinctValue()
        {
            // Villager 必须存在且与旧 MobKind 值不同（避免与 Passive/Hostile/Neutral/Pig/Cow/Chicken/Zombie 冲突）。
            int v = (int)MobKind.Villager;
            Assert.That(v, Is.Not.EqualTo((int)MobKind.Passive));
            Assert.That(v, Is.Not.EqualTo((int)MobKind.Hostile));
            Assert.That(v, Is.Not.EqualTo((int)MobKind.Neutral));
            Assert.That(v, Is.Not.EqualTo((int)MobKind.Pig));
            Assert.That(v, Is.Not.EqualTo((int)MobKind.Cow));
            Assert.That(v, Is.Not.EqualTo((int)MobKind.Chicken));
            Assert.That(v, Is.Not.EqualTo((int)MobKind.Zombie));
        }

        [Test]
        public void Villager_StandStill_WhenPlayerClose()
        {
            // 玩家紧贴：Villager 不应进入 Scared（不会像 Pig 那样 flee）。
            var v = NewVillagerMob();
            MobAI.Tick(v, new Float3(0.5f, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(v.State, Is.EqualTo(MobState.Idle),
                "Villager 玩家靠近应保持 Idle，不 flee（中立友好）");
        }

        [Test]
        public void Villager_StandStill_AtNight()
        {
            // 夜晚：Villager 不应进入 Chasing（不会像 Zombie 那样追玩家）。
            var v = NewVillagerMob();
            var night = new TimeOfDay { CurrentTick = 15000 };
            MobAI.Tick(v, new Float3(0.5f, 64, 0), null, night, 0.1f);
            Assert.That(v.State, Is.EqualTo(MobState.Idle),
                "Villager 夜晚应保持 Idle，不 chase（中立友好）");
        }

        [Test]
        public void Villager_NeverChases_AcrossManyFrames()
        {
            // 反复 tick 各种距离：Villager 永远不应进入 Chasing。
            var v = NewVillagerMob();
            var night = new TimeOfDay { CurrentTick = 15000 };
            for (int seed = 0; seed < 30; seed++)
            {
                float distance = (seed % 5) + 0.5f;
                MobAI.Tick(v, new Float3(distance, 64, 0), null, night, 0.1f);
            }
            Assert.That(v.State, Is.Not.EqualTo(MobState.Chasing),
                "Villager 不应进入 Chasing 状态");
        }

        [Test]
        public void Villager_PositionStaysPut_WhenTicked()
        {
            // 反复 tick：Villager 位置应保持不变（无 wander、无 flee）。
            var v = NewVillagerMob();
            var initialPos = v.Position;
            for (int i = 0; i < 20; i++)
            {
                MobAI.Tick(v, new Float3(0.5f, 64, 0), null, new TimeOfDay(), 0.1f);
            }
            Assert.That(v.Position.X, Is.EqualTo(initialPos.X),
                "Villager X 位置应保持不变（stand still）");
            Assert.That(v.Position.Z, Is.EqualTo(initialPos.Z),
                "Villager Z 位置应保持不变（stand still）");
        }

        [Test]
        public void Villager_SpawnRules_AreLoadedForPlains()
        {
            // JSON 里应配置 Villager 在 Plains + minLight=9。
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            int spawns = 0;
            for (int i = 0; i < 100; i++)
            {
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Villager, lightLevel: 15, seed: i)) spawns++;
            }
            Assert.That(spawns, Is.GreaterThan(0),
                $"Villager Plains + 光 15 应能刷（实际 {spawns}/100）");
        }

        [Test]
        public void Villager_SpawnRules_RespectsMinLight()
        {
            // lightLevel=0 < MinLight=9：Villager 应被拒绝。
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            int blocked = 0;
            for (int i = 0; i < 50; i++)
            {
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Villager, lightLevel: 0, seed: i)) blocked++;
            }
            Assert.That(blocked, Is.EqualTo(0), "Villager 黑夜不应刷（MinLight=9）");
        }

        [Test]
        public void Villager_DropTable_ReturnsEmpty()
        {
            // Villager 占位：死亡不掉落（spec 范围内不引入新物品）。
            var drops = MyWorld.Core.Items.ItemDropTable.Drop(MobKind.Villager);
            Assert.That(drops, Is.Empty, "Villager 占位：死亡应不掉落");
        }

#if UNITY_EDITOR
        [Test]
        public void VillagerView_Setup_CreatesBodyAndHead()
        {
            // Villager 视觉：棕色袍（body + head），与 Zombie 同结构但颜色不同。
            var host = new GameObject("VillagerViewHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                var setup = typeof(MobView).GetMethod("Setup",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(setup, Is.Not.Null, "MobView 没有公共 Setup 方法");
                setup.Invoke(view, new object[] { MobKind.Villager });

                Assert.That(host.transform.Find("body"), Is.Not.Null, "Villager 应创建 Body 子物体");
                Assert.That(host.transform.Find("head"), Is.Not.Null, "Villager 应创建 Head 子物体");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void VillagerView_Setup_BodyHasBrownRobeColor()
        {
            // Villager 视觉：Body 颜色应偏棕色（袍），与 Zombie 草绿（0.4, 0.6, 0.4）区分。
            var host = new GameObject("VillagerViewColorHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                var setup = typeof(MobView).GetMethod("Setup",
                    BindingFlags.Instance | BindingFlags.Public);
                setup.Invoke(view, new object[] { MobKind.Villager });

                var body = host.transform.Find("body").GetComponent<Renderer>();
                var block = new MaterialPropertyBlock();
                body.GetPropertyBlock(block);
                var color = block.GetColor("_BaseColor");
                // 红 + 绿 + 蓝 = 棕色，应以红=绿 > 蓝 为特征（蓝 < 0.4 区分 Zombie 蓝=0.4）
                Assert.That(color.r, Is.GreaterThan(color.b),
                    $"Villager body 红色分量应 > 蓝色（棕色系），实际 r={color.r} b={color.b}");
                Assert.That(color.b, Is.LessThan(0.45f),
                    $"Villager body 蓝色分量应 < 0.45（与 Zombie 蓝=0.4 区分），实际 b={color.b}");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
#endif
    }
}
