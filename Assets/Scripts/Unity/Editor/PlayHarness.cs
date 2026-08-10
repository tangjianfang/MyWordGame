using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Player;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyWorld.Unity.Editor
{
    /// <summary>
    /// 无头验证玩家层：手动驱动若干帧，断言下落、前进、挖/放、流式加载。
    /// <para>
    /// 不进 Play 模式——手动调 <see cref="PlayerController.Tick"/> 与
    /// <see cref="ChunkStreamer.Tick"/> 即可，URP / 时间系统都不会动。
    /// </para>
    /// <para>
    /// 全部断言走 NUnit（<c>Assert.That</c> + <c>Is.*</c> 约束）。编辑器非播放态下
    /// 加载 <c>Assets/Scenes/Preview.unity</c>——里面挂着一个 <see cref="WorldBootstrap"/>，
    /// 借用它的 transform 作为视图索引的父节点。本任务另外起一套
    /// <see cref="ChunkStreamer"/> + <see cref="ChunkViewRegistry"/> + 玩家，与
    /// Bootstrap 的实例并存但互不干扰，确保验证的是“裸”链路而不是 Bootstrap 包装后的链路。
    /// </para>
    /// </summary>
    public static class PlayHarness
    {
        private const float Dt = 1f / 60f;
        private const int InitialFrames = 240;   // 自由落体停到地面
        private const int WalkFrames = 120;      // 向前走
        private const int FarWalkFrames = 1200;  // 走到能覆盖远端区块的位置
        private const int Seed = 42;             // 与 WorldBootstrap 默认值一致

        /// <summary>
        /// 玩家出生 Y。地表最高可达 <c>BaseHeight + HeightAmplitude = 68 + 40 = 108</c>
        /// （任何种子），brief 原值 <c>80</c> 会让玩家卡在地表之下的石头里——碰撞系统不会
        /// 自动把人往上推，落到洞底后四面被实心方块夹住，水平方向走不动。这里用 120 保证
        /// 任何种子下都从空中开始下落。
        /// </summary>
        private const float SpawnY = 120f;

        [MenuItem("MyWorld/无头验证玩家层")]
        public static void Run()
        {
            int failures = 0;
            BlockMaterialLibrary materials = null;
            GameObject playerGo = null;

            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Preview.unity", OpenSceneMode.Single);

                Scene scene = SceneManager.GetActiveScene();
                var bootstrap = Object.FindObjectOfType<WorldBootstrap>();
                Assert.IsNotNull(bootstrap, "场景里没有 WorldBootstrap");

                BlockRegistry registry = BlockRegistryLoader.Load();
                World world = new World();
                var generator = new WorldGenerator(Seed);
                materials = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);
                ChunkViewRegistry views = new ChunkViewRegistry(bootstrap.transform, world, registry, materials);
                ChunkStreamer streamer = new ChunkStreamer(world, generator, registry, views, Seed);

                playerGo = new GameObject("PlayerHarness");
                playerGo.transform.SetParent(bootstrap.transform);
                var controller = playerGo.AddComponent<PlayerController>();
                controller.Bind(world, registry, new Float3(0.5f, SpawnY, 0.5f));

                // 预加载：先把出生点周围 (LoadRadius=6 → 13×13=169 根) 的区块全建好，
                // 否则玩家在下落时会穿过还没加载的区块（World.GetBlock 把未加载视为空气），
                // 一头扎进地表之下的实心石头里，落地后被四面夹住完全无法水平移动。
                for (var i = 0; i < 200; i++)
                {
                    streamer.Tick(controller.State.Position);
                }

                // --- 断言 1：自由落体停在地面 ---
                for (var i = 0; i < InitialFrames; i++)
                {
                    controller.Tick(PlayerInput.None, Dt);
                    streamer.Tick(controller.State.Position);
                }

                PlayerState state = controller.State;
                Assert.That(state.IsGrounded, Is.True, "自由落体后应当停在地表上且 IsGrounded");
                Assert.That(state.Position.Y, Is.LessThan(SpawnY), $"应当已经从 y={SpawnY} 落到了地面附近");
                Assert.That(state.Velocity.Y, Is.EqualTo(0f).Within(1e-3f), "落地后竖直速度应归零");

                // --- 断言 2：向前走 ---
                float zBefore = controller.State.Position.Z;
                for (var i = 0; i < WalkFrames; i++)
                {
                    controller.Tick(new PlayerInput(0f, 1f, false, false), Dt);
                    streamer.Tick(controller.State.Position);
                }

                float traveled = controller.State.Position.Z - zBefore;
                Assert.That(traveled, Is.GreaterThan(1f), $"向前走 {traveled:F2}，应当 > 1 格");

                // --- 断言 3 / 4：挖一格再放回去，该段重建结果应变化 ---
                // 目标选在玩家前方、y=20 的实心石头里——y=20 始终位于"地表以下的纯石头段"
                // （最小地表 BaseHeight-HeightAmplitude=28，y=20 < 28-4=24 必然是石头）。
                // 整段完全被实心石头包住，原本 Rebuild 返回 false（无可见几何）；
                // 挖成空气后段内多 6 个洞面 → 返回 true；放回石头 → 返回 false。
                // 两种状态的返回值必然相反，断言可硬性检验。
                // brief 原选 (tx, ty-1, tz)（地表块），但那块四周仍是实心草方块，
                // 无论挖空还是放回，该段始终有可见几何，Rebuild 都返回 true，断言哑火。
                int tx = (int)controller.State.Position.X;
                int tz = (int)controller.State.Position.Z + 2;
                const int targetY = 20;
                ChunkPos targetChunk = new ChunkPos(VoxelCoords.WorldToChunk(tx),
                                                    VoxelCoords.WorldToChunk(tz));
                var targetRef = new SectionRef(targetChunk, VoxelCoords.SectionIndexForY(targetY));

                world.SetBlock(tx, targetY, tz, BlockIds.Stone);
                views.MarkBlockChanged(tx, targetY, tz);
                bool stoneRebuilt = views.Rebuild(targetRef);

                world.SetBlock(tx, targetY, tz, BlockIds.Air);
                views.MarkBlockChanged(tx, targetY, tz);
                bool airRebuilt = views.Rebuild(targetRef);

                world.SetBlock(tx, targetY, tz, BlockIds.Stone);
                views.MarkBlockChanged(tx, targetY, tz);
                bool stoneAgainRebuilt = views.Rebuild(targetRef);

                Assert.That(airRebuilt, Is.Not.EqualTo(stoneRebuilt),
                    $"挖空 (air) 与放回 (stone) 后该段重建结果应当不同；当前 stone={stoneRebuilt}, air={airRebuilt}");
                Assert.That(stoneAgainRebuilt, Is.EqualTo(stoneRebuilt),
                    $"再放回石头后该段重建结果应当与最初放石头时一致；stone={stoneRebuilt}, stoneAgain={stoneAgainRebuilt}");

                // --- 断言 5：走出 13×13 初始区域后能取到更远的区块（流式加载生效） ---
                // brief 原写法目标是 (20, 20)，但 sprint 5.59 m/s 需要 ~57 秒才能到，
                // 600 帧根本到不了。改为 1200 帧（~20 秒）走到 chunk 8 左右，校验 (10, 0)
                // 这一开始未被加载、跑路之后应被加载进来的区块——这才真的在测流式加载。
                ChunkPos farChunk = new ChunkPos(10, 0);
                for (var i = 0; i < FarWalkFrames; i++)
                {
                    controller.Tick(new PlayerInput(1f, 0f, false, true), Dt);
                    streamer.Tick(controller.State.Position);
                }

                Assert.That(world.TryGetChunk(farChunk, out _), Is.True,
                    $"流式加载应当已经生成 ({farChunk.X}, {farChunk.Z})");

                Debug.Log("[PlayHarness] 玩家层无头验证通过");
            }
            catch (AssertionException ex)
            {
                failures++;
                Debug.LogError($"[PlayHarness] 断言失败: {ex.Message}");
            }
            finally
            {
                if (playerGo != null)
                {
                    Object.DestroyImmediate(playerGo);
                }
                materials?.Dispose();
            }

            if (failures > 0)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                EditorApplication.Exit(0);
            }
        }
    }
}