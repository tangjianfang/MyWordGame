#if UNITY_EDITOR
// m6 C2：QuestEventBus——游戏事件 → QuestSystem 的转发总线 + 挖/拾/合/烧/夜五事件源接线。
// 依赖 UnityEngine（MonoBehaviour / GameObject），#if UNITY_EDITOR 包裹只跑 EditMode 链；
// dotnet 链的纯逻辑（条件判定 / 链推进）由 C1 的 QuestSystemTests 覆盖。
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Quests
{
    /// <summary>
    /// 覆盖四组行为：
    /// 1) <see cref="QuestEventBus.Raise"/> 转发到 QuestSystem，完成时触发
    ///    <see cref="QuestEventBus.OnQuestCompleted"/> 钩子并经 PlayerContext.Experience 入账经验；
    /// 2) 拾取路径（含挖矿 spawn 的掉落物）发 ObtainItem 且 Count=「背包现存量」、不双计；
    /// 3) 口袋合成取产出 / 熔炉取产出分别发 CraftItem / SmeltItem；
    /// 4) TimeOfDay 跨过 NightEndTick（日出）的那一帧发 SurviveNight，日间推进 / 回绕帧不发。
    /// </summary>
    [TestFixture]
    public class QuestEventBusTests
    {
        private static readonly List<string> TempFiles = new List<string>();

        private GameObject _host;
        private PlayerContext _ctx;
        private PlayerController _player;
        private QuestEventBus _bus;

        /// <summary>默认测试链：q1 拾取原木 → q2 合成木板。</summary>
        private const string DefaultChain = @"[
            { ""id"": ""q1"", ""name"": ""挖一根原木"", ""desc"": ""..."",
              ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 1 }, ""rewardExp"": 5 },
            { ""id"": ""q2"", ""name"": ""合成木板"", ""desc"": ""..."",
              ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 } ]";

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("QuestBusHost");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx); // EditMode 下 AddComponent 不触发 Awake，手动补一次让单例就位
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _ctx.Time = new TimeOfDay();
            _ctx.Experience = new Experience();
            _player = _host.AddComponent<PlayerController>();
            _bus = _host.AddComponent<QuestEventBus>();
            _bus.Bind(_ctx, QuestSystem.LoadChapter(WriteChapter(DefaultChain)));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            foreach (string path in TempFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            TempFiles.Clear();
        }

        private static string WriteChapter(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            TempFiles.Add(path);
            return path;
        }

        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        // ─── 1) Raise 转发 / 完成钩子 / 经验入账 ───────────────────────────────

        [Test]
        public void Raise_命中条件_任务推进_无订阅者也不炸()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 });

            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "命中条件的事件应完成当前任务");
            Assert.That(_bus.Quests.Current.Id, Is.EqualTo("q2"), "完成后解锁下一个任务");
        }

        [Test]
        public void 任务完成_触发钩子并按RewardExp入账经验_含升级()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 120 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));

            Quest completed = null;
            _bus.OnQuestCompleted += q => completed = q;
            _bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight });

            Assert.That(completed, Is.Not.Null, "完成瞬间必须触发 OnQuestCompleted 钩子（C3 HUD 消费）");
            Assert.That(completed.Id, Is.EqualTo("q1"), "钩子参数应是刚完成的任务");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(20), "120 经验入账：升 1 级（100/级）后剩 20");
            Assert.That(_ctx.Experience.Level, Is.EqualTo(1), "经验溢出应升级");
        }

        [Test]
        public void 条件不满足_不触发钩子不加经验()
        {
            Quest completed = null;
            _bus.OnQuestCompleted += q => completed = q;
            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 });

            Assert.That(completed, Is.Null, "q1 未完成，q2 的事件不能触发钩子");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(0), "无完成就无经验");
        }

        [Test]
        public void 未绑定任务系统_Raise为安全noOp()
        {
            // 强转消歧：Bind(PlayerContext, QuestCampaign) 与旧重载 Bind(PlayerContext, QuestSystem)
            // 对 null 二义（CS0121），m11 W2-4 双重载并存后 null 必须显式指定走任务书重载
            _bus.Bind(_ctx, (QuestCampaign)null); // 链文件缺失 / 加载失败时的形态

            Assert.DoesNotThrow(() => _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }));
            Assert.That(_bus.Quests, Is.Null);
        }

        // ─── 2) 拾取 → ObtainItem（背包现存量口径 + 防双计） ───────────────────

        [Test]
        public void 拾取掉落物_发ObtainItem且Count为背包现存量()
        {
            // 任务要求「背包有 5 根原木」：预置 2 + 拾取 3 = 现存量 5 才应完成。
            // 若 Count 误用「本次拾取量 3」则完不成——这条断言锁死现存量口径。
            string path = WriteChapter(@"[
                { ""id"": ""q5"", ""name"": ""五根原木"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 5 }, ""rewardExp"": 5 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));
            _ctx.Inventory.SetSlot(0, new ItemStack(1000, 2));

            _host.transform.position = Vector3.zero;
            _ctx.ItemDrops.Add(new ItemDropEntity(new ItemStack(1000, 3), new Float3(0.5f, 0f, 0f)));
            // m7 B1 吸附语义：0.5m 在吸附半径内但 > 0.3m 完成距离，逐步步进到入包
            int picked = 0, steps = 0;
            while (_ctx.ItemDrops.Count > 0 && steps < 600)
            {
                picked += _player.PickupNearbyDrops(1f / 60f);
                steps++;
            }

            Assert.That(picked, Is.EqualTo(3), "掉落物应照常进包（事件接线不改变拾取行为）");
            Assert.That(_ctx.Inventory.CountOf(1000), Is.EqualTo(5));
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "现存量 5 ≥ 要求 5，任务应经拾取事件完成");
        }

        [Test]
        public void 挖矿掉落拾取_只发一次ObtainItem不双计()
        {
            // 链上两个任务条件相同：若挖（BreakAt）和拾（PickupNearbyDrops）都发事件，
            // 挖 1 块石头会连跳 2 个任务——本测试锁死「只在进包时发一次」。
            string path = WriteChapter(@"[
                { ""id"": ""m1"", ""name"": ""挖圆石一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1003, ""count"": 1 }, ""rewardExp"": 5 },
                { ""id"": ""m2"", ""name"": ""挖圆石二"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1003, ""count"": 2 }, ""rewardExp"": 5 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));

            var world = new World();
            world.SetBlock(8, 70, 8, BlockIds.Stone);
            var registry = BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
            });
            var items = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""cobblestone"", ""numericId"": 1003, ""texture"": ""cobblestone"" }",
            });
            var interactor = new GameObject("Interactor");
            try
            {
                var block = interactor.AddComponent<BlockInteraction>();
                block.Bind(world, registry, null, interactor.transform);
                block.SetBlockDrops(BlockDrops.FromJson(new[]
                {
                    @"{ ""blockId"": ""stone"", ""blockNumericId"": 1,
                        ""drops"": [ { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 1 } ] }",
                }, items));

                _host.transform.position = new Vector3(8.5f, 70.5f, 8.5f); // 站到掉落点旁，拾取半径内
                block.BreakAt(8, 70, 8);

                Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0),
                    "挖的瞬间掉落物只是 spawn，还没进包——不应发 ObtainItem");

                // EditMode 的 Time.time 是编辑器累计时间（非 0），spawn 即拾会撞上 0.5s 宽限期；
                // SpawnTime 归零 = 跳过宽限检查（ItemDropEntity 对 Core 单元测试的既定约定）
                foreach (var d in _ctx.ItemDrops)
                {
                    d.SpawnTime = 0f;
                }

                _player.PickupNearbyDrops();

                Assert.That(_ctx.Inventory.CountOf(1003), Is.EqualTo(1));
                Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1),
                    "进包后发一次事件：完成 m1；m2 要求现存量 2，不应被同一事件顺带完成（防双计）");
                Assert.That(_bus.Quests.Current.Id, Is.EqualTo("m2"));
            }
            finally
            {
                Object.DestroyImmediate(interactor);
            }
        }

        [Test]
        public void 场景无总线_拾取不抛异常()
        {
            Object.DestroyImmediate(_bus); // 早期场景 / 纯逻辑场景：没有总线也能玩

            _host.transform.position = Vector3.zero;
            _ctx.ItemDrops.Add(new ItemDropEntity(new ItemStack(1000, 1), new Float3(0.5f, 0f, 0f)));
            // m7 B1 吸附语义：逐步步进到入包（0.5m → 贴脸 <0.3m）
            int picked = 0, steps = 0;
            while (_ctx.ItemDrops.Count > 0 && steps < 600)
            {
                picked += _player.PickupNearbyDrops(1f / 60f);
                steps++;
            }

            Assert.That(picked, Is.EqualTo(1), "无总线时拾取行为不受影响");
            Assert.That(_ctx.Inventory.CountOf(1000), Is.EqualTo(1));
        }

        // ─── 3) 合成 → CraftItem / 熔炉 → SmeltItem ───────────────────────────

        [Test]
        public void 口袋合成取产出_发CraftItem()
        {
            // 链式解锁：把 CraftItem 放在链首，避免先要完成 ObtainItem 才轮到它
            string path = WriteChapter(@"[
                { ""id"": ""c2"", ""name"": ""合成木板"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));
            _ctx.Items = ItemDatabaseLoader.Load();
            _ctx.Recipes = ItemDatabaseLoader.LoadRecipes(_ctx.Items); // log_to_planks：1 原木 → 4 木板
            var ui = _host.AddComponent<CraftingPocketUi>();
            ui.SetInputForTest(new ItemStack(1000, 1));

            bool taken = ui.TryTakeCraftOutput();

            Assert.That(taken, Is.True, "有匹配配方时应取到产出");
            Assert.That(_ctx.Inventory.CountOf(1001), Is.EqualTo(4), "4 块木板应进包");
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "CraftItem(plank,4) 应完成当前任务");
            Assert.That(_bus.Quests.Current, Is.Null, "链走完 Current 为 null");
        }

        [Test]
        public void 熔炉取产出_发SmeltItem_无产出不发言()
        {
            _ctx.FurnaceSystem = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            var ui = _host.AddComponent<CraftingFurnaceUi>();
            ui.Bind(_ctx.FurnaceSystem);

            Assert.That(ui.TryTakeOutput(), Is.False, "没有产出时取料应失败");
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "空手取料不应发事件");

            // 直接灌一个产出（Restore 是存档恢复专用入口，测试里当注入器用）
            _ctx.FurnaceSystem.Restore(
                new ItemStack(FurnaceSystem.SmeltInputItemId, 1), new ItemStack(FurnaceSystem.CoalItemId, 1),
                new ItemStack(FurnaceSystem.SmeltOutputItemId, 1), 1f, 8f);

            Assert.That(ui.TryTakeOutput(), Is.True, "有产出时应取到");
            Assert.That(_ctx.Inventory.CountOf(FurnaceSystem.SmeltOutputItemId), Is.EqualTo(1), "铁锭应进包");
            Assert.That(_ctx.FurnaceSystem.Output, Is.Null, "取走后输出槽清空");
        }

        [Test]
        public void 熔炉投入圆石与煤_烧炼取出全链_只发SmeltItem()
        {
            // 单任务链只认 SmeltItem(1004)×1：全链走通 = 投料 → 烧 → 取 → 事件完成
            string path = WriteChapter(@"[
                { ""id"": ""s7"", ""name"": ""炼一根铁锭"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SmeltItem"", ""itemId"": 1004, ""count"": 1 }, ""rewardExp"": 20 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));
            _ctx.FurnaceSystem = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            var ui = _host.AddComponent<CraftingFurnaceUi>();
            ui.Bind(_ctx.FurnaceSystem);

            _ctx.Inventory.SetSlot(0, new ItemStack(FurnaceSystem.SmeltInputItemId, 2));
            _ctx.Inventory.SelectedHotbarIndex = 0;
            Assert.That(ui.DepositSelectedAsInput(), Is.True, "选中圆石应能投入输入槽");
            Assert.That(_ctx.FurnaceSystem.Input.Value.Count, Is.EqualTo(2));
            Assert.That(_ctx.Inventory.GetSelected().IsEmpty, Is.True, "投入后手上清空");

            _ctx.Inventory.SetSlot(0, new ItemStack(FurnaceSystem.CoalItemId, 1));
            Assert.That(ui.DepositSelectedAsFuel(), Is.True, "选中煤应能投入燃料槽");
            Assert.That(_ctx.FurnaceSystem.FuelRemaining, Is.EqualTo(8f));

            _ctx.FurnaceSystem.Tick(1.1f);
            Assert.That(_ctx.FurnaceSystem.Output.Value.ItemId, Is.EqualTo(FurnaceSystem.SmeltOutputItemId),
                "圆石 + 煤应烧出铁锭");

            Assert.That(ui.TryTakeOutput(), Is.True);
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "取出铁锭应经 SmeltItem 事件完成任务");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(20), "任务奖励经验 20 应入账");
        }

        // ─── 4) 昼夜 → SurviveNight ──────────────────────────────────────────

        [Test]
        public void 跨过日出刻那一帧_发SurviveNight_日间与回绕帧不发()
        {
            string path = WriteChapter(@"[
                { ""id"": ""n1"", ""name"": ""活过一夜一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 30 },
                { ""id"": ""n2"", ""name"": ""活过一夜二"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 30 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));

            // Bind 时基线在正午 6000。白天推进不发。
            _ctx.Time.CurrentTick = 12000f;
            _bus.WatchNightCrossing();
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "白天推进不应发 SurviveNight");

            // 入夜后推进到日出刻前 1 tick 仍不发。
            _ctx.Time.CurrentTick = 22999f;
            _bus.WatchNightCrossing();
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "22999 还没跨过 23000（NightEndTick）");

            // 跨过日出刻的那一帧发事件（EditMode 手动步进模拟 Advance 后的 Update）。
            _ctx.Time.CurrentTick = 23001f;
            _bus.WatchNightCrossing();
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "跨过 NightEndTick 应发 SurviveNight");

            // 日出后到午夜回绕都不发（23000..24000 与回绕帧 prev≥curr 均不判）。
            _ctx.Time.CurrentTick = 23500f;
            _bus.WatchNightCrossing();
            _ctx.Time.CurrentTick = 100f; // 模拟 Advance 在 24000 处回绕后的帧
            _bus.WatchNightCrossing();
            _ctx.Time.CurrentTick = 6000f;
            _bus.WatchNightCrossing();
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "回绕帧 / 白天不应重复发");

            // 第二个夜晚再次跨过日出 → 第二次事件。
            _ctx.Time.CurrentTick = 22999f;
            _bus.WatchNightCrossing();
            _ctx.Time.CurrentTick = 23000f;
            _bus.WatchNightCrossing();
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(2), "第二个日出应再发一次（恰好等于 NightEndTick 也算跨过）");
            Assert.That(_bus.Quests.Current, Is.Null, "两夜任务链走完");
        }

        // ─── 4.5) m6 C2 fix1：合成网格点击交互 + 口袋产出 leftover 修复 ───────

        [Test]
        public void 工作台点击交互_放四块木板造工作台_点输出入包并发CraftItem事件()
        {
            string path = WriteChapter(@"[
                { ""id"": ""w3"", ""name"": ""造工作台"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1700, ""count"": 1 }, ""rewardExp"": 10 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));
            _ctx.Items = ItemDatabaseLoader.Load();
            _ctx.Recipes = ItemDatabaseLoader.LoadRecipes(_ctx.Items); // planks_to_crafting_table：workbench 档
            var ui = _host.AddComponent<CraftingWorkbenchUi>();

            _ctx.Inventory.SetSlot(0, new ItemStack(1001, 4)); // 4 块木板在手上
            _ctx.Inventory.SelectedHotbarIndex = 0;

            Assert.That(ui.ClickGridCell(0), Is.True, "点空格：从选中 hotbar 格放 1 块木板");
            Assert.That(ui.ClickGridCell(1), Is.True);
            Assert.That(ui.ClickGridCell(2), Is.True);
            Assert.That(ui.ClickGridCell(3), Is.True, "shapeless 配方：四格各 1 块即匹配");
            Assert.That(_ctx.Inventory.GetSelected().IsEmpty, Is.True, "4 块全部入格后手上清零");

            ui.CraftForTest();
            Assert.That(ui.LastOutput.HasValue, Is.True, "网格变化后配方匹配应刷新输出格");
            Assert.That(ui.LastOutput.Value.ItemId, Is.EqualTo(1700), "4 板 → 工作台");

            Assert.That(ui.TryTakeCraftOutput(), Is.True, "点输出格：拿走产出");
            Assert.That(_ctx.Inventory.CountOf(1700), Is.EqualTo(1), "工作台 +1 进包");
            Assert.That(_ctx.Inventory.CountOf(1001), Is.EqualTo(0), "网格里的 4 块板被配方消耗，不复制");
            Assert.That(ui.GetCellForTest(0).IsEmpty, Is.True, "取产出后网格清空");
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "CraftItem(crafting_table,1) 事件应完成任务");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(10), "奖励经验 10 入账");
        }

        [Test]
        public void 合成格点击_空格放1个再点取回_背包满则取不回留在格子里()
        {
            _ctx.Items = ItemDatabaseLoader.Load();
            _ctx.Recipes = ItemDatabaseLoader.LoadRecipes(_ctx.Items);
            var ui = _host.AddComponent<CraftingInventoryUi>();

            _ctx.Inventory.SetSlot(0, new ItemStack(1000, 3));
            _ctx.Inventory.SelectedHotbarIndex = 0;

            Assert.That(ui.ClickGridCell(0), Is.True, "空格点击：放 1 个");
            Assert.That(_ctx.Inventory.GetSelected().Count, Is.EqualTo(2), "手上 3 → 2");
            Assert.That(ui.GetCellForTest(0).ItemId, Is.EqualTo(1000), "网格格里是刚放的物品");

            Assert.That(ui.ClickGridCell(0), Is.True, "再点同一格：取回 1 个");
            Assert.That(_ctx.Inventory.CountOf(1000), Is.EqualTo(3), "取回后背包回到 3");
            Assert.That(ui.GetCellForTest(0).IsEmpty, Is.True, "格子清空");

            // 背包对格内物品零空间时取不回（物品留在格子里，不凭空消失）
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
                _ctx.Inventory.SetSlot(i, new ItemStack(5000 + i, 64)); // 每格互不相同且叠满
            _ctx.Inventory.SelectedHotbarIndex = 0;
            Assert.That(ui.ClickGridCell(1), Is.True, "满包仍可放格（放是手上 -1，不占背包）");
            _ctx.Inventory.SetSlot(0, new ItemStack(5000, 64)); // 手上补回满 → 对格内物品零空间
            Assert.That(ui.ClickGridCell(1), Is.False, "背包零空间：取回失败");
            Assert.That(ui.GetCellForTest(1).ItemId, Is.EqualTo(5000), "物品应留在格子里");
        }

        [Test]
        public void 背包合成取产出_发CraftItem并精确消耗网格()
        {
            // m6 终审修 M2：workbench / pocket 的 TryTakeCraftOutput 各有专项，
            // inventory 2×2 版此前零直接测试（代码同构）。链首放 CraftItem（链式解锁，
            // ObtainItem 在前会挡住断言）。配方选 planks_to_sticks（inventory 档 2 板 → 4 棍）——
            // pocket 档配方被 CraftingMatrix 限定只能在 1×1 网格匹配，2×2 用不了。
            string path = WriteChapter(@"[
                { ""id"": ""e2"", ""name"": ""合成木棍"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1002, ""count"": 4 }, ""rewardExp"": 5 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));
            _ctx.Items = ItemDatabaseLoader.Load();
            _ctx.Recipes = ItemDatabaseLoader.LoadRecipes(_ctx.Items); // planks_to_sticks：inventory 档
            var ui = _host.AddComponent<CraftingInventoryUi>();
            ui.SetGridForTest(new[] { 1001, 0, 0, 1001 }); // 对角 2 块木板（shapeless 只数总数）

            Assert.That(ui.TryTakeCraftOutput(), Is.True, "背包 2×2：点输出格应拿到产出");
            Assert.That(_ctx.Inventory.CountOf(1002), Is.EqualTo(4), "4 根木棍进包");
            Assert.That(ui.GetCellForTest(0).IsEmpty, Is.True, "网格里的木板被配方精确消耗");
            Assert.That(_ctx.Inventory.CountOf(1001), Is.EqualTo(0), "木板不复制");
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "CraftItem(stick,4) 事件应完成任务");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(5), "奖励经验 5 入账");
        }

        [Test]
        public void 合成格放取工具_保留Metadata_耐久不重置()
        {
            // m6 终审修 M1：PutSelectedOne/TakeBackOne 旧实现 new ItemStack(id, 1) 丢 Metadata——
            // 磨损的工具放格再取回 = 免费修复，是刷修复边角。WithCount(1) 必须把 Metadata 带上。
            _ctx.Items = ItemDatabaseLoader.Load();
            _ctx.Recipes = ItemDatabaseLoader.LoadRecipes(_ctx.Items);
            var ui = _host.AddComponent<CraftingInventoryUi>();

            // 木镐（1400）：满耐久 60 磨到 47
            var worn = new ItemStack(1400, 1).WithMaxDurability(60);
            for (int i = 0; i < 13; i++) worn = worn.DamageOnce();
            Assert.That(worn.CurrentDurability, Is.EqualTo(47), "前置自检：磨损到 47");
            _ctx.Inventory.SetSlot(0, worn);
            _ctx.Inventory.SelectedHotbarIndex = 0;

            Assert.That(ui.ClickGridCell(0), Is.True, "空格点击：放 1 个");
            Assert.That(ui.GetCellForTest(0).Metadata, Is.EqualTo(worn.Metadata),
                "放格时 Metadata 必须随物品走");

            Assert.That(ui.ClickGridCell(0), Is.True, "再点同一格：取回 1 个");
            var back = _ctx.Inventory.GetSlot(0);
            Assert.That(back.ItemId, Is.EqualTo(1400), "取回的还是木镐");
            Assert.That(back.Metadata, Is.EqualTo(worn.Metadata),
                "取回时 Metadata 必须原样带回——丢失就是刷修复（放格取回耐久重置成全新）");
            Assert.That(back.CurrentDurability, Is.EqualTo(47), "磨损 47 点必须保持，不能被重置");
        }

        [Test]
        public void 口袋合成_背包满时拿取失败_产出不丢_事件不发()
        {
            // 链首放 CraftItem（链式解锁，ObtainItem 在前会挡住断言）
            string path = WriteChapter(@"[
                { ""id"": ""p2"", ""name"": ""合成木板"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 } ]");
            _bus.Bind(_ctx, QuestSystem.LoadChapter(path));
            _ctx.Items = ItemDatabaseLoader.Load();
            _ctx.Recipes = ItemDatabaseLoader.LoadRecipes(_ctx.Items); // log_to_planks：pocket 档
            var ui = _host.AddComponent<CraftingPocketUi>();
            ui.SetInputForTest(new ItemStack(1000, 1));

            for (int i = 0; i < PlayerInventory.TotalSize; i++)
                _ctx.Inventory.SetSlot(i, new ItemStack(6000 + i, 64)); // 全满：对木板零空间

            Assert.That(ui.TryTakeCraftOutput(), Is.False, "背包装不下产出：拿取应失败");
            Assert.That(_ctx.Inventory.CountOf(1001), Is.EqualTo(0), "没有木板凭空进包");
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "拿取失败不应发 CraftItem 事件（旧 bug：丢了产出还发事件）");

            _ctx.Inventory.SetSlot(0, ItemStack.Empty); // 腾一格
            Assert.That(ui.TryTakeCraftOutput(), Is.True, "腾出空间后重试应成功——也证明失败尝试没有消耗输入");
            Assert.That(_ctx.Inventory.CountOf(1001), Is.EqualTo(4), "4 块木板这时才进包");
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "此时才发 CraftItem(plank,4) 事件");
        }

        // ─── 5) 真实首章数据集成 ─────────────────────────────────────────────

        [Test]
        public void 真实chapter1链_拾取一根原木_完成首任务并入账5经验()
        {
            _bus.Bind(_ctx, QuestSystem.LoadChapter(Chapter1Path()));

            _host.transform.position = Vector3.zero;
            _ctx.ItemDrops.Add(new ItemDropEntity(new ItemStack(1000, 1), new Float3(0.5f, 0f, 0f)));
            // m7 B1 吸附语义：逐步步进到入包（0.5m → 贴脸 <0.3m）
            int steps = 0;
            while (_ctx.ItemDrops.Count > 0 && steps < 600)
            {
                _player.PickupNearbyDrops(1f / 60f);
                steps++;
            }

            Assert.That(_bus.Quests.Current.Id, Is.EqualTo("ch1_02_craft_planks"),
                "拾取原木完成 ch1_01 后应停在 ch1_02");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(5), "ch1_01 奖励 5 经验应入账");
        }

        private static string Chapter1Path()
        {
            return Path.Combine(Application.streamingAssetsPath, "quests", "chapter1.json");
        }
    }
}
#endif
