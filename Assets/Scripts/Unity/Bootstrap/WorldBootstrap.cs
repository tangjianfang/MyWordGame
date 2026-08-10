using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Player;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.World;
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
        [SerializeField] private Vector3 spawnPosition = new Vector3(0.5f, 80f, 0.5f);

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

            // 4. 视图索引：所有新建 / 重建的 GameObject 都挂到本组件所在 transform 下
            _views = new ChunkViewRegistry(transform, _world, _registry, _materials);

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
    }
}
