#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// B8：拾取掉落物 + 多源 TakeDamage（摔落 / 饥饿）。
    /// 全部走 PlayerController 的公开步进方法，不依赖 Update / Play 模式。
    /// m7 B1 起拾取是吸附语义（进 2.5m 圈 → 飞向玩家 → 贴脸 &lt;0.3m 才入包），
    /// 拾取测试显式传 dt 逐步步进。
    /// m5 A2 起伤害统一写 <see cref="PlayerContext"/>.Health（血条 / 存档唯一真源），
    /// 断言从 PlayerController 私有 int Health 改为读 ctx.Health.Current。
    /// </summary>
    public class PlayerPickupDamageTests
    {
        private GameObject _go;
        private PlayerController _player;
        private PlayerContext _ctx;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("玩家");
            _ctx = _go.AddComponent<PlayerContext>();
            // 显式赋值：PlayerContext 是单例，若上一个测试的实例尚未真正销毁，
            // Awake 会走 Destroy(this) 分支而不初始化 Inventory。
            _ctx.Inventory = new PlayerInventory();
            _ctx.HungerSystem = new HungerSystem();
            // EditMode 下 AddComponent 不触发 Awake，Health 需要显式初始化，
            // 否则默认 Current=0，伤害扣不进任何可见血条。
            _ctx.Health = new Health(20f);
            _player = _go.AddComponent<PlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void 吸附到位后掉落物入包()
        {
            _go.transform.position = new Vector3(10f, 5f, 10f);
            var drop = new ItemDropEntity(new ItemStack(7, 3), new Float3(10.5f, 5f, 10f));
            _ctx.ItemDrops.Add(drop);

            // m7 B1 吸附语义：0.5m 在吸附半径（2.5m）内但 > 完成距离（0.3m），
            // 第一帧只开始飞（Attracting），不立即入包
            int first = _player.PickupNearbyDrops(1f / 60f);
            Assert.AreEqual(0, first, "吸附刚开始未到位，本帧不应入包");
            Assert.IsTrue(drop.Attracting, "2.5m 内应标记吸附");
            Assert.AreEqual(1, _ctx.ItemDrops.Count, "掉落物应还在列表里飞行");

            // 手动步进到吸附到位（EditMode 不自动跑 Update）；600 帧上限防死循环
            int picked = first, steps = 0;
            while (_ctx.ItemDrops.Count > 0 && steps < 600)
            {
                picked += _player.PickupNearbyDrops(1f / 60f);
                steps++;
            }

            Assert.AreEqual(3, picked, "到位后累计应拾取 3 个");
            Assert.AreEqual(0, _ctx.ItemDrops.Count, "拾取后掉落物应从列表移除");
            Assert.IsNull(drop.Content, "掉落物内容应置空");
            Assert.AreEqual(3, _ctx.Inventory.GetSlot(0).Count, "背包首格应有 3 个");
            Assert.AreEqual(7, _ctx.Inventory.GetSlot(0).ItemId);
        }

        [Test]
        public void 范围外的掉落物不被拾取()
        {
            _go.transform.position = Vector3.zero;
            _ctx.ItemDrops.Add(new ItemDropEntity(new ItemStack(7, 3), new Float3(9f, 0f, 0f)));

            Assert.AreEqual(0, _player.PickupNearbyDrops(1f / 60f));
            Assert.AreEqual(1, _ctx.ItemDrops.Count, "9m 远超 2.5m 吸附半径，掉落物应保留");
        }

        [Test]
        public void 摔落超过三格按每格一点扣血()
        {
            // 起跳离地 → 记录最高点 20 → 落到 10（净落差 10）→ 着地结算 10-3=7 点伤害
            _player.Jump();
            _go.transform.position = new Vector3(0f, 20f, 0f);
            _player.TickFallDamage();
            _go.transform.position = new Vector3(0f, 10f, 0f);
            _player.TickFallDamage();
            _player.ForceGroundedForTest();
            _player.TickFallDamage();

            Assert.AreEqual(_ctx.Health.Max - 7, _ctx.Health.Current, "摔落 10 格应扣 7 点血");

            // 结算后峰值重置，再调一次不应重复扣血
            _player.TickFallDamage();
            Assert.AreEqual(_ctx.Health.Max - 7, _ctx.Health.Current, "着地后不应重复结算");
        }

        [Test]
        public void 饥饿归零每十秒扣一点血()
        {
            _ctx.HungerSystem = new HungerSystem { Hunger = 0, Saturation = 0f };

            _player.TickHungerDamage(9f);
            Assert.AreEqual(_ctx.Health.Max, _ctx.Health.Current, "不足 10 秒不扣血");

            _player.TickHungerDamage(1f);
            Assert.AreEqual(_ctx.Health.Max - 1, _ctx.Health.Current, "满 10 秒扣 1 点血");
        }

        [Test]
        public void 饥饿未归零不扣血()
        {
            _ctx.HungerSystem = new HungerSystem { Hunger = 10, Saturation = 5f };

            _player.TickHungerDamage(30f);

            Assert.AreEqual(_ctx.Health.Max, _ctx.Health.Current, "不饥饿时不应扣血");
        }
    }
}
#endif
