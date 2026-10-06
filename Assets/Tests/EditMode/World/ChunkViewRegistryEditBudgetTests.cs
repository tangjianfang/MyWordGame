#if UNITY_EDITOR
// 评审 02#3/03 B-3：编辑触发的段重建分帧契约——MarkBlockChanged 只把受牵连段
// 记入待建集合（不当场重建），DrainPendingEdits 按毫秒预算消费、耗尽留待下帧。
// 真实段重建（材质/网格上传）由 PlayHarness 与实机覆盖；本文件用 budget=0 的
// 零工作路径钉住「不当场重建」语义（旧实现在这里会立即 Rebuild，null 材质下即炸）。
using MyWorld.Core.Voxel;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.World
{
    [TestFixture]
    public class ChunkViewRegistryEditBudgetTests
    {
        [Test]
        public void MarkBlockChanged_不当场重建_进待建集合()
        {
            var world = new World();
            var host = new GameObject("registry");
            try
            {
                var registry = new ChunkViewRegistry(host.transform, world, null, null);
                world.SetBlock(8, 70, 8, BlockIds.Stone);

                registry.MarkBlockChanged(8, 70, 8);

                Assert.That(registry.PendingEditCount, Is.GreaterThan(0),
                    "受牵连段必须进待建集合（评审 02#3：挖一格 5.5ms×N 段不再当场挤爆单帧）");
                Assert.That(registry.ViewCount, Is.EqualTo(0),
                    "MarkBlockChanged 不当场重建——重建延到每帧预算内的 DrainPendingEdits");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void DrainPendingEdits_预算耗尽_剩余留待下帧()
        {
            var world = new World();
            var host = new GameObject("registry");
            try
            {
                var registry = new ChunkViewRegistry(host.transform, world, null, null);
                world.SetBlock(8, 70, 8, BlockIds.Stone);
                registry.MarkBlockChanged(8, 70, 8);

                int remaining = registry.DrainPendingEdits(0); // 预算 0：一段都不做

                Assert.That(remaining, Is.GreaterThan(0), "预算耗尽剩余留待下帧");
                Assert.That(registry.ViewCount, Is.EqualTo(0), "预算 0 不触发任何重建");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
#endif
