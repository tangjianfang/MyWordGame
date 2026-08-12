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

        [Tooltip("玩家初始出生位置（世界坐标）。Y 应给到地表以上，避免落到山里。")]
        // 默认种子 42 下，原点地表约为 y=98；流式加载中心出队之前玩家所在的列可能是
        // 第 78 个才被加载的列，Y=80 会让玩家出生在石头里、四周被实心方块夹住。
        // Y=120 保证约 22 格的自由落体距离，物理 + 流式加载来得及把地面准备好。
        [SerializeField] private Vector3 spawnPosition = new Vector3(0.5f, 120f, 0.5f);

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

            // 6. 流式加载器
            _streamer = new ChunkStreamer(_world, generator, _registry, _views, seed);

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

            // 8. 玩家控制器
            _player = GetComponent<PlayerController>() ?? gameObject.AddComponent<PlayerController>();
            _player.Bind(_world, _registry, new Float3(spawnPosition.x, spawnPosition.y, spawnPosition.z));

            // 9. 方块交互
            _interaction = GetComponent<BlockInteraction>() ?? gameObject.AddComponent<BlockInteraction>();
            _interaction.Bind(_world, _registry, _views, transform);

            // 10. 手部
            _hand = gameObject.AddComponent<HandController>();

            // 11. 战斗
            _combat = gameObject.AddComponent<CombatController>();
            _combat.Player = _player;
            _combat.Hand = _hand;

            // 12. 动物系统
            _mobManager = gameObject.AddComponent<MobManager>();
            _mobManager.Bind(_world, _playerContext.Time, transform);

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
            var villagerMgr = gameObject.AddComponent<MyWorld.Unity.Combat.VillagerManager>();
            villagerMgr.Bind(_world, _playerContext.Time, transform);
            gameObject.AddComponent<MyWorld.Unity.UI.TradeUi>();

            // 20. 附魔 UI（plan-3d）
            gameObject.AddComponent<MyWorld.Unity.UI.EnchantingUi>();
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
