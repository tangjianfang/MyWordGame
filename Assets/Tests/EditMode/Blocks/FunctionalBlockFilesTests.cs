using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// m11 W1-4：五个功能方块（火把/玻璃/箱子/木门/床）+ 配套物品/配方/掉落/熔炉的真文件守卫。
    /// 数字全部钉住：方块 numericId 显式 1022–1026 段、物品 1350–1355 段——两个段位是
    /// 并行波次的防撞约定（方块段紧跟 W1-5 的 1013–1021、W1-6 走 1050+；物品段避开
    /// arrow 的 1300、W1-1 弓盾的 1301/1302、W1-6 锄头的 1430+），
    /// 谁挤进来这里立刻红，避免「自动分配在并行落地时漂移」把存档里的方块悄悄换掉。
    /// </summary>
    [TestFixture]
    public class FunctionalBlockFilesTests
    {
        private BlockRegistry _blocks;
        private ItemDatabase _items;
        private RecipeDatabase _recipes;
        private BlockDrops _drops;

        [OneTimeSetUp]
        public void LoadRealDefinitions()
        {
            string blocksDir = LocateStreamingAssetsSubdirectory("blocks");
            string itemsDir = LocateStreamingAssetsSubdirectory("items");
            string recipesDir = LocateStreamingAssetsSubdirectory("recipes");

            _blocks = BlockRegistry.FromJson(Directory.GetFiles(blocksDir, "*.json").Select(File.ReadAllText));
            _items = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));
            _recipes = RecipeDatabase.FromJson(
                Directory.GetFiles(recipesDir, "*.json").Select(File.ReadAllText), _items);
            _drops = BlockDrops.FromJson(
                new[] { File.ReadAllText(Path.Combine(blocksDir, "drops", "block_drops.json")) }, _items);
        }

        // ─── 五个功能方块：注册 + numericId 段位 + 物理属性 ───────────────────────

        [TestCase("bed", (ushort)1022)]
        [TestCase("chest", (ushort)1023)]
        [TestCase("glass", (ushort)1024)]
        [TestCase("torch", (ushort)1025)]
        [TestCase("wooden_door", (ushort)1026)]
        public void 五方块_已注册且numericId在显式段(string id, ushort expected)
        {
            Assert.That(_blocks.GetById(id).NumericId, Is.EqualTo(expected),
                $"{id} 的 numericId 应显式钉在 {expected}（1022–1026 是 W1-4 的防撞段，改了会漂移存档）");
        }

        [Test]
        public void 火把_不挡移动不挡视线且自发光14()
        {
            var torch = _blocks.GetById("torch");

            Assert.That(torch.Solid, Is.False, "火把是杆子不是墙，玩家应能穿过它所在格子");
            Assert.That(torch.Opaque, Is.False, "火把不挡光——Opaque=true 会把它四周的面全部错误剔除");
            Assert.That(torch.LightEmission, Is.EqualTo((byte)14), "火把光照 14（MC 同款，仅比满级少 1）");
            Assert.That(torch.MinToolTier, Is.EqualTo(0), "徒手可挖");
        }

        [Test]
        public void 玻璃_不挡视线但挡移动()
        {
            var glass = _blocks.GetById("glass");

            Assert.That(glass.Solid, Is.True, "玻璃是实体方块，玩家不能穿墙");
            Assert.That(glass.Opaque, Is.False, "玻璃透光（照 water.json 的非实心路径），Opaque=true 会挡住玻璃后的地形");
            Assert.That(glass.Liquid, Is.False);
        }

        [TestCase("chest")]
        [TestCase("wooden_door")]
        [TestCase("bed")]
        public void 箱门床_常规实体方块属性(string id)
        {
            var block = _blocks.GetById(id);
            Assert.That(block.Solid, Is.True, $"{id} 挡移动");
            Assert.That(block.Opaque, Is.True, $"{id} 按整格实心简化（半高/镂空造型留给渲染层）");
            Assert.That(block.Hardness, Is.EqualTo(1.0f).Within(1e-5f),
                $"{id} hardness=1：走 BlockInteraction.BreakTime 的 default 分支（m11 并行波禁改 Unity 层，偏离 1 会红 EditMode 全量对表守卫）");
        }

        [Test]
        public void 五方块_引用的贴图都有美术需求()
        {
            string artDir = LocateArtRequestsDirectory();

            (string blockId, string[] expectedTextures)[] expectations =
            {
                ("torch", new[] { "torch" }),
                ("glass", new[] { "glass" }),
                ("chest", new[] { "chest-top", "chest-front" }),
                ("wooden_door", new[] { "wooden-door-upper", "wooden-door-lower" }),
                ("bed", new[] { "bed-head-top", "bed-side" }),
            };

            foreach ((string blockId, string[] expected) in expectations)
            {
                var referenced = new SortedSet<string>(_blocks.GetById(blockId).Textures, StringComparer.Ordinal);
                Assert.That(referenced, Is.EquivalentTo(expected),
                    $"{blockId} 六面引用的贴图应与 A1 立过的需求名一一对应");

                foreach (string texture in referenced)
                {
                    Assert.That(File.Exists(Path.Combine(artDir, texture + ".md")), Is.True,
                        $"{blockId} 引用的贴图 {texture} 在 art/requests/blocks 下没有需求文件，美术链路会断档");
                }
            }
        }

        // ─── 两条 IsSolid 路径：玻璃照水先例走非实心网格路径，火把双不挡 ───────────

        [Test]
        public void 玻璃与火把_两条IsSolid路径各归其位()
        {
            var world = new World();
            ushort glass = _blocks.GetById("glass").NumericId;
            ushort torch = _blocks.GetById("torch").NumericId;
            ushort stone = _blocks.GetById("stone").NumericId;

            world.SetBlock(0, 70, 0, glass);
            world.SetBlock(2, 70, 0, torch);

            var collision = new WorldSolidSource(world, _blocks);
            var mesh = new ChunkMeshSource(world, _blocks, new ChunkPos(0, 0), 64);

            // 玻璃：挡移动（Solid=true）但不挡视线（Opaque=false → 网格不剔除玻璃后方面）
            Assert.That(collision.IsSolidAt(0, 70, 0), Is.True, "玻璃应挡住玩家移动");
            Assert.That(mesh.IsSolid(glass), Is.False, "玻璃不透明度判定应为 false（照水先例），否则玻璃后的面被剔除");

            // 火把：两路都不挡（杆子可穿过、不挡视线）
            Assert.That(collision.IsSolidAt(2, 70, 0), Is.False, "火把不挡移动");
            Assert.That(mesh.IsSolid(torch), Is.False, "火把不挡视线");

            // 对照组：石头两路都挡，证明断言路径本身没有失效
            Assert.That(collision.IsSolidAt(0, 70, 1), Is.False, "空气不挡移动");
            Assert.That(mesh.IsSolid(stone), Is.True, "石头照旧挡视线");
        }

        // ─── 六个配套物品：注册 + 1300 段位 + 熔炉常量绑定 ────────────────────────

        [TestCase("torch", 1350)]
        [TestCase("chest", 1351)]
        [TestCase("wooden_door", 1352)]
        [TestCase("bed", 1353)]
        [TestCase("glass", 1354)]
        [TestCase("sand", 1355)]
        public void 六物品_已注册且numericId在1350段(string id, int expected)
        {
            Assert.That(_items.GetById(id).NumericId, Is.EqualTo(expected),
                $"{id} 应显式钉在 {expected}（1350 段是 W1-4 物品防撞段：1300 被 arrow 占用、"
                + "1301/1302 被 W1-1 弓盾占用，1430+ 被 W1-6 锄头占用）");
        }

        [Test]
        public void 熔炉常量_与真物品表一致()
        {
            Assert.That(_items.GetById("sand").NumericId, Is.EqualTo(FurnaceSystem.SandItemId),
                "FurnaceSystem.SandItemId 必须指向真表里的沙子物品，否则熔炉永远吃不到沙");
            Assert.That(_items.GetById("glass").NumericId, Is.EqualTo(FurnaceSystem.GlassItemId),
                "FurnaceSystem.GlassItemId 必须指向真表里的玻璃物品");
        }

        [Test]
        public void 熔炉_沙子10秒烧成玻璃()
        {
            var furnace = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);

            Assert.That(furnace.AddInput(new ItemStack(FurnaceSystem.SandItemId, 1)), Is.True);
            Assert.That(furnace.CurrentSmeltDuration, Is.EqualTo(10f).Within(1e-5f),
                "沙子烧玻璃 10 秒（与粗矿同档），不能走构造时长 1s");
            // 一块煤 8s 烧不完 10s 的沙子，投两块（16s 燃料）——燃料耗尽 Tick 会空转
            Assert.That(furnace.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 2)), Is.True);

            furnace.Tick(9.9f);
            Assert.That(furnace.Output, Is.Null, "9.9s 还没烧好");

            furnace.Tick(0.2f);
            Assert.That(furnace.Output, Is.Not.Null, "10s 出玻璃");
            Assert.That(furnace.Output.Value.ItemId, Is.EqualTo(FurnaceSystem.GlassItemId));
            Assert.That(furnace.Output.Value.Count, Is.EqualTo(1));
        }

        // ─── 四个配方：真表加载可匹配、产物数量正确 ───────────────────────────────

        [Test]
        public void 配方_木棍加煤_口袋外2x2合成火把4支()
        {
            var input = new List<ItemStack>
            {
                new ItemStack(_items.GetById("stick").NumericId, 1),
                new ItemStack(_items.GetById("coal").NumericId, 1),
                ItemStack.Empty,
                ItemStack.Empty,
            };

            var recipe = _recipes.FindMatch(input, width: 2, height: 2);

            Assert.That(recipe, Is.Not.Null, "木棍+煤应能合成火把");
            Assert.That(recipe.Id, Is.EqualTo("torch_recipe"));
            Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById("torch").NumericId));
            Assert.That(recipe.Output.Count, Is.EqualTo(4), "一支木棍一块煤出 4 支火把");
        }

        [Test]
        public void 配方_木板8块_工作台合成箱子()
        {
            int plank = _items.GetById("plank").NumericId;
            var input = new List<ItemStack>
            {
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                new ItemStack(plank, 1), ItemStack.Empty, new ItemStack(plank, 1),
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
            };

            var recipe = _recipes.FindMatch(input, width: 3, height: 3);

            Assert.That(recipe, Is.Not.Null, "木板环形 8 块应能合成箱子");
            Assert.That(recipe.Id, Is.EqualTo("chest_recipe"));
            Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById("chest").NumericId));
            Assert.That(recipe.Output.Count, Is.EqualTo(1));
        }

        [Test]
        public void 配方_木板6块_工作台合成木门3扇()
        {
            int plank = _items.GetById("plank").NumericId;
            var input = new List<ItemStack>
            {
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                ItemStack.Empty, ItemStack.Empty, ItemStack.Empty,
            };

            var recipe = _recipes.FindMatch(input, width: 3, height: 3);

            Assert.That(recipe, Is.Not.Null, "木板两行 6 块应能合成木门");
            // m11 ②起 FindMatch 同档位内稳定排序：shaped 优先于 shapeless、材料格多者优先
            // （Recipe.MaterialCount）——6 板门不再依赖「文件名恰好排在 planks_to_crafting_table
            // 前面」的枚举顺序侥幸赢过 4 板工作台的 shapeless 宽匹配。
            Assert.That(recipe.Id, Is.EqualTo("door_wooden_recipe"));
            Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById("wooden_door").NumericId));
            Assert.That(recipe.Output.Count, Is.EqualTo(3), "6 块木板出 3 扇门（MC 同款）");

            // 反向不误伤：恰好 4 块木板仍能合工作台（木门配方 shape 6 块不匹配 4 块的摆法）
            var tableInput = new List<ItemStack>
            {
                new ItemStack(plank, 1), new ItemStack(plank, 1), ItemStack.Empty,
                new ItemStack(plank, 1), new ItemStack(plank, 1), ItemStack.Empty,
                ItemStack.Empty, ItemStack.Empty, ItemStack.Empty,
            };
            Assert.That(_recipes.FindMatch(tableInput, width: 3, height: 3)?.Id,
                Is.EqualTo("planks_to_crafting_table"), "4 块木板的旧配方行为不能被木门抢走");
        }

        [Test]
        public void 配方_羊毛3加木板3_工作台合成床()
        {
            int wool = _items.GetById("wool").NumericId;
            int plank = _items.GetById("plank").NumericId;
            var input = new List<ItemStack>
            {
                new ItemStack(wool, 1), new ItemStack(wool, 1), new ItemStack(wool, 1),
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                ItemStack.Empty, ItemStack.Empty, ItemStack.Empty,
            };

            var recipe = _recipes.FindMatch(input, width: 3, height: 3);

            Assert.That(recipe, Is.Not.Null, "羊毛 3 + 木板 3 应能合成床");
            Assert.That(recipe.Id, Is.EqualTo("bed_recipe"));
            Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById("bed").NumericId));
            Assert.That(recipe.Output.Count, Is.EqualTo(1));
        }

        // ─── 掉落表：沙子掉自身物品，五方块挖掉各回自身物品 ────────────────────────

        [Test]
        public void 掉落_沙方块掉沙子物品()
        {
            ushort sand = _blocks.GetById("sand").NumericId;

            var drops = _drops.DropsFor(sand);

            Assert.That(drops.Length, Is.EqualTo(1));
            Assert.That(drops[0].ItemId, Is.EqualTo(_items.GetById("sand").NumericId),
                "沙方块应掉沙子物品（旧值 dirt 是沙子物品诞生前的占位），否则熔炉烧玻璃的原料拿不到");
        }

        [TestCase("torch", "torch")]
        [TestCase("glass", "glass")]
        [TestCase("chest", "chest")]
        [TestCase("wooden_door", "wooden_door")]
        [TestCase("bed", "bed")]
        public void 掉落_功能方块挖掉回自身物品(string blockId, string itemId)
        {
            ushort numeric = _blocks.GetById(blockId).NumericId;

            var drops = _drops.DropsFor(numeric);

            Assert.That(drops.Length, Is.EqualTo(1), $"{blockId} 应有一条掉落");
            Assert.That(drops[0].ItemId, Is.EqualTo(_items.GetById(itemId).NumericId),
                $"{blockId} 挖掉应掉回 {itemId} 物品，孩子搭错拆掉不该血本无归");
        }

        // ─── 目录定位（dotnet 从输出目录向上爬；Unity 走 streamingAssetsPath） ──

        private static string LocateStreamingAssetsSubdirectory(string name)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, name);
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", name);
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException($"未能从测试输出目录向上找到 Assets/StreamingAssets/{name}。");
#endif
        }

        private static string LocateArtRequestsDirectory()
        {
            string blocksDir = LocateStreamingAssetsSubdirectory("blocks");
            string repoRoot = new DirectoryInfo(blocksDir).Parent.Parent.Parent.FullName;
            return Path.Combine(repoRoot, "art", "requests", "blocks");
        }
    }
}
