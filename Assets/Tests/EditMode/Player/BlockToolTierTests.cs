#if UNITY_EDITOR
// m10 A3：BlockInteraction.BreakAt 的工具门槛路径 + BreakTime 加 toolTier 维度。
// 门槛：toolTier < block.minToolTier → 方块照样挖掉、不走 BlockDrops、hotbar 上方一次性
// 提示「需要更好的镐」。依赖 Unity MonoBehaviour（PlayerContext / BlockInteraction），
// 与 BlockBreakDropTests 同款 #if UNITY_EDITOR 包裹，dotnet 链由 BlockGatingTests（Core）覆盖纯函数。
using System.IO;
using System.Linq;
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class BlockToolTierTests
    {
        private GameObject _host;
        private GameObject _interactorHost;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;
        private BlockRegistry _registry;

        private const int RawIronItemId = 1024;
        private const int CobblestoneItemId = 1003;
        private const int WoodenPickaxeItemId = 1400;
        private const int StonePickaxeItemId = 1401;

        /// <summary>EditMode 下 AddComponent 不会跑 Awake，用反射补一脚（BlockBreakDropTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        private static BlockRegistry BuildRegistry()
        {
            var docs = new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                // stone 故意不写 minToolTier：守住「没配门槛的方块挖矿行为不变」的回归
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
                // 铁矿：minToolTier=2（需石镐），hardness=2（石镐挖 2s）
                @"{ ""id"": ""raw_iron_ore"", ""numericId"": 1010, ""textures"": { ""all"": ""iron-ore"" },
                    ""hardness"": 2.0, ""minToolTier"": 2 }",
            };
            return BlockRegistry.FromJson(docs);
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""cobblestone"", ""numericId"": 1003, ""texture"": ""cobblestone"" }",
                @"{ ""id"": ""raw_iron"", ""numericId"": 1024, ""texture"": ""raw_iron"" }",
                @"{ ""id"": ""wooden_pickaxe"", ""numericId"": 1400, ""texture"": ""wooden_pickaxe"",
                    ""isTool"": true, ""miningLevel"": 1, ""toolTier"": 1 }",
                @"{ ""id"": ""stone_pickaxe"", ""numericId"": 1401, ""texture"": ""stone_pickaxe"",
                    ""isTool"": true, ""miningLevel"": 2, ""toolTier"": 2 }",
            });
        }

        private static BlockDrops BuildDrops()
        {
            string[] docs =
            {
                @"{ ""blockId"": ""stone"", ""blockNumericId"": 1, ""drops"": [
                    { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 1 } ] }",
                @"{ ""blockId"": ""raw_iron_ore"", ""blockNumericId"": 1010, ""drops"": [
                    { ""itemId"": ""raw_iron"", ""countMin"": 1, ""countMax"": 1 } ] }",
            };
            return BlockDrops.FromJson(docs, BuildItems());
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("BlockToolTierCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = BuildItems();

            _world = new World();
            _registry = BuildRegistry();

            _interactorHost = new GameObject("BlockToolTierBlock");
            _block = _interactorHost.AddComponent<BlockInteraction>();
            _block.Bind(_world, _registry, null, _interactorHost.transform);
            _block.SetBlockDrops(BuildDrops());
        }

        [TearDown]
        public void TearDown()
        {
            if (_interactorHost != null) Object.DestroyImmediate(_interactorHost);
            if (_host != null) Object.DestroyImmediate(_host);
        }

        private void SelectNothing() => _ctx.Inventory.SelectedHotbarIndex = 0; // 槽位全空 = 徒手

        private void SelectPickaxe(int itemId)
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(itemId, 1));
            _ctx.Inventory.SelectedHotbarIndex = 0;
        }

        /// <summary>
        /// 木镐（toolTier 1）挖铁矿（minToolTier 2）：方块消失、不掉任何物品、
        /// 门槛提示触发一次——「挖得掉但白挖」，孩子的 §130 需求按 MC 制修正后的样子。
        /// </summary>
        [Test]
        public void BreakAt_IronOre_WithWoodenPickaxe_BlockBreaks_NoDrops_HintShown()
        {
            SelectPickaxe(WoodenPickaxeItemId);
            _world.SetBlock(8, 70, 8, BlockIds.RawIronOre);
            int dropsBefore = _ctx.ItemDrops.Count;

            _block.BreakAt(8, 70, 8);

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air),
                "门槛不够也应该挖得掉方块（MC 制：门槛限制掉落，不限制破坏）");
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore),
                "木镐挖铁矿不应产生掉落（不走 BlockDrops）");
            Assert.That(_block.ToolTierHintCount, Is.EqualTo(1),
                "首次不达标应触发一次「需要更好的镐」提示");
        }

        /// <summary>徒手（无选中物品）挖铁矿：同样挖得掉、无掉落、有提示。</summary>
        [Test]
        public void BreakAt_IronOre_BareHand_NoDrops()
        {
            SelectNothing();
            _world.SetBlock(8, 70, 8, BlockIds.RawIronOre);

            _block.BreakAt(8, 70, 8);

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air));
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(0), "徒手挖铁矿不掉");
            Assert.That(_block.ToolTierHintCount, Is.EqualTo(1), "徒手也应看到提示");
        }

        /// <summary>石镐（toolTier 2）挖铁矿：正常掉 1 个粗铁，无提示。</summary>
        [Test]
        public void BreakAt_IronOre_WithStonePickaxe_DropsMaterial_NoHint()
        {
            SelectPickaxe(StonePickaxeItemId);
            _world.SetBlock(8, 70, 8, BlockIds.RawIronOre);
            int dropsBefore = _ctx.ItemDrops.Count;

            _block.BreakAt(8, 70, 8);

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air));
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore + 1), "石镐挖铁矿应掉 1 个");
            Assert.That(_ctx.ItemDrops[_ctx.ItemDrops.Count - 1].Content.Value.ItemId,
                Is.EqualTo(RawIronItemId), "掉的应是 raw_iron");
            Assert.That(_block.ToolTierHintCount, Is.EqualTo(0), "达标挖掘不应有提示");
        }

        /// <summary>
        /// 提示的「一次性」语义：2 秒显示窗口内连挖多块不达标方块，提示不重置不叠加；
        /// 窗口过后再次不达标才重新提示。EditMode 下 Time.time 恒为 0，两次连挖必落在
        /// 同一个窗口内——正好钉死「不叠加」这一半契约。
        /// </summary>
        [Test]
        public void BreakAt_Hint_TriggeredOncePerWindow()
        {
            SelectPickaxe(WoodenPickaxeItemId);
            _world.SetBlock(8, 70, 8, BlockIds.RawIronOre);
            _world.SetBlock(9, 70, 8, BlockIds.RawIronOre);

            _block.BreakAt(8, 70, 8);
            _block.BreakAt(9, 70, 8);

            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(0));
            Assert.That(_block.ToolTierHintCount, Is.EqualTo(1),
                "提示显示期间（EditMode 下 Time.time 不走，必然还在 2s 窗口内）再挖不达标方块，提示不应重复触发");
        }

        /// <summary>
        /// 回归：没配 minToolTier 的方块（测试内联 stone，默认 0）无论拿什么挖，
        /// 掉落行为与 m9 之前完全一致——门槛是新增维度，不改变未声明门槛的方块。
        /// </summary>
        [Test]
        public void BreakAt_BlockWithoutGate_DropsUnchanged()
        {
            SelectPickaxe(WoodenPickaxeItemId);
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            int dropsBefore = _ctx.ItemDrops.Count;

            _block.BreakAt(8, 70, 8);

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air));
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore + 1), "无门槛方块照常掉落");
            Assert.That(_ctx.ItemDrops[_ctx.ItemDrops.Count - 1].Content.Value.ItemId,
                Is.EqualTo(CobblestoneItemId), "掉的应是 cobblestone");
            Assert.That(_block.ToolTierHintCount, Is.EqualTo(0), "无门槛方块不应触发提示");
        }

        // ─── BreakTime(blockId, biome, toolTier, hardness) 即挖/回退分支（m11 ②） ──

        /// <summary>
        /// m11 ②：hardness 第 4 参的三条契约——①[0, 0.05) 秒挖 → 0.15s（与 blockId 无关，
        /// 花草/作物这类 hardness=0 的装饰方块不用逐个进 switch）；②无 switch 特例的方块
        /// 以传入 hardness 为基准（farmland 0.6、新树种原木 2 等不进表也能对表）；
        /// ③不传（NaN）/负数（不可破坏）保持旧行为（m3 默认 1s，空气契约有旧测试钉着）。
        /// </summary>
        [Test]
        public void BreakTime_HardnessParam_InstantAndFallbackBranches()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Plains, 0, hardness: 0f),
                Is.EqualTo(BlockInteraction.InstantBreakSeconds), "hardness=0（花草/作物）→ 0.15s 秒挖");
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Plains, 0, hardness: 0.049f),
                Is.EqualTo(BlockInteraction.InstantBreakSeconds), "阈值左开右闭：0.049 仍秒挖");
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Plains, 0, hardness: 0.05f),
                Is.EqualTo(0.05f), "恰在阈值上不秒挖，回落 hardness 本值");

            // 无特例方块以传入 hardness 为基准：耕地 0.6（numericId=FarmSystem.FarmlandId）
            Assert.That(
                BlockInteraction.BreakTime(MyWorld.Core.Farming.FarmSystem.FarmlandId, Biome.Plains, 0, 0.6f),
                Is.EqualTo(0.6f), "farmland 不进 switch，default 分支采信传入 hardness");

            // 旧调用不传 hardness：泥 1s 不变；负 hardness（空气/基岩）回落 m3 默认 1s
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Plains, 0), Is.EqualTo(1f),
                "缺省 NaN 与旧三参调用行为完全一致");
            Assert.That(BlockInteraction.BreakTime(BlockIds.Air, Biome.Plains, 0, hardness: -1f),
                Is.EqualTo(1f), "负 hardness（不可破坏）不参与即挖/回退，回落默认 1s");
        }

        // ─── BreakTime(blockId, biome, toolTier) 查表（spec §1 矩阵 + 群系折扣） ──

        [Test]
        public void BreakTime_ToolTier_SpecMatrix()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Plains, 1),
                Is.EqualTo(1f), "木镐挖石 1s");
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Plains, 0),
                Is.EqualTo(4f), "徒手挖石 4s（且不掉）");
            Assert.That(BlockInteraction.BreakTime(BlockIds.RawIronOre, Biome.Plains, 2),
                Is.EqualTo(2f), "石镐挖铁 2s");
            Assert.That(BlockInteraction.BreakTime(BlockIds.RawIronOre, Biome.Plains, 1),
                Is.EqualTo(8f), "木镐挖铁 8s（且不掉）");
            Assert.That(BlockInteraction.BreakTime(BlockIds.GoldOre, Biome.Plains, 3),
                Is.EqualTo(3f), "铁镐挖金 3s");
            Assert.That(BlockInteraction.BreakTime(BlockIds.SummerAlloyOre, Biome.Plains, 3),
                Is.EqualTo(4f), "铁镐挖夏季合金 4s");
            Assert.That(BlockInteraction.BreakTime(BlockIds.MachineEssenceOre, Biome.Plains, 4),
                Is.EqualTo(6f), "钻镐挖机元 6s");
            Assert.That(BlockInteraction.BreakTime(BlockIds.MachineEssenceOre, Biome.Plains, 3),
                Is.EqualTo(24f), "铁镐挖机元 24s（且不掉）");
            Assert.That(BlockInteraction.BreakTime(TreeFeature.LogId, Biome.Plains, 0),
                Is.EqualTo(2f), "徒手砍木头 2s");
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Plains, 0),
                Is.EqualTo(1f), "徒手挖泥 1s");
        }

        [Test]
        public void BreakTime_BiomeMultiplier_StillAppliesAfterTierRule()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Mountains, 0),
                Is.EqualTo(8f), "山地石头 ×2 叠在徒手 4s 之上 = 8s");
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Mountains, 1),
                Is.EqualTo(2f), "山地木镐挖石 2s（m3 契约不变）");
            Assert.That(BlockInteraction.BreakTime(BlockIds.Sand, Biome.Desert, 0),
                Is.EqualTo(0.5f), "沙漠沙 ×0.5（门槛 0，倍率照旧）");
        }

        /// <summary>
        /// 旧两参签名（m3 C7 契约）在重载后语义不变：视为持达标镐的耗时。
        /// 既有 BlockInteractionBiomeTests 已逐值钉死，这里补一条重载委托关系的冒烟。
        /// </summary>
        [Test]
        public void BreakTime_LegacyTwoArg_EqualsQualifiedToolTierCall()
        {
            foreach (int blockId in new[]
                     {
                         BlockIds.Stone, BlockIds.Dirt, BlockIds.Sand, TreeFeature.LogId,
                         BlockIds.RawIronOre, BlockIds.GoldOre, BlockIds.MachineEssenceOre,
                     })
            {
                foreach (Biome biome in new[] { Biome.Plains, Biome.Mountains, Biome.Desert, Biome.Forest })
                {
                    Assert.That(
                        BlockInteraction.BreakTime(blockId, biome),
                        Is.EqualTo(BlockInteraction.BreakTime(blockId, biome, 99)),
                        $"blockId={blockId} 两参签名应等于「视为持达标镐（99）」的三参调用");
                }
            }
        }

        /// <summary>
        /// 一致性守卫（fix1 全量化）：代码里的 (block, toolTier) 查表与真实 blocks/*.json 的
        /// hardness 必须<b>逐块全量</b>一致——遍历注册表全部方块，JSON hardness ≥ 0 的每一个
        /// 都对表（达标耗时 = hardness）。谁单独改了一边（新方块忘了进 switch、或改了 JSON
        /// 没同步代码），这里立刻红。负 hardness（空气/水/基岩）表示挖不动，没有「挖掘耗时」
        /// 可言，BreakTime 对它们返回 m3 默认 1f 只是为了契约稳定（空气那条有旧测试钉着），
        /// 不参与对表。
        /// <para>
        /// m11 ②两处同步：①调用传入 <c>def.Hardness</c>（default 分支以它为基准——第 1 波
        /// 新方块如七新树种原木/树叶 2s、耕地 0.6 不进 switch 也能对表）；②hardness 落在
        /// [0, <see cref="BlockInteraction.InstantBreakHardness"/>) 的秒挖方块（12 花草 + 9 作物）
        /// 期望 <see cref="BlockInteraction.InstantBreakSeconds"/>=0.15s——「秒挖」而非「1 秒」，
        /// 这就是 12 花草 hardness=0 与本守卫冲突的修复面（改代码不改 12 份 JSON）。
        /// </para>
        /// </summary>
        [Test]
        public void BreakTime_Table_AgreesWithRealBlockJsonHardness_EveryRegisteredBlock()
        {
            string blocksDir = Path.Combine(Application.streamingAssetsPath, "blocks");
            var registry = BlockRegistry.FromJson(
                Directory.GetFiles(blocksDir, "*.json").Select(File.ReadAllText));

            int compared = 0;
            int instant = 0;
            foreach (BlockDefinition def in registry.Definitions)
            {
                if (def.Hardness < 0f)
                {
                    continue; // 不可破坏方块：无耗时语义，见方法注释
                }

                // 期望表生成（m11 ②同步）：秒挖方块 → 0.15s；其余 → JSON hardness
                float expected = def.Hardness < BlockInteraction.InstantBreakHardness
                    ? BlockInteraction.InstantBreakSeconds
                    : def.Hardness;

                Assert.That(
                    BlockInteraction.BreakTime(def.NumericId, Biome.Plains, 99, def.Hardness),
                    Is.EqualTo(expected).Within(1e-5f),
                    $"{def.Id}（numericId={def.NumericId}）：代码查表的达标耗时应等于 {expected}"
                    + $"（JSON hardness={def.Hardness}，秒挖分支阈值 {BlockInteraction.InstantBreakHardness}），"
                    + "两边改不同步了——新方块要么 JSON hardness 写 1（走 default 分支），要么同步进 BreakTime 的 switch");
                compared++;
                if (expected == BlockInteraction.InstantBreakSeconds) instant++;
            }

            // 全部已注册方块 - 3 个负 hardness（air/water/bedrock）= 参与对表数。
            // 这个数本身也是守卫：注册表新增方块而本测试没跑到全量，说明遍历路径坏了。
            Assert.That(compared, Is.EqualTo(registry.Count - 3),
                "对表方块数应为「注册总数 - 不可破坏方块数」，全量遍历不能悄悄漏块");

            // m11 ①第 1 波注册的秒挖方块（12 花草 + 9 作物 = 21）必须全部命中即挖分支——
            // 这个下限防「阈值调大误伤普通方块」或「新秒挖方块没被覆盖」两个方向的漂移。
            Assert.That(instant, Is.GreaterThanOrEqualTo(21),
                "12 花草 + 9 作物应全部走即挖分支（0.15s）");
        }
    }
}
#endif
