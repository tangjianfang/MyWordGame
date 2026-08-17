#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Persistence;
using MyWorld.Unity.Player;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-4 C1：全链路 save/restore round-trip（本里程碑核心验收测试）。
    /// 一条链路同时打穿 A1-A4 + B1-B3：
    /// 方块改动走 region 层（A1/A3 + B1 overlay），
    /// 玩家/熔炉/掉落物/世界时间走 level.dat 层（A2/A4 + B2 SaveNow + B3 TryRestore）。
    /// m6 C4 追加：任务链进度（QuestState）进 level.dat 的 round-trip + 旧档兼容。
    /// （EditMode-only：MonoBehaviour + PlayerContext 单例。）
    /// </summary>
    [TestFixture]
    public class SaveLoadEndToEndTests
    {
        private string _saveRoot;
        private readonly System.Collections.Generic.List<GameObject> _gos =
            new System.Collections.Generic.List<GameObject>();
        /// <summary>WriteChapter 产生的任务链临时文件，TearDown 统一清理。</summary>
        private readonly System.Collections.Generic.List<string> _questFiles =
            new System.Collections.Generic.List<string>();

        /// <summary>测试任务链：q1 拾取原木 → q2 合成木板（需求 4，测累计中停态）→ q3 过夜。</summary>
        private const string QuestChainJson = @"[
            { ""id"": ""q1"", ""name"": ""挖一根原木"", ""desc"": ""..."",
              ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 1 }, ""rewardExp"": 5 },
            { ""id"": ""q2"", ""name"": ""合成木板"", ""desc"": ""..."",
              ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 },
            { ""id"": ""q3"", ""name"": ""活过一夜"", ""desc"": ""..."",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 10 } ]";

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, $"e2e-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _gos) Object.DestroyImmediate(go);
            _gos.Clear();
            foreach (string path in _questFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            _questFiles.Clear();
            Directory.Delete(_saveRoot, true);
        }

        /// <summary>建一棵完整的树（PlayerContext + PlayerController + SaveLoadService）。
        /// 每棵树独立 GameObject：PlayerContext 是单例，两棵树共存时第二棵的 Awake
        /// 会走 Destroy(this) 分支而不初始化各系统（同 SaveLoadServiceRestoreTests）。</summary>
        private (PlayerContext ctx, PlayerController player, SaveLoadService svc) BuildTree()
        {
            var go = new GameObject();
            _gos.Add(go);
            var ctx = go.AddComponent<PlayerContext>();
            ctx.Inventory = new PlayerInventory();
            ctx.Health = new Health(20f);
            ctx.Time = new TimeOfDay();
            ctx.HungerSystem = new HungerSystem();
            ctx.Experience = new Experience();
            ctx.FurnaceSystem = new FurnaceSystem(8, 30f);
            var player = go.AddComponent<PlayerController>();
            var svc = go.AddComponent<SaveLoadService>();
            return (ctx, player, svc);
        }

        [Test]
        public void FullRoundTrip_EverythingSurvives()
        {
            var generator = new WorldGenerator(42);
            var pos = new ChunkPos(0, 0);

            // 找挖掘点的策略：不写死 y=70——地形高度随 seed 变化，硬编码可能恰好挖在空气里
            // 让"挖掉"这一步失去意义。改为生成后在 (8, y, 8) 自上而下扫，
            // 取第一个非空气非水的方块（水不算"挖掉方块"）。
            int digY = -1;

            // === 世界 A：挖一个、放一个、塞背包、熔炉烧一半、扔 2 个掉落物 ===
            var worldA = new World();
            worldA.AddChunk(pos, generator.Generate(pos));
            for (int y = 319; y >= -64; y--)
            {
                ushort block = worldA.GetBlock(8, y, 8);
                if (block != BlockIds.Air && block != BlockIds.Water)
                {
                    digY = y;
                    break;
                }
            }
            Assert.That(digY, Is.GreaterThan(0), "前置条件：(8, y, 8) 处应存在非空气非水方块可挖");

            ushort beforeDig = worldA.GetBlock(8, digY, 8);
            Assert.That(beforeDig, Is.Not.EqualTo(BlockIds.Air), "前置条件：挖掘点原本是实心方块");
            // 放置点取地表上方一格：若该点天然已有方块（未来 seed/生成器变化），
            // "放下的还在"断言会空通过，所以先断言前置为空气。
            Assert.That(worldA.GetBlock(10, digY + 1, 8), Is.EqualTo(BlockIds.Air),
                "前置条件：放置点原本应是空气（否则「放下的还在」断言无效）");
            worldA.SetBlock(8, digY, 8, BlockIds.Air);                        // 挖
            worldA.SetBlock(10, digY + 1, 8, BlockIds.Bedrock);               // 放（原空气处放基岩）
            ushort untouchedA = worldA.GetBlock(12, digY, 12);

            var (ctxA, playerA, svcA) = BuildTree();
            svcA.Bind(worldA, ctxA, playerA, 42, _saveRoot);
            ctxA.Inventory.SetSlot(0, new ItemStack(1000, 12));           // 快捷栏普通物品
            ctxA.Inventory.SetSlot(1, new ItemStack(1006, 1, 0x0503));    // 带耐久 Metadata 的工具
            ctxA.Inventory.SetSlot(9, new ItemStack(1004, 3));            // 背包区（第 10 槽起）
            ctxA.Health.Current = 13.5f;
            ctxA.Time.CurrentTick = 9000f;
            ctxA.FurnaceSystem.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 3));
            ctxA.FurnaceSystem.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 2));   // 煤是唯一合法燃料（m6 C2 起真实 id 1007）
            // 烧到一半：进度 17.3s、剩余燃料 45.5s。注意 AddFuel 只累计 _fuelRemaining、
            // 不写 Fuel 槽（Fuel 槽由 Restore 专管，见 SnapshotMappersTests 的注释），所以燃料槽在这里显式给。
            ctxA.FurnaceSystem.Restore(
                ctxA.FurnaceSystem.Input, new ItemStack(FurnaceSystem.CoalItemId, 2), null, 17.3f, 45.5f);
            ctxA.ItemDrops.Add(new ItemDropEntity(new ItemStack(1008, 2), new Float3(5f, 71f, 6f)));
            ctxA.ItemDrops.Add(new ItemDropEntity(new ItemStack(1010, 1), new Float3(6f, 71f, 7f)));
            playerA.RestoreCoreState(PlayerState.AtRest(new Float3(8.5f, digY + 2, 8.5f)));
            // m5 C3：注入同步执行器，SaveNow 后 region/level.dat 立刻可读（m4 断言语义不变）
            svcA.WriteExecutor = a => a();
            svcA.SaveNow();

            // === 模拟重启：销毁整棵树 → 全新 World 同 seed 生成 + region overlay + 全新树恢复 ===
            Object.DestroyImmediate(_gos[0]);
            _gos.RemoveAt(0);

            var worldB = new World();
            worldB.AddChunk(pos, generator.Generate(pos));
            Assert.That(RegionSaveCoordinator.TryLoadChunk(worldB, pos, Path.Combine(_saveRoot, "42", "regions")),
                Is.True, "区块 overlay 应命中：脏区块已随 SaveNow 落进 region 文件");

            var (ctxB, playerB, svcB) = BuildTree();
            svcB.Bind(worldB, ctxB, playerB, 42, _saveRoot);
            Assert.That(svcB.TryRestore(), Is.True, "同 seed 好档应恢复成功");

            // === 断言：region 层（方块改动）===
            Assert.That(worldB.GetBlock(8, digY, 8), Is.EqualTo(BlockIds.Air), "挖掉的方块应该还是空气");
            Assert.That(worldB.GetBlock(10, digY + 1, 8), Is.EqualTo(BlockIds.Bedrock), "放下的方块应该还在");
            Assert.That(worldB.GetBlock(12, digY, 12), Is.EqualTo(untouchedA),
                "没动过的方块应与 seed 生成一致（overlay 不污染未改动位置）");

            // === 断言：level.dat 层（玩家/熔炉/掉落物/时间）===
            Assert.That(ctxB.Inventory.GetSlot(0).ItemId, Is.EqualTo(1000), "快捷栏物品 id 在");
            Assert.That(ctxB.Inventory.GetSlot(0).Count, Is.EqualTo(12), "快捷栏物品在");
            Assert.That(ctxB.Inventory.GetSlot(1).Metadata, Is.EqualTo((ushort)0x0503), "工具耐久 Metadata 无损");
            Assert.That(ctxB.Inventory.GetSlot(9).Count, Is.EqualTo(3), "背包区物品在");
            Assert.That(ctxB.Health.Current, Is.EqualTo(13.5f), "生命值接续");
            Assert.That(playerB.State.Position.X, Is.EqualTo(8.5f), "玩家 Core 位置接续");
            Assert.That(playerB.transform.position.y, Is.EqualTo(digY + 2), "transform 应同步恢复后的位置");
            Assert.That(ctxB.FurnaceSystem.Progress, Is.EqualTo(17.3f), "熔炉进度接续");
            Assert.That(ctxB.FurnaceSystem.FuelRemaining, Is.EqualTo(45.5f), "熔炉剩余燃料接续（读档后火不灭）");
            Assert.That(ctxB.FurnaceSystem.Fuel.Value.Count, Is.EqualTo(2), "熔炉燃料槽在");
            Assert.That(ctxB.FurnaceSystem.Input.Value.Count, Is.EqualTo(3), "熔炉输入槽在");
            Assert.That(ctxB.ItemDrops.Count, Is.EqualTo(2), "两个掉落物都在");
            Assert.That(ctxB.ItemDrops[0].Content.Value.ItemId, Is.EqualTo(1008), "第一个掉落物内容正确");
            Assert.That(ctxB.ItemDrops[1].Content.Value.Count, Is.EqualTo(1), "第二个掉落物数量正确");
            Assert.That(ctxB.ItemDrops[0].Position, Is.EqualTo(new Float3(5f, 71f, 6f)), "掉落物位置接续");
            // 裸 Time 会先解析到 MyWorld.Core.Time 命名空间，必须写全限定 UnityEngine.Time
            Assert.That(ctxB.ItemDrops[0].SpawnTime, Is.EqualTo(UnityEngine.Time.time),
                "掉落物宽限期应重计（SpawnTime=恢复时刻）");
            Assert.That(ctxB.Time.CurrentTick, Is.EqualTo(9000f), "时间接续");
        }

        /// <summary>把任务链 JSON 落成临时文件并返回路径（模拟 chapter1.json，TearDown 清理）。</summary>
        private string WriteChapter()
        {
            return WriteChapter(QuestChainJson);
        }

        /// <summary>把指定章节 JSON 落成临时文件（m11 W2-4 起多章节测试用第二章内容）。</summary>
        private string WriteChapter(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            _questFiles.Add(path);
            return path;
        }

        /// <summary>给树挂任务总线并绑定**全新加载**的任务链（挂 PlayerContext 同物体，与
        /// WorldBootstrap 装配一致）。每次调用各自 LoadChapter——模拟「重启后从章节文件
        /// 重建 QuestSystem」；总线 Bind 同时把静态 Instance 交接给自己（C2 约定）。</summary>
        private QuestEventBus AttachQuestBus(PlayerContext ctx)
        {
            var bus = ctx.gameObject.AddComponent<QuestEventBus>();
            bus.Bind(ctx, QuestSystem.LoadChapter(WriteChapter()));
            return bus;
        }

        // ─── m11 W2-4：多章节任务书进度进 level.dat ───────────────────────

        [Test]
        public void QuestChaptersRoundTrip_两章进度各自接续()
        {
            // 树 A：第一章（q1/q2/q3）全链完成 + 切到第二章后停在 s2 累计 1/2
            var (ctxA, playerA, svcA) = BuildTree();
            svcA.Bind(new World(), ctxA, playerA, 42, _saveRoot);
            var busA = ctxA.gameObject.AddComponent<QuestEventBus>();
            busA.Bind(ctxA, QuestCampaign.Load(
                WriteChapter(), WriteChapter(ChapterTwoJson)));
            busA.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 });
            busA.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 });
            busA.Raise(new QuestEvent { Type = QuestEventType.SurviveNight }); // 第一章走完 → 切第二章
            Assert.That(busA.Quests.Current.Id, Is.EqualTo("s1"), "前置：第二章首任务已解锁");
            busA.Raise(new QuestEvent { Type = QuestEventType.SleepInBed }); // s1 完成 → 停在 s2
            busA.Raise(new QuestEvent { Type = QuestEventType.FeedAnimal, Kind = MobKind.Sheep, Count = 1 });
            Assert.That(busA.Quests.CurrentProgress, Is.EqualTo(1), "前置：s2 累计 1/2");

            svcA.WriteExecutor = a => a();
            svcA.SaveNow();

            // 落盘侧：两章快照都写进了 QuestChapters，Quest 字段带活动章（旧版兼容）
            LevelData saved = LevelDataCodec.Load(svcA.LevelDataPath);
            Assert.That(saved.QuestChapters, Is.Not.Null, "QuestChapters 应入档");
            Assert.That(saved.QuestChapters.Count, Is.EqualTo(2), "每章一个快照");
            Assert.That(saved.QuestChapters[0].CompletedCount, Is.EqualTo(3), "第一章全链完成");
            Assert.That(saved.QuestChapters[1].CurrentQuestId, Is.EqualTo("s2"), "第二章停在 s2");
            Assert.That(saved.Quest.CurrentQuestId, Is.EqualTo("s2"), "Quest 字段 = 当前活动章快照");

            // 模拟重启：拆树 A → 全新树 B + 全新两章任务书
            Object.DestroyImmediate(_gos[_gos.Count - 1]);
            _gos.RemoveAt(_gos.Count - 1);
            var (ctxB, playerB, svcB) = BuildTree();
            svcB.Bind(new World(), ctxB, playerB, 42, _saveRoot);
            var busB = ctxB.gameObject.AddComponent<QuestEventBus>();
            busB.Bind(ctxB, QuestCampaign.Load(WriteChapter(), WriteChapter(ChapterTwoJson)));
            Assert.That(svcB.TryRestore(), Is.True, "同 seed 好档应恢复成功");

            Assert.That(busB.ActiveChapterIndex, Is.EqualTo(1), "第一章完成态接续：活动章仍是第二章");
            Assert.That(busB.Quests.Current.Id, Is.EqualTo("s2"), "第二章进度接续");
            Assert.That(busB.Quests.CurrentProgress, Is.EqualTo(1), "s2 的累计分子接续（1/2）");
            Assert.That(busB.Quests.CompletedCount, Is.EqualTo(1), "第二章完成计数接续（s1）");

            busB.Raise(new QuestEvent { Type = QuestEventType.FeedAnimal, Kind = MobKind.Sheep, Count = 1 });
            Assert.That(busB.Quests.Current.Id, Is.EqualTo("s3"),
                "恢复后 1+1 凑满 2/2 应完成 s2（多章节进度真的接得上）");
        }

        [Test]
        public void LegacySave_只有Quest字段_第一章进度接续_第二章全新()
        {
            // 手写 m6→m11 过渡期的旧档：只有单章 Quest 字段（第二章字段的形态不存在）
            var (ctx, player, svc) = BuildTree();
            svc.Bind(new World(), ctx, player, 42, _saveRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(svc.LevelDataPath));
            File.WriteAllText(svc.LevelDataPath, @"{
                ""Seed"": 42, ""TimeTick"": 6000.0,
                ""Quest"": { ""CurrentQuestId"": ""q2"", ""CompletedCount"": 1, ""Progress"": 2 } }");
            var bus = ctx.gameObject.AddComponent<QuestEventBus>();
            bus.Bind(ctx, QuestCampaign.Load(WriteChapter(), WriteChapter(ChapterTwoJson)));

            Assert.That(svc.TryRestore(), Is.True, "旧档其余字段合法，整档应照常恢复");

            Assert.That(bus.Quests.Current.Id, Is.EqualTo("q2"), "旧档单章字段恢复进第一章（接续 q2）");
            Assert.That(bus.Quests.CurrentProgress, Is.EqualTo(2), "进度分子接续");
            Assert.That(bus.ActiveChapterIndex, Is.EqualTo(0), "第一章进行中：第二章未解锁");
            Assert.That(bus.Campaign.Chapters[1].Current.Id, Is.EqualTo("s1"), "旧档没有第二章字段 = 全新开始");
        }

        [Test]
        public void LegacySave_旧档第一章已完成_读档直接解锁第二章()
        {
            var (ctx, player, svc) = BuildTree();
            svc.Bind(new World(), ctx, player, 42, _saveRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(svc.LevelDataPath));
            // 第一章 q1/q2/q3 全链完成的旧档（CurrentQuestId 空 + CompletedCount = 链长 3）
            File.WriteAllText(svc.LevelDataPath, @"{
                ""Seed"": 42, ""TimeTick"": 6000.0,
                ""Quest"": { ""CurrentQuestId"": null, ""CompletedCount"": 3, ""Progress"": 0 } }");
            var bus = ctx.gameObject.AddComponent<QuestEventBus>();
            bus.Bind(ctx, QuestCampaign.Load(WriteChapter(), WriteChapter(ChapterTwoJson)));

            Assert.That(svc.TryRestore(), Is.True);

            Assert.That(bus.ActiveChapterIndex, Is.EqualTo(1), "第一章已完成：读档直接落在第二章");
            Assert.That(bus.Quests.Current.Id, Is.EqualTo("s1"), "第二章首任务就位");
            Assert.That(bus.Quests.CompletedCount, Is.EqualTo(0), "第二章全新开始");
        }

        /// <summary>第二章测试链：s1 睡觉 → s2 喂两只羊（累计中停态）→ s3 弓杀骷髅。</summary>
        private const string ChapterTwoJson = @"[
            { ""id"": ""s1"", ""name"": ""在床上睡到天亮"", ""desc"": ""..."",
              ""condition"": { ""type"": ""SleepInBed"" }, ""rewardExp"": 10 },
            { ""id"": ""s2"", ""name"": ""喂两只羊"", ""desc"": ""..."",
              ""condition"": { ""type"": ""FeedAnimal"", ""kind"": ""Sheep"", ""count"": 2 }, ""rewardExp"": 15 },
            { ""id"": ""s3"", ""name"": ""用弓击败骷髅"", ""desc"": ""..."",
              ""condition"": { ""type"": ""KillKind"", ""kind"": ""Skeleton"", ""weapon"": ""bow"", ""count"": 1 }, ""rewardExp"": 30 } ]";

        // ─── m6 C4：任务进度进 level.dat ─────────────────────────────────

        [Test]
        public void QuestRoundTrip_推进到q2累计2_存读后接续()
        {
            // 树 A：q1 完成解锁 q2，q2 累计 2/4 停在中途（CraftItem 累计口径，单笔 2 不够 4）
            var (ctxA, playerA, svcA) = BuildTree();
            svcA.Bind(new World(), ctxA, playerA, 42, _saveRoot);
            QuestEventBus busA = AttachQuestBus(ctxA);
            busA.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 });
            Assert.That(busA.Quests.Current.Id, Is.EqualTo("q2"), "前置：q1 应已完成解锁 q2");
            busA.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 2 });
            Assert.That(busA.Quests.CurrentProgress, Is.EqualTo(2), "前置：q2 进度应累计到 2（未达 4）");

            svcA.WriteExecutor = a => a(); // 同步落盘，SaveNow 返回即可读档
            svcA.SaveNow();

            // 落盘侧：Quest 快照确实写进了 level.dat（收集不是只有内存里对）
            LevelData saved = LevelDataCodec.Load(svcA.LevelDataPath);
            Assert.That(saved.Quest, Is.Not.Null, "SaveNow 应把总线 QuestSystem 的状态收集进 level.dat");
            Assert.That(saved.Quest.CurrentQuestId, Is.EqualTo("q2"), "当前任务 id 应入档");
            Assert.That(saved.Quest.CompletedCount, Is.EqualTo(1), "完成计数应入档");
            Assert.That(saved.Quest.Progress, Is.EqualTo(2), "q2 的累计进度分子应入档");

            // 模拟重启：拆树 A（OnDestroy 顺带清静态 Instance）→ 全新树 B + 全新任务链
            Object.DestroyImmediate(_gos[_gos.Count - 1]);
            _gos.RemoveAt(_gos.Count - 1);

            var (ctxB, playerB, svcB) = BuildTree();
            svcB.Bind(new World(), ctxB, playerB, 42, _saveRoot);
            QuestEventBus busB = AttachQuestBus(ctxB); // 先绑总线再 TryRestore（与 WorldBootstrap 顺序一致）
            Assert.That(svcB.TryRestore(), Is.True, "同 seed 好档应恢复成功");

            Assert.That(busB.Quests.Current.Id, Is.EqualTo("q2"), "任务进度应接续到 q2（不是从头 q1）");
            Assert.That(busB.Quests.CompletedCount, Is.EqualTo(1), "完成计数应接续");
            Assert.That(busB.Quests.CurrentProgress, Is.EqualTo(2), "q2 的累计进度分子应接续");

            // 接续的进度是「真的」：再合成 2 个 → 恢复的 2 + 新事件 2 = 4 恰好完成 q2 → 切 q3
            busB.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 2 });
            Assert.That(busB.Quests.Current.Id, Is.EqualTo("q3"),
                "恢复后的累计 2 加新事件 2 应凑满 q2 的需求 4（进度接续而非重置）");
        }

        [Test]
        public void LegacySave_无Quest字段_恢复任务链全新开始不炸()
        {
            // 手写 m4 时代的 level.dat：没有 Quest 字段（Newtonsoft 反序列化得 null）
            var (ctx, player, svc) = BuildTree();
            svc.Bind(new World(), ctx, player, 42, _saveRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(svc.LevelDataPath));
            File.WriteAllText(svc.LevelDataPath, @"{ ""Seed"": 42, ""TimeTick"": 6000.0 }");
            QuestEventBus bus = AttachQuestBus(ctx);

            bool restored = false;
            Assert.DoesNotThrow(() => restored = svc.TryRestore(), "旧档缺 Quest 字段不应抛异常");
            Assert.That(restored, Is.True, "旧档其余字段合法，整档应照常恢复");
            Assert.That(ctx.Time.CurrentTick, Is.EqualTo(6000f), "其余层照常接续（时间）");

            Assert.That(bus.Quests.Current.Id, Is.EqualTo("q1"), "任务链应全新开始（停在 q1）");
            Assert.That(bus.Quests.CompletedCount, Is.EqualTo(0), "完成计数保持 0");
            Assert.That(bus.Quests.CurrentProgress, Is.EqualTo(0), "进度分子保持 0");
        }
    }
}
#endif
