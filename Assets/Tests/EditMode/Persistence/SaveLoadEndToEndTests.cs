#if UNITY_EDITOR
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
    /// （EditMode-only：MonoBehaviour + PlayerContext 单例。）
    /// </summary>
    [TestFixture]
    public class SaveLoadEndToEndTests
    {
        private string _saveRoot;
        private readonly System.Collections.Generic.List<GameObject> _gos =
            new System.Collections.Generic.List<GameObject>();

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
    }
}
#endif
