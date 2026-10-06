#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Persistence;
using MyWorld.Unity.Player;
// using System 引入的 object 与 UnityEngine.Object 冲突，显式别名消歧
using Object = UnityEngine.Object;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-4 B2：SaveNow 收集全部状态并落盘。
    /// （EditMode-only：MonoBehaviour + PlayerContext。）
    /// </summary>
    [TestFixture]
    public class SaveLoadServiceSaveTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, $"save-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_saveRoot, true);

        [Test]
        public void SaveNow_region文件损坏_返回false且LastSaveError可见()
        {
            // 评审 05 T-B1：region 层写失败原来不影响 SaveNow 返回值——同步路径恒 true，
            // 「保存并退出」误报成功照常退出，玩家的方块改动全部丢失且零提示。
            var go = new GameObject();
            try
            {
                var ctx = go.AddComponent<PlayerContext>();
                ctx.Inventory = new PlayerInventory();
                ctx.Health = new Health(20f);
                ctx.Time = new TimeOfDay { CurrentTick = 7000f };
                var player = go.AddComponent<PlayerController>();
                var service = go.AddComponent<SaveLoadService>();

                // 带脏块的世界（chunk (0,0)）+ 预置坏 region 文件（魔数错误）
                var world = new World();
                var generator = new MyWorld.Core.WorldGen.WorldGenerator(42);
                world.AddChunk(new ChunkPos(0, 0), generator.Generate(new ChunkPos(0, 0)));
                world.SetBlock(3, 64, 5, BlockIds.Bedrock);

                service.Bind(world, ctx, player, seed: 42, saveRoot: _saveRoot);
                Directory.CreateDirectory(service.RegionsDir);
                // 8 字节错误魔数：让解析走到魔数校验分支（3 字节会在 ReadInt32 抛流末尾异常）
                File.WriteAllBytes(Path.Combine(service.RegionsDir, "r.0.0.mwr"),
                    new byte[] { 0x58, 0x58, 0x58, 0x58, 0, 0, 0, 0 });

                bool ok = service.SaveNow(async: false);

                Assert.That(ok, Is.False,
                    "region 层写失败时同步保存必须返回 false——否则退出菜单误报已保存（评审 T-B1）");
                StringAssert.Contains("不是有效的区域文件", service.LastSaveError ?? "",
                    "region 错误要能经 LastSaveError 亮给玩家");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SaveNow_写入level_dat与regions目录()
        {
            var go = new GameObject();
            try
            {
                var ctx = go.AddComponent<PlayerContext>();
                // 显式赋值：PlayerContext 是单例，若上一个测试的实例尚未真正销毁，
                // Awake 会走 Destroy(this) 分支而不初始化各系统（同 PlayerPickupDamageTests）。
                ctx.Inventory = new PlayerInventory();
                ctx.Inventory.SetSlot(0, new ItemStack(1000, 5));
                ctx.Health = new Health(20f);
                ctx.Time = new TimeOfDay { CurrentTick = 7000f };
                var player = go.AddComponent<PlayerController>();

                var service = go.AddComponent<SaveLoadService>();
                service.Bind(new World(), ctx, player, seed: 42, saveRoot: _saveRoot);
                // m5 C3：SaveNow 默认把写盘交给后台执行器，这里注入同步执行器
                // 让「保存后文件立刻存在」的 m4 断言原样成立（语义不变）
                service.WriteExecutor = a => a();
                service.SaveNow();

                Assert.That(File.Exists(service.LevelDataPath), Is.True, "level.dat 应落地");
                Assert.That(Directory.Exists(service.RegionsDir), Is.True, "regions/ 目录应创建");
                Assert.That(service.LevelDataPath, Does.StartWith(_saveRoot), "档应落在 <saveRoot>/<seed>/ 下");

                var loaded = LevelDataCodec.Load(service.LevelDataPath);
                Assert.That(loaded.Seed, Is.EqualTo(42L), "seed 应写入 level.dat");
                Assert.That(loaded.TimeTick, Is.EqualTo(7000f), "世界时间应写入 level.dat");
                Assert.That(loaded.Player.Slots[0].ItemId, Is.EqualTo(1000), "背包应进档");
                Assert.That(loaded.Player.Slots[0].Count, Is.EqualTo(5), "背包物品数量应进档");
                Assert.That(loaded.Player.HealthMax, Is.EqualTo(20f), "生命上限应进档");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RestoreCoreState_替换状态并同步transform()
        {
            var go = new GameObject();
            try
            {
                var player = go.AddComponent<PlayerController>();
                var state = new PlayerState(
                    new Float3(12.5f, 64f, -8f), new Float3(0f, 1.5f, 0f), true);

                player.RestoreCoreState(state);

                Assert.That(player.transform.position.x, Is.EqualTo(12.5f), "transform.x 应同步");
                Assert.That(player.transform.position.y, Is.EqualTo(64f), "transform.y 应同步");
                Assert.That(player.transform.position.z, Is.EqualTo(-8f), "transform.z 应同步");
                Assert.That(player.State.Position.X, Is.EqualTo(12.5f), "State 应被整体替换");
                Assert.That(player.State.Velocity.Y, Is.EqualTo(1.5f), "速度应一并恢复");
                Assert.That(player.State.IsGrounded, Is.True, "着地标记应一并恢复");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ─── 异步存档（milestone-5 C3）──────────────────────────────────

        /// <summary>建一棵最小可保存的树（PlayerContext + PlayerController + SaveLoadService）。</summary>
        private SaveLoadService BuildService(GameObject go, World world, float timeTick)
        {
            var ctx = go.AddComponent<PlayerContext>();
            // 显式赋值：PlayerContext 单例残留时 Awake 不初始化各系统（同第一个测试的注释）
            ctx.Inventory = new PlayerInventory();
            ctx.Health = new Health(20f);
            ctx.Time = new TimeOfDay { CurrentTick = timeTick };
            var player = go.AddComponent<PlayerController>();
            var service = go.AddComponent<SaveLoadService>();
            service.Bind(world, ctx, player, seed: 42, saveRoot: _saveRoot);
            return service;
        }

        [Test]
        public void SaveNow_异步_快照主线程冻结_写盘交给执行器()
        {
            var go = new GameObject();
            try
            {
                var service = BuildService(go, new World(), 7000f);

                // 「捕获不执行」的执行器：SaveNow 返回时写盘尚未发生
                Action pending = null;
                int scheduled = 0;
                service.WriteExecutor = a => { scheduled++; pending = a; };

                service.SaveNow();
                Assert.That(scheduled, Is.EqualTo(1), "写盘动作应交给注入的执行器调度");
                Assert.That(pending, Is.Not.Null, "执行器应收到写盘动作");
                Assert.That(File.Exists(service.LevelDataPath), Is.False,
                    "执行器未执行前不应有任何文件落地——写盘确实被挪出了主线程");

                // 主线程在「后台写盘前」又改了状态：写出的必须是冻结时刻的快照
                go.GetComponent<PlayerContext>().Time.CurrentTick = 9999f;
                pending(); // 模拟后台线程此刻才写盘
                var loaded = LevelDataCodec.Load(service.LevelDataPath);
                Assert.That(loaded.TimeTick, Is.EqualTo(7000f),
                    "LevelData 应在主线程冻结为纯数据，写盘期间的状态变化不得混入");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveNow_异步_上一轮写盘中再次保存整轮跳过()
        {
            var go = new GameObject();
            try
            {
                var service = BuildService(go, new World(), 0f);
                Action pending = null;
                int scheduled = 0;
                service.WriteExecutor = a => { scheduled++; pending = a; };

                service.SaveNow();
                service.SaveNow(); // 上一轮仍在「写盘中」（pending 未执行）→ 应整轮跳过
                Assert.That(scheduled, Is.EqualTo(1), "后台写盘进行中再次 SaveNow 不得重叠调度第二轮");

                pending(); // 上一轮写完
                service.SaveNow();
                Assert.That(scheduled, Is.EqualTo(2), "写盘完成后下一轮保存恢复正常调度");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveNow_异步_写盘完成后主线程按版本守卫清脏()
        {
            var go = new GameObject();
            try
            {
                var world = new World();
                var chunkA = new ChunkPos(0, 0);
                var chunkB = new ChunkPos(3, 0);
                world.SetBlock(3, 64, 5, BlockIds.Stone);  // chunk A
                world.SetBlock(52, 64, 5, BlockIds.Dirt);  // chunk B

                var service = BuildService(go, world, 0f);
                Action pending = null;
                service.WriteExecutor = a => pending = a;

                service.SaveNow();                          // 主线程冻结 A、B 的快照
                world.SetBlock(4, 64, 5, BlockIds.Bedrock); // A 在保存窗口内又被改
                pending();                                   // 后台写盘完成：A、B 都落盘成功

                Assert.That(world.DirtyChunks, Is.EquivalentTo(new[] { chunkA, chunkB }),
                    "主线程确认之前，脏标记一个都不能少（后台只写不清）");
                service.ApplyPendingClears();               // 主线程确认写完 → 清脏
                Assert.That(world.DirtyChunks, Is.EquivalentTo(new[] { chunkA }),
                    "无新改动的 B 正常清脏；保存窗口内又被改的 A 保留脏下轮重存（版本守卫）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveNow_异步_执行器抛异常不卡死存档()
        {
            var go = new GameObject();
            try
            {
                var service = BuildService(go, new World(), 7000f);
                int calls = 0;
                service.WriteExecutor = a =>
                {
                    calls++;
                    if (calls == 1) throw new InvalidOperationException("调度失败"); // 第一轮：执行器本身抛
                    a();                                                              // 第二轮：正常执行
                };

                // 执行器抛异常不得炸到调用方（否则 30s 自动保存会把异常抛进 Update）；
                // 同时应有一条错误日志（EditMode 会把未声明的 LogError 判为失败）
                LogAssert.Expect(LogType.Error,
                    "[SaveLoadService] 写盘调度失败（本轮跳过，脏区块保留下轮重试）：调度失败");
                Assert.DoesNotThrow(() => service.SaveNow(), "执行器异常应由 SaveNow 内部兜住");
                Assert.That(File.Exists(service.LevelDataPath), Is.False, "写盘未执行，不应有文件落地");

                // 标志必须复位：否则重叠保护会把之后所有自动保存静默跳过
                service.SaveNow();
                Assert.That(calls, Is.EqualTo(2), "执行器异常后 _writeInProgress 应复位，下一轮保存仍可触发");
                Assert.That(File.Exists(service.LevelDataPath), Is.True, "第二轮保存应正常落盘");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnApplicationQuit_走同步路径立即落盘()
        {
            var go = new GameObject();
            try
            {
                var service = BuildService(go, new World(), 7000f);
                // 退出保存必须同步执行：执行器一旦被调用即失败
                service.WriteExecutor = a => Assert.Fail("退出保存必须同步落盘，不得再走后台执行器");

                var onQuit = typeof(SaveLoadService).GetMethod("OnApplicationQuit",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(onQuit, Is.Not.Null, "前置条件：OnApplicationQuit 应存在");
                onQuit.Invoke(service, null);

                Assert.That(File.Exists(service.LevelDataPath), Is.True,
                    "退出路径必须同步写完 level.dat（OnApplicationQuit 返回即落盘）");
                Assert.That(Directory.Exists(service.RegionsDir), Is.True, "regions/ 目录应创建");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
