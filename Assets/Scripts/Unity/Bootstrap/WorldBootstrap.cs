using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Player;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.Streaming;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 把场景串成可玩：注册表 → 世界 + 生成器 → 区块视图索引 → 流式加载器 → 玩家 → 方块交互。
    /// 一切靠 <see cref="ChunkStreamer"/> 驱动，按玩家位置持续生成 / 卸载区块列。
    /// </summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
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

        private void Awake()
        {
            // 1. 注册表
            _registry = BlockRegistryLoader.Load();

            // 2. 世界 + 生成器
            _world = new World();
            var generator = new WorldGenerator((int)seed);

            // 3. 材质库（先建好，再让视图索引引用）
            _materials = BlockMaterialLibrary.Load(_registry, BlockRegistryLoader.TextureDirectory);

            // 4. 视图索引：所有新建 / 重建的 GameObject 挂在静态的 "世界" 根下，
            //    不要挂到本组件（也就是玩家根）——PlayerController 会把玩家 transform
            //    抬到 (0.5, 120, 0.5)，用玩家当父节点会让所有区块整体上抬 120 单位，
            //    相机朝下看地表（y≈98）就什么都没有。详见 PreviewSceneBuilder.CreateWorld 的注释。
            Transform worldRoot = ResolveWorldRoot();
            _views = new ChunkViewRegistry(worldRoot, _world, _registry, _materials);

            // 5. 流式加载器：每帧根据玩家位置决定生成 / 卸载哪些列
            _streamer = new ChunkStreamer(_world, generator, _registry, _views, seed);

            // 6. 玩家控制器：Bind 之前必须存在组件，但位置由 Bind 注入
            _player = GetComponent<PlayerController>() ?? gameObject.AddComponent<PlayerController>();
            _player.Bind(_world, _registry, new Float3(spawnPosition.x, spawnPosition.y, spawnPosition.z));

            // 7. 方块交互：射线拾取 + 挖 / 放
            _interaction = GetComponent<BlockInteraction>() ?? gameObject.AddComponent<BlockInteraction>();
            _interaction.Bind(_world, _registry, _views, transform);
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
        }

        /// <summary>
        /// 拿到区块 GameObject 的父节点。由 <see cref="MyWorld.Unity.EditorTools.PreviewSceneBuilder"/>
        /// 在场景里建一个名为 <c>世界</c> 的空 GameObject，这里按名查找。
        /// <para>
        /// 找不到时兜底再新建一个——保证运行时（不是 Editor 重建场景流程）也能跑；
        /// 但 <c>BuildSetup.Apply</c> 在打包前一定先调过 <c>PreviewSceneBuilder.Build()</c>，
        /// 所以正常路径下 <c>世界</c> 一定存在。
        /// </para>
        /// </summary>
        private static Transform ResolveWorldRoot()
        {
            GameObject world = GameObject.Find("世界");
            if (world != null)
            {
                return world.transform;
            }

            Debug.LogWarning("[WorldBootstrap] 场景里没找到名为 '世界' 的 GameObject，运行时新建一个（玩家位置仍可能偏离预期）。");
            return new GameObject("世界").transform;
        }
    }
}
