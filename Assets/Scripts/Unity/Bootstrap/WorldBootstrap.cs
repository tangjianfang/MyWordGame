using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.Streaming;
using MyWorld.Unity.UI;
using System.IO;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 把场景串成可玩：注册表 → 世界 + 生成器 → 区块视图索引 → 流式加载器 → 玩家 → 方块交互。
    /// 一切靠 <see cref="ChunkStreamer"/> 驱动，按玩家位置持续生成 / 卸载区块列。
    /// </summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        /// <summary>其他系统（<see cref="MobManager"/> 等）从这里拿 World，避免 FindObjectOfType。</summary>
        public static World CurrentWorld { get; private set; }

        [SerializeField] private long seed = 42;

        [Tooltip("玩家水平出生坐标（X/Z）。Y 由生成器地表高度 +2 决定，序列化的 Y 值被忽略。")]
        // m5 A2 起出生 Y 不再用固定值：旧默认 Y=120 在默认种子 42（地表 ~98）的世界里是
        // 22 格自由落体，落地结算 (22-3)=19 点摔落伤害——玩家一进角色就莫名掉大半管血。
        // 现在 Awake 里用 generator.SurfaceHeightAt 现算地表，出生即在地表上方 2 格，
        // 落差 < FallDamageThreshold(3) 不触发摔落伤害；streamer 按距中心由近到远入队，
        // 中心列第一帧就生成，2 格余量足够碰撞数据在落地前就位。
        [SerializeField] private Vector3 spawnPosition = new Vector3(0.5f, 0f, 0.5f);

        private BlockMaterialLibrary _materials;

        private World _world;
        private BlockRegistry _registry;
        private ChunkViewRegistry _views;
        private ChunkStreamer _streamer;
        private PlayerController _player;
        private BlockInteraction _interaction;
        private PlayerContext _playerContext;
        private MobManager _mobManager;
        private HandController _hand;
        private CombatController _combat;

        private void Awake()
        {
            // 0. 自适应物理屏全屏（m5 B3）：ProjectSettings 固定 1920×1080 + FullScreenWindow，
            // 非 16:9 物理屏（如 2560×1600）两侧会出现 pillarbox 黑边。启动时按物理屏
            // 原生分辨率 SetResolution，画面铺满整屏。编辑器下无效但无害。
            ApplyAdaptiveFullscreen();

            // 1. 方块注册表
            _registry = BlockRegistryLoader.Load();

            // 2. 物品 + 配方
            var items = ItemDatabaseLoader.Load();
            var recipes = ItemDatabaseLoader.LoadRecipes(items);

            // 3. 世界 + 生成器
            _world = new World();
            CurrentWorld = _world;
            var generator = new WorldGenerator((int)seed);

            // 4. 材质库
            _materials = BlockMaterialLibrary.Load(_registry, BlockRegistryLoader.TextureDirectory);

            // 5. 区块视图索引
            Transform worldRoot = ResolveWorldRoot();
            _views = new ChunkViewRegistry(worldRoot, _world, _registry, _materials);

            // 6. 流式加载器（m4 B1：带存档 regionsDir，卸载前把脏区块落盘到 region 文件）
            string saveRoot = Path.Combine(Application.persistentDataPath, "worlds");
            string regionsDir = Path.Combine(saveRoot, seed.ToString(), "regions");
            _streamer = new ChunkStreamer(_world, generator, _registry, _views, seed, regionsDir);

            // 7. 玩家上下文（背包 / 生命 / 时间 / 物品 / 经验 / 死亡）
            _playerContext = gameObject.AddComponent<PlayerContext>();
            _playerContext.Items = items;
            _playerContext.Recipes = recipes;
            _playerContext.Inventory = new PlayerInventory();
            _playerContext.Health = new Health(20);
            _playerContext.Time = new TimeOfDay();
            _playerContext.Experience = new Experience();
            _playerContext.Death = new DeathSystem();
            _playerContext.Inventory.SetMaxStackLookup(id => items.TryGetByNumericId(id, out var d) ? d.MaxStack : 64);

            // 默认热键栏第 0 格预填一个基础方块物品：让首次进游戏 hotbar 不空，能立刻看到挖到的方块。
            // brief 要求 "dirt"，但物品库里只有 plank / cobblestone / coal 等基础物品，
            // 没有单独的 dirt 物品（dirt 只是 block）。这里用 plank（numericId=1001）作为等价起点。
            if (items.TryGetById("plank", out var starterDef))
            {
                _playerContext.Inventory.SetSlot(0, new ItemStack(starterDef.NumericId, 64));
            }

            // 8. 玩家控制器
            // 出生 Y 现算（m5 A2）：地表 +2 格落地，根治固定 Y=120 的出生摔落伤害。
            // 序列化字段只取 X/Z 作水平出生点；Y 忽略场景里保存的旧值（Preview.unity
            // 序列化过 y=120，信它就退回老 bug）。
            int spawnSurfaceY = generator.SurfaceHeightAt((int)spawnPosition.x, (int)spawnPosition.z);
            _player = GetComponent<PlayerController>() ?? gameObject.AddComponent<PlayerController>();
            _player.Bind(_world, _registry,
                new Float3(spawnPosition.x, spawnSurfaceY + 2f, spawnPosition.z));

            // 9. 方块交互
            _interaction = GetComponent<BlockInteraction>() ?? gameObject.AddComponent<BlockInteraction>();
            _interaction.Bind(_world, _registry, _views, transform);
            // X2 fix-up：把方块→物品掉落表注入 BlockInteraction，让挖方块 spawn ItemDropEntity。
            // 数据来自 StreamingAssets/blocks/drops/block_drops.json。
            try
            {
                _interaction.SetBlockDrops(BlockDropsLoader.Load(items));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[WorldBootstrap] 加载 block_drops.json 失败：{ex.Message}。挖方块不掉落。");
            }

            // 10. 手部
            _hand = gameObject.AddComponent<HandController>();

            // 11. 战斗
            _combat = gameObject.AddComponent<CombatController>();
            _combat.Player = _player;
            _combat.Hand = _hand;
            // m9 A1 fix1（I2）：注入世界与方块表——攻击命中前的体素视线复核要用
            // （chunk mesh 无 Physics collider，mob 射线会穿墙）。
            _combat.World = _world;
            _combat.Registry = _registry;

            // 12. 动物系统
            _mobManager = gameObject.AddComponent<MobManager>();
            // 加载 MobSpawnRules 并把 WorldGenerator 注入 MobManager，让运行时刷怪
            // 按 biome + 光照 + seed 决定（而非写死概率）。
            var spawnRules = MobSpawnRulesLoader.TryLoad();
            _mobManager.Bind(_world, _playerContext.Time, transform, generator, spawnRules);

            // X4.5 fix-up：把 JSON 驱动的概率掉落表（mobs/drop_tables.json）注入 MobAI。
            // 之前 MobAI.cs:48 走静态 Items.ItemDropTable.Drop（count=1 hardcoded），僵尸死亡
            // 拿不到 [0,2] rotten_flesh + 5% iron_ingot（spec D4/D5 实际未生效）。TryLoad 找不到
            // 文件时回退到静态路径，保证游戏可启动但掉落仍按旧规则——与 BlockDropsLoader 风格一致。
            try
            {
                var dropPath = Path.Combine(Application.streamingAssetsPath, "mobs", "drop_tables.json");
                if (File.Exists(dropPath))
                {
                    MobAI.DropTable = MyWorld.Core.Entities.MobDropTable.Load(dropPath);
                }
                else
                {
                    Debug.LogWarning($"[WorldBootstrap] 未找到 {dropPath}，运行时 MobAI 走静态 Items.ItemDropTable.Drop");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[WorldBootstrap] 加载 drop_tables.json 失败：{ex.Message}。运行时 MobAI 走静态路径。");
            }

            // 13. 时间 + 水
            var sun = GameObject.Find("方向光");
            if (sun != null)
            {
                var cycle = gameObject.AddComponent<MyWorld.Unity.Environment.DayNightCycle>();
                cycle.SunLight = sun.GetComponent<Light>();
            }
            gameObject.AddComponent<MyWorld.Unity.Environment.WaterRenderer>();

            // 14. 树苗右键长成树（plan-3b）
            var sapling = gameObject.AddComponent<MyWorld.Unity.Environment.SaplingGrowth>();
            sapling.Bind(_world, _registry, _views);

            // 15. 红石系统（plan-3b）
            var redstone = gameObject.AddComponent<MyWorld.Unity.Environment.RedstoneSystem>();
            redstone.Bind(_world, _views, _player);

            // 16. 玩家身体（part2 任务 B1）
            gameObject.AddComponent<MyWorld.Unity.Player.PlayerVisual>();

            // 17. 第三人称相机（plan-3c）
            gameObject.AddComponent<MyWorld.Unity.Player.CameraThirdPerson>();

            // 18. 经验条 + 死亡画面（plan-3c）
            gameObject.AddComponent<MyWorld.Unity.UI.ExperienceBarUi>();
            gameObject.AddComponent<MyWorld.Unity.UI.DeathScreenUi>();

            // 19. 村民 + 交易 UI（plan-3d）
            // X3 fix-up：把 MobSpawnRules + WorldGenerator 也注入 VillagerManager，
            // 让刷怪决策走 MobSpawnRules.PickKind 数据驱动（Plains+Forest 限制生效）。
            var villagerMgr = gameObject.AddComponent<MyWorld.Unity.Combat.VillagerManager>();
            villagerMgr.Bind(_world, _playerContext.Time, transform, generator, spawnRules);
            gameObject.AddComponent<MyWorld.Unity.UI.TradeUi>();

            // 20. 附魔 UI（plan-3d）
            gameObject.AddComponent<MyWorld.Unity.UI.EnchantingUi>();

            // 21. 熔炉系统（plan-3 task B5：FurnaceSystem 是 Core 类，挂在 PlayerContext 上）
            if (_playerContext.FurnaceSystem == null)
                _playerContext.FurnaceSystem = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);

            // 22. 熔炉 UI（plan-3 task B5：挂 Player 上并 Bind FurnaceSystem）
            var furnaceUi = gameObject.AddComponent<MyWorld.Unity.UI.CraftingFurnaceUi>();
            furnaceUi.Bind(_playerContext.FurnaceSystem);

            // 23. 合成背包 UI（plan-3 task B6：Bind RecipeDatabase；优先用现成组件，重复 AddComponent 会双倍 OnGUI）
            var invUi = GetComponent<MyWorld.Unity.UI.CraftingInventoryUi>()
                        ?? gameObject.AddComponent<MyWorld.Unity.UI.CraftingInventoryUi>();
            if (_playerContext.Recipes != null) invUi.Bind(_playerContext.Recipes);

            // 24. 任务事件总线（m6 C2）：挂在 PlayerContext 同物体上，加载首章任务链。
            // 挖/拾/合/烧/夜五事件源经 QuestEventBus.Instance?.Raise 喂给它；
            // 链文件缺失 / 坏 JSON 只 warn，Quests 保持 null = 事件转发 no-op，游戏照常玩。
            // m6 C4 起必须放在 TryRestore **之前**：任务进度恢复在 TryRestore 内经
            // QuestEventBus.Instance.Quests.Restore 落进 bus.Quests，总线还没绑就等于跳过。
            var questBus = gameObject.AddComponent<MyWorld.Unity.Gameplay.QuestEventBus>();
            try
            {
                string chapterPath = Path.Combine(Application.streamingAssetsPath, "quests", "chapter1.json");
                questBus.Bind(_playerContext, File.Exists(chapterPath)
                    ? MyWorld.Core.Quests.QuestSystem.LoadChapter(chapterPath)
                    : null);
                if (!File.Exists(chapterPath))
                {
                    Debug.LogWarning($"[WorldBootstrap] 未找到 {chapterPath}，任务链不生效（事件转发 no-op）。");
                }
            }
            catch (System.Exception ex)
            {
                questBus.Bind(_playerContext, null);
                Debug.LogWarning($"[WorldBootstrap] 加载 chapter1.json 失败：{ex.Message}。任务链不生效。");
            }

            // 25. 存档服务（m4 B4：30s 自动 + 退出保存；启动时恢复玩家/时间/熔炉/掉落物/任务链）。
            // 顺序关键：必须等 PlayerContext / player / FurnaceSystem / 任务总线全部建好之后再 TryRestore——
            // 恢复的位置 Y 直接用存档值，streamer 半径内的区块会在 warmup 内生成，玩家不会在空气里下落。
            var saveLoad = gameObject.AddComponent<MyWorld.Unity.Persistence.SaveLoadService>();
            saveLoad.Bind(_world, _playerContext, _player, seed, saveRoot);
            saveLoad.TryRestore();
            // 总线先于 TryRestore 绑定（任务进度要恢复进 bus.Quests），Bind 里的跨夜观察基线
            // 因此取到的是恢复**前**的时刻——这里显式重置一次，否则「存档正午 → 读档深夜」的
            // 第一帧会被误判成跨过日出，白发一次 SurviveNight（ResetNightBaseline 正是为此公开）。
            questBus.ResetNightBaseline();

            // 26. UI 截图验证（m6 B1）：--ui-shot 启动参数 → 挂自动截图组件。
            // 无参数时 ShouldCapture 读一次 args 即返回 false，零开销。
            if (MyWorld.Unity.UiScreenshotOnArg.ShouldCapture(System.Environment.GetCommandLineArgs()))
                gameObject.AddComponent<MyWorld.Unity.UiScreenshotOnArg>();

            // 27. 帮助菜单（m6 B3）：H 键开关「怎么玩 + 设置」两页。挂玩家身上：
            // 打开时经 BlockInteraction.InputLocked + UiCursorGate 指针门抑制挖/放并解锁指针，
            // 灵敏度乘数也从这里找到 PlayerController。
            gameObject.AddComponent<MyWorld.Unity.UI.HelpMenuUi>();

            // 28. 任务目标卡（m6 C3）：右上角常驻「当前目标 + 进度」，完成瞬间变绿打勾 1s
            //     后切下一任务，全链完成显示「首章完成 ✓」5s 后隐藏。订阅总线完成钩子 +
            //     OnGUI 每帧经 QuestProgress 现读进度（无链时 GetHudText 返 null，不画卡）。
            var questHud = gameObject.AddComponent<MyWorld.Unity.UI.QuestHudUi>();
            questHud.Bind(questBus);

            // 29. 掉落物视图（m7 B1）：ItemDrops 增删同步建/毁 0.25 格小方块视图，
            //     挖到的掉落物看得见 + 吸附飞向玩家的动画可见。挂世界根节点下。
            var dropViews = gameObject.AddComponent<MyWorld.Unity.Items.ItemDropViewRegistry>();
            dropViews.Bind(_playerContext, worldRoot);

            // 30. Esc 暂停菜单（m8 B2）：Esc 开 / 关真暂停菜单——打开即 Time.timeScale=0
            //     冻结世界，关闭恢复 1；「设置」展开公共 SettingsPanelUi（与帮助菜单同一实例），
            //     「保存并退出」整体委托 HelpMenuUi 的 m7 A4 状态机（同步落盘 → 半秒停留 →
            //     退出，期间保持暂停）。死亡画面可见时 Esc 让位。挂在帮助菜单之后：
            //     Awake 用 GetComponent 复用同物体的 HelpMenuUi / SettingsPanelUi。
            gameObject.AddComponent<MyWorld.Unity.UI.PauseMenuUi>();
        }

        private void Update()
        {
            if (_streamer != null && _player != null)
            {
                _streamer.Tick(_player.State.Position);
            }
        }

        private void OnDestroy()
        {
            _materials?.Dispose();
            _materials = null;
            if (CurrentWorld == _world) CurrentWorld = null;
        }

        /// <summary>
        /// 按物理屏系统分辨率把窗口设为无边框全屏，修非 16:9 显示器两侧的黑边（m5 B3 #5）。
        /// 计算部分抽到 <see cref="ComputeAdaptiveResolution"/> 纯函数，EditMode 可测。
        /// </summary>
        private static void ApplyAdaptiveFullscreen()
        {
            var display = Display.main;
            // systemWidth/Height 是操作系统报告的物理屏分辨率（与当前窗口无关）；
            // 批处理 / 无头环境下可能拿不到有效值，纯函数返回 null 时跳过。
            var resolution = ComputeAdaptiveResolution(display.systemWidth, display.systemHeight);
            if (resolution == null) return;
            Screen.SetResolution(resolution.Value.Width, resolution.Value.Height, resolution.Value.Mode);
        }

        /// <summary>
        /// 由物理屏系统分辨率算启动分辨率与全屏模式：直接返回原生值 + FullScreenWindow，
        /// 让画面铺满整屏（不缩放、不出 pillarbox 黑边）。宽或高非正（0/负，常见于无头环境）
        /// 返回 null，调用方应跳过 SetResolution、沿用 ProjectSettings 默认值。
        /// </summary>
        public static (int Width, int Height, FullScreenMode Mode)? ComputeAdaptiveResolution(
            int systemWidth, int systemHeight)
        {
            if (systemWidth <= 0 || systemHeight <= 0) return null;
            return (systemWidth, systemHeight, FullScreenMode.FullScreenWindow);
        }

        /// <summary>拿到区块 GameObject 的父节点。由 <see cref="MyWorld.Unity.EditorTools.PreviewSceneBuilder"/> 在场景里建一个名为 <c>世界</c> 的空 GameObject，这里按名查找。</summary>
        private static Transform ResolveWorldRoot()
        {
            GameObject world = GameObject.Find("世界");
            if (world != null) return world.transform;
            Debug.LogWarning("[WorldBootstrap] 场景里没找到名为 '世界' 的 GameObject，运行时新建一个（玩家位置仍可能偏离预期）。");
            return new GameObject("世界").transform;
        }
    }
}
