#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Persistence;
using MyWorld.Unity.Player;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-4 B3：TryRestore 启动恢复 + seed 校验 + 坏档降级。
    /// （EditMode-only：MonoBehaviour + PlayerContext。）
    /// </summary>
    [TestFixture]
    public class SaveLoadServiceRestoreTests
    {
        private string _saveRoot;
        private readonly List<GameObject> _gos = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, $"restore-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _gos) Object.DestroyImmediate(go);
            _gos.Clear();
            Directory.Delete(_saveRoot, true);
        }

        /// <summary>建一棵完整的树（PlayerContext + PlayerController + SaveLoadService）。
        /// 每次 new 一个 GameObject：RoundTrip 测试要在同一测试里建两棵树，
        /// 分开建避免 PlayerContext 单例把第二棵树 Destroy 掉。</summary>
        private (PlayerContext ctx, PlayerController player, SaveLoadService svc) BuildTree(long seed)
        {
            var go = new GameObject();
            _gos.Add(go);
            // 显式赋值：PlayerContext 是单例，若上一个测试的实例尚未真正销毁，
            // Awake 会走 Destroy(this) 分支而不初始化各系统（同 SaveLoadServiceSaveTests）。
            var ctx = go.AddComponent<PlayerContext>();
            ctx.Inventory = new PlayerInventory();
            ctx.Health = new Health(20f);
            ctx.Time = new TimeOfDay();
            ctx.HungerSystem = new HungerSystem();
            ctx.Experience = new Experience();
            ctx.FurnaceSystem = new FurnaceSystem(8, 30f);
            var player = go.AddComponent<PlayerController>();
            var svc = go.AddComponent<SaveLoadService>();
            svc.Bind(new World(), ctx, player, seed, _saveRoot);
            return (ctx, player, svc);
        }

        [Test]
        public void TryRestore_全量状态往返恢复()
        {
            // 存：背包 1 件 + 熔炉进度/燃料 + 掉落物 1 个 + TimeTick 7000 + 生命/饥饿/经验
            var (ctxA, playerA, svcA) = BuildTree(42);
            ctxA.Inventory.SetSlot(0, new ItemStack(1000, 5));
            ctxA.Time.CurrentTick = 7000f;
            ctxA.Health = new Health(20f) { Current = 13.5f };
            ctxA.HungerSystem.Hunger = 17;
            ctxA.Experience = new Experience(30, 2);
            ctxA.FurnaceSystem.Restore(new ItemStack(1004, 3), null, null, 17.3f, 12.5f);
            ctxA.ItemDrops.Add(new ItemDropEntity(new ItemStack(1008, 2), new Float3(5f, 71f, 6f)));
            playerA.RestoreCoreState(PlayerState.AtRest(new Float3(12.5f, 70f, -3.5f)));
            // m5 C3：注入同步执行器，SaveNow 后文件立刻存在（m4 断言语义不变）
            svcA.WriteExecutor = a => a();
            svcA.SaveNow();

            // 恢复：新的树，同一 saveRoot、同一 seed
            var (ctxB, playerB, svcB) = BuildTree(42);
            Assert.That(svcB.TryRestore(), Is.True, "同 seed 的好档应恢复成功");

            Assert.That(ctxB.Time.CurrentTick, Is.EqualTo(7000f), "世界时间应接续");
            Assert.That(ctxB.Inventory.GetSlot(0).Count, Is.EqualTo(5), "背包应恢复");
            Assert.That(ctxB.Inventory.GetSlot(0).ItemId, Is.EqualTo(1000), "背包物品应恢复");
            Assert.That(ctxB.Health.Current, Is.EqualTo(13.5f), "生命值应恢复");
            Assert.That(ctxB.HungerSystem.Hunger, Is.EqualTo(17), "饥饿值应恢复");
            Assert.That(ctxB.Experience.Current, Is.EqualTo(30), "经验当前值应恢复");
            Assert.That(ctxB.Experience.Level, Is.EqualTo(2), "经验等级应恢复");
            Assert.That(ctxB.FurnaceSystem.Progress, Is.EqualTo(17.3f), "熔炉烧炼进度应接续");
            Assert.That(ctxB.FurnaceSystem.FuelRemaining, Is.EqualTo(12.5f), "剩余燃料应接续");
            Assert.That(ctxB.FurnaceSystem.Input.Value.Count, Is.EqualTo(3), "熔炉输入槽应恢复");
            Assert.That(ctxB.ItemDrops.Count, Is.EqualTo(1), "掉落物应恢复");
            Assert.That(ctxB.ItemDrops[0].Content.Value.ItemId, Is.EqualTo(1008), "掉落物内容应恢复");
            // 这里必须写全限定 UnityEngine.Time：当前命名空间 MyWorld.Core.Tests.* 下，
            // 裸的 Time 会先解析到 MyWorld.Core.Time 命名空间
            Assert.That(ctxB.ItemDrops[0].SpawnTime, Is.EqualTo(UnityEngine.Time.time),
                "掉落物 SpawnTime 应重计为当前 Time.time（宽限期重新计时）");
            Assert.That(playerB.State.Position.X, Is.EqualTo(12.5f), "玩家 Core 状态应恢复");
            Assert.That(playerB.transform.position.x, Is.EqualTo(12.5f), "transform 应同步恢复后的位置");
        }

        [Test]
        public void TryRestore_seed不符整档忽略()
        {
            var (_, _, svcA) = BuildTree(42);
            svcA.WriteExecutor = a => a(); // m5 C3：注入同步执行器，保存后立刻可拷档
            svcA.SaveNow(); // 档落在 <saveRoot>/42/level.dat，档里 Seed=42
            string savedPath = svcA.LevelDataPath;

            var (ctxB, _, svcB) = BuildTree(43); // 当前 seed=43，档目录是 <saveRoot>/43/（另一处）
            // 必须把 42 的档拷进 43 目录：否则 43 下无 level.dat，TryRestore 会在
            // 「文件不存在」分支早退，seed 比较分支永远不执行（m4 终审 Important 1 修复）
            string mismatchPath = svcB.LevelDataPath;
            Directory.CreateDirectory(Path.GetDirectoryName(mismatchPath));
            File.Copy(savedPath, mismatchPath);
            Assert.That(File.Exists(mismatchPath), Is.True, "前置：43 目录下应有拷贝来的 level.dat");

            Assert.That(svcB.TryRestore(), Is.False, "seed 不符应整档忽略");
            Assert.That(ctxB.Inventory.GetSlot(0).IsEmpty, Is.True, "背包保持全新");
            Assert.That(ctxB.Time.CurrentTick, Is.EqualTo(new TimeOfDay().CurrentTick), "时间保持默认");
            Assert.That(File.Exists(savedPath), Is.True, "seed 不符只忽略、不动 42 目录的原档文件");
            Assert.That(File.Exists(savedPath + ".corrupt"), Is.False, "42 原档不应产生 .corrupt");
            Assert.That(File.Exists(mismatchPath), Is.True, "seed 不符只忽略、不重命名 43 目录下的档文件");
            Assert.That(File.Exists(mismatchPath + ".corrupt"), Is.False,
                "seed 不符不是坏档，不应触发 .corrupt 降级");
        }

        [Test]
        public void TryRestore_坏档重命名corrupt并全新开始()
        {
            var (ctxB, _, svcB) = BuildTree(42);
            Directory.CreateDirectory(Path.GetDirectoryName(svcB.LevelDataPath));
            File.WriteAllText(svcB.LevelDataPath, "坏档");

            Assert.That(svcB.TryRestore(), Is.False, "坏档应返回 false 而不是抛异常");
            Assert.That(File.Exists(svcB.LevelDataPath + ".corrupt"), Is.True, "坏档应重命名 .corrupt 留案");
            Assert.That(File.Exists(svcB.LevelDataPath), Is.False, "原路径不应再留坏档");
            Assert.That(ctxB.Time.CurrentTick, Is.EqualTo(new TimeOfDay().CurrentTick),
                "时间保持默认——全新开始");

            // 第二次坏档：.corrupt 已存在，应先删旧的再改名顶替
            File.WriteAllText(svcB.LevelDataPath, "还是坏档");
            Assert.That(svcB.TryRestore(), Is.False, "第二次坏档同样应降级");
            Assert.That(File.Exists(svcB.LevelDataPath + ".corrupt"), Is.True, "旧 .corrupt 应被顶替");
        }
    }
}
#endif
