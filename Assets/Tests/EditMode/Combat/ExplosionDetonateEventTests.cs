// m11 W3-4：Explosion.AfterDetonate 纯视觉事件契约（爆心 + 破坏半径）。
// Unity 侧 ParticlePool 靠它播 fx-explosion 三帧特效——本测试守「起爆完成广播一次、
// 参数与调用一致」的接线契约。纯 Core 无 UnityEngine 依赖，dotnet 与 EditMode 双链同跑
//（文件不包 #if UNITY_EDITOR，与 ExplosionTests 同款纯逻辑测试）。
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class ExplosionDetonateEventTests
    {
        [Test]
        public void Detonate完成后_广播AfterDetonate_参数为爆心与半径()
        {
            var world = new World();
            world.SetBlock(8, 70, 8, BlockIds.Stone);

            int calls = 0;
            Float3 seenCenter = default;
            float seenRadius = -1f;
            void Handler(Float3 center, float radius)
            {
                calls++;
                seenCenter = center;
                seenRadius = radius;
            }

            MyWorld.Core.Combat.Explosion.AfterDetonate += Handler;
            try
            {
                var result = MyWorld.Core.Combat.Explosion.Detonate(
                    world, new Float3(8.5f, 70.5f, 8.5f), new Float3(200f, 90f, 200f),
                    attackerEntityId: 1, radius: 3f);

                Assert.That(result.DestroyedBlocks.Count, Is.GreaterThan(0),
                    "前置：爆心在实方块上，确有可炸目标");
                Assert.That(calls, Is.EqualTo(1), "起爆一次只广播一次");
                Assert.That(seenCenter.X, Is.EqualTo(8.5f), "事件爆心 X 与调用一致");
                Assert.That(seenCenter.Y, Is.EqualTo(70.5f), "事件爆心 Y 与调用一致");
                Assert.That(seenCenter.Z, Is.EqualTo(8.5f), "事件爆心 Z 与调用一致");
                Assert.That(seenRadius, Is.EqualTo(3f), "事件半径与调用一致");
            }
            finally
            {
                MyWorld.Core.Combat.Explosion.AfterDetonate -= Handler;
            }
        }
    }
}
