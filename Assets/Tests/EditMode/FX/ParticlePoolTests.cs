#if UNITY_EDITOR
// m11 W3-4：粒子池机制——容量 64 复用（超发不新建视图）、挖掘碎屑抛物线 0.5s、
// 爆炸三帧 0.3s、附魔光柱 1s 生灭。
//   挂载点接线（事件 → 池响应的端到端）在 FxMountPointTests——C# 事件在声明类
//   之外只能 +=/-=，不能 ?.Invoke，所以端到端直接驱动真实发射端（BreakAt/DoEnchant/Detonate）。
// 依赖 UnityEngine，dotnet 链跑不动，整文件 #if UNITY_EDITOR 包裹（与 MobHitFeedbackTests 同款）。
// 时间注入：全部经 ParticlePool.Tick(dt) 直调步长驱动（EditMode 下 Update 不回调）。
using MyWorld.Unity.FX;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.FX
{
    [TestFixture]
    public class ParticlePoolTests
    {
        private GameObject _go;
        private ParticlePool _pool;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ParticlePool(测试)");
            _pool = _go.AddComponent<ParticlePool>(); // OnEnable 即订阅三个挂载点事件
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go); // OnDisable 退订
        }

        private static int CountKind(ParticlePool pool, ParticlePool.FxKind kind)
        {
            int n = 0;
            for (int i = 0; i < ParticlePool.Capacity; i++)
            {
                if (pool.SlotKind(i) == kind) n++;
            }
            return n;
        }

        [Test]
        public void 挖掘碎屑_一次4到6粒_抛物线0p5秒落地消失()
        {
            // 中心传方块中心 (x+0.5, y+0.5, z+0.5)，与 BlockInteraction 事件入口的取心一致
            int emitted = _pool.EmitDigDebris(new Vector3(8.5f, 70.5f, 8.5f), Color.gray, salt: 12345);
            Assert.That(emitted, Is.InRange(ParticlePool.DebrisMinPerBreak, ParticlePool.DebrisMaxPerBreak),
                "一次挖掘 4-6 粒");
            Assert.That(CountKind(_pool, ParticlePool.FxKind.Debris), Is.EqualTo(emitted), "全部落碎屑槽");

            // 抛物线：初速向上 → 先升后落
            _pool.Tick(0.05f);
            float apex = float.MinValue;
            for (int i = 0; i < ParticlePool.Capacity; i++)
            {
                if (_pool.SlotKind(i) == ParticlePool.FxKind.Debris)
                {
                    apex = Mathf.Max(apex, _pool.SlotViewPosition(i).y);
                }
            }
            Assert.That(apex, Is.GreaterThan(70.5f), "前 0.05s 碎屑被初速抛起（高于方块中心）");

            _pool.Tick(ParticlePool.DebrisDuration); // 走完 0.5s
            Assert.That(CountKind(_pool, ParticlePool.FxKind.Debris), Is.EqualTo(0),
                "0.5s 后碎屑全部释放回池");
        }

        [Test]
        public void 容量64_超发覆盖最旧_视图数不随超发增长()
        {
            for (int i = 0; i < 50; i++)
            {
                _pool.EmitDigDebris(new Vector3(8f, 70f, 8f), Color.gray, salt: i);
            }
            Assert.That(_pool.TotalEmitted, Is.GreaterThanOrEqualTo(200), "50 次挖掘累计发射 ≥200 粒");
            Assert.That(_pool.ActiveCount, Is.LessThanOrEqualTo(ParticlePool.Capacity),
                "同时存活粒数封顶 64");
            Assert.That(_pool.CreatedDebrisViews, Is.EqualTo(ParticlePool.Capacity),
                "懒建视图恰好 64 份后封顶——超发复用槽位，绝不新建");

            // 再超发 50 次：视图数不变（零 new 的池纪律）
            for (int i = 50; i < 100; i++)
            {
                _pool.EmitDigDebris(new Vector3(8f, 70f, 8f), Color.gray, salt: i);
            }
            Assert.That(_pool.CreatedDebrisViews, Is.EqualTo(ParticlePool.Capacity),
                "继续超发视图数不再增长");
            Assert.That(_pool.TotalEmitted, Is.GreaterThanOrEqualTo(400), "累计计数继续增长");
        }

        [Test]
        public void 爆炸_三帧0p3秒_帧序推进后释放()
        {
            _pool.PlayExplosion(new Vector3(0f, 70f, 0f), 3f);
            Assert.That(CountKind(_pool, ParticlePool.FxKind.Explosion), Is.EqualTo(1), "起播一张面片");
            Assert.That(_pool.SlotExplosionFrame(IndexOfKind(ParticlePool.FxKind.Explosion)), Is.EqualTo(0),
                "起始第 1 帧");

            int slot = IndexOfKind(ParticlePool.FxKind.Explosion);
            _pool.Tick(0.05f); // 0.05s → 帧 0
            Assert.That(_pool.SlotExplosionFrame(slot), Is.EqualTo(0), "0.05s 仍是第 1 帧");
            _pool.Tick(0.06f); // 0.11s → 帧 1
            Assert.That(_pool.SlotExplosionFrame(slot), Is.EqualTo(1), "0.11s 切第 2 帧");
            _pool.Tick(0.10f); // 0.21s → 帧 2
            Assert.That(_pool.SlotExplosionFrame(slot), Is.EqualTo(2), "0.21s 切第 3 帧");
            _pool.Tick(0.10f); // 0.31s ≥ 0.3s → 释放
            Assert.That(CountKind(_pool, ParticlePool.FxKind.Explosion), Is.EqualTo(0), "0.3s 后特效结束");
        }

        [Test]
        public void 附魔光柱_升起1秒后消失()
        {
            _pool.PlayEnchantColumn(new Vector3(0f, 70f, 0f));
            Assert.That(CountKind(_pool, ParticlePool.FxKind.EnchantColumn), Is.EqualTo(1), "起播光柱");

            _pool.Tick(0.5f);
            Assert.That(CountKind(_pool, ParticlePool.FxKind.EnchantColumn), Is.EqualTo(1), "0.5s 仍在升起/停留");
            _pool.Tick(0.51f); // 1.01s ≥ 1s
            Assert.That(CountKind(_pool, ParticlePool.FxKind.EnchantColumn), Is.EqualTo(0), "1s 后光柱消失");
        }

        [Test]
        public void tick零步长_noop不推进()
        {
            _pool.PlayExplosion(new Vector3(0f, 70f, 0f), 3f);
            _pool.Tick(0f);
            int slot = IndexOfKind(ParticlePool.FxKind.Explosion);
            Assert.That(_pool.SlotElapsed(slot), Is.EqualTo(0f), "dt=0（真暂停）不推进特效时间");
        }

        private int IndexOfKind(ParticlePool.FxKind kind)
        {
            for (int i = 0; i < ParticlePool.Capacity; i++)
            {
                if (_pool.SlotKind(i) == kind) return i;
            }
            return -1;
        }
    }
}
#endif
