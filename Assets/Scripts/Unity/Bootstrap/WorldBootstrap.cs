using System.Diagnostics;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 里程碑 1 的验收场景：生成固定范围的区块并一次性全部建成网格。
    /// 跟随玩家的动态加载/卸载是后续里程碑的事，这里刻意不做。
    /// </summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        [SerializeField] private int seed = 12345;

        [Tooltip("以原点为中心，向四周各生成多少个区块。3 表示 7×7 共 49 根区块列。")]
        [SerializeField] private int chunkRadius = 3;

        private BlockMaterialLibrary _materials;

        private void Start()
        {
            BuildWorld();
        }

        /// <summary>
        /// 生成世界并建好全部网格。独立成公开方法，使编辑器的无头冒烟检查能在非播放态下跑同一条路径。
        /// </summary>
        public void BuildWorld()
        {
            var stopwatch = Stopwatch.StartNew();

            BlockRegistry registry = BlockRegistryLoader.Load();
            _materials = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);

            var world = new World();
            var generator = new WorldGenerator(seed);

            // 必须先把全部区块灌进 World 再建网格：建网格要采样邻区块才能剔除接缝面，
            // 边生成边建网格会让先建的那几根在接缝处多出一整面
            for (int x = -chunkRadius; x <= chunkRadius; x++)
            {
                for (int z = -chunkRadius; z <= chunkRadius; z++)
                {
                    var pos = new ChunkPos(x, z);
                    world.AddChunk(pos, generator.Generate(pos));
                }
            }

            long generateMs = stopwatch.ElapsedMilliseconds;
            int visible = BuildAllMeshes(world, registry);

            Debug.Log($"世界就绪：{world.LoadedChunkCount} 根区块列（生成 {generateMs} ms），" +
                      $"{visible} 个可见区块段（建网格 {stopwatch.ElapsedMilliseconds - generateMs} ms），" +
                      $"{registry.TextureNames.Count} 种贴图。");
        }

        private int BuildAllMeshes(World world, BlockRegistry registry)
        {
            var visible = 0;

            for (int x = -chunkRadius; x <= chunkRadius; x++)
            {
                for (int z = -chunkRadius; z <= chunkRadius; z++)
                {
                    var pos = new ChunkPos(x, z);
                    if (!world.TryGetChunk(pos, out ChunkColumn column))
                    {
                        continue;
                    }

                    for (var section = 0; section < VoxelCoords.SectionCount; section++)
                    {
                        if (!column.HasSection(section))
                        {
                            continue;
                        }

                        ChunkSectionView view = ChunkSectionView.Create(transform, pos, section);
                        if (view.Rebuild(world, registry, _materials))
                        {
                            visible++;
                        }
                        else
                        {
                            // 完全被包裹的段一个面都没有，留着只是白占一个 GameObject
                            DestroyObject(view.gameObject);
                        }
                    }
                }
            }

            return visible;
        }

        /// <summary>编辑器非播放态下 Destroy 不生效，必须走 DestroyImmediate。</summary>
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            _materials?.Dispose();
            _materials = null;
        }
    }
}
