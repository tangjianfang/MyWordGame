#if UNITY_EDITOR
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// Task C7：BlockInteraction 按 Biome 调整挖掘耗时。
    /// 山地石头硬 ×2，沙漠沙软 ×0.5，其它默认 1f。
    /// <para>
    /// 测的是 <see cref="BlockInteraction.BreakTime"/> 的纯函数语义：
    /// EditMode 测试不构造 MonoBehaviour / World / PlayerController，直接走静态方法。
    /// 与瞬时挖矿的实际节奏（<c>Update</c> 里 <c>SetBlock</c> 直接生效）正交——
    /// 真实 MiningTimed 可能以后接上，这里只先保证"给定方块 + 群系 → 期望时长"契约稳定。
    /// </para>
    /// </summary>
    [TestFixture]
    public class BlockInteractionBiomeTests
    {
        // ─── 石头：山地 ×2，其它 ×1 ──────────────────────────────────────────

        [Test]
        public void Stone_InMountains_TakesDoubleTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Mountains),
                Is.EqualTo(2f), "山地石头应硬 ×2（2 秒）");
        }

        [Test]
        public void Stone_InPlains_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Plains),
                Is.EqualTo(1f), "平原石头默认 1 秒");
        }

        [Test]
        public void Stone_InDesert_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Desert),
                Is.EqualTo(1f), "沙漠里石头保持 1 秒（沙漠只让沙软）");
        }

        [Test]
        public void Stone_InForest_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Stone, Biome.Forest),
                Is.EqualTo(1f), "森林石头默认 1 秒");
        }

        // ─── 沙：沙漠 ×0.5，其它 ×1 ──────────────────────────────────────────

        [Test]
        public void Sand_InDesert_TakesHalfTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Sand, Biome.Desert),
                Is.EqualTo(0.5f), "沙漠沙软 ×0.5");
        }

        [Test]
        public void Sand_InPlains_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Sand, Biome.Plains),
                Is.EqualTo(1f), "平原沙默认 1 秒");
        }

        [Test]
        public void Sand_InMountains_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Sand, Biome.Mountains),
                Is.EqualTo(1f), "山地沙默认 1 秒（山地规则只针对石头）");
        }

        [Test]
        public void Sand_InForest_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Sand, Biome.Forest),
                Is.EqualTo(1f), "森林沙默认 1 秒");
        }

        // ─── 其它基础方块任意群系都走默认 1f ───────────────────────────────

        [Test]
        public void Dirt_InAnyBiome_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Plains), Is.EqualTo(1f));
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Desert), Is.EqualTo(1f));
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Forest), Is.EqualTo(1f));
            Assert.That(BlockInteraction.BreakTime(BlockIds.Dirt, Biome.Mountains), Is.EqualTo(1f));
        }

        [Test]
        public void Grass_InAnyBiome_TakesDefaultTime()
        {
            Assert.That(BlockInteraction.BreakTime(BlockIds.Grass, Biome.Plains), Is.EqualTo(1f));
            Assert.That(BlockInteraction.BreakTime(BlockIds.Grass, Biome.Desert), Is.EqualTo(1f));
            Assert.That(BlockInteraction.BreakTime(BlockIds.Grass, Biome.Forest), Is.EqualTo(1f));
            Assert.That(BlockInteraction.BreakTime(BlockIds.Grass, Biome.Mountains), Is.EqualTo(1f));
        }

        [Test]
        public void Air_InAnyBiome_TakesDefaultTime()
        {
            // 空气没时间意义，但契约要稳定——挖空气返回 0 反而会让 Update 早退逻辑误判。
            Assert.That(BlockInteraction.BreakTime(BlockIds.Air, Biome.Plains), Is.EqualTo(1f));
            Assert.That(BlockInteraction.BreakTime(BlockIds.Air, Biome.Mountains), Is.EqualTo(1f));
        }
    }
}
#endif
