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
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Persistence;
using MyWorld.Unity.Player;

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
    }
}
#endif
