// m11 W1-1（战斗）：Core/Combat/Explosion 的球判定 / 距离衰减伤害 / 确定性守卫。
// 苦力怕自爆的破坏与伤害全部委托这里（MobAI 新苦力怕分支），Core 纯函数可双链单测。
// 硬约束：确定性（整数范围枚举，不持随机数对象）、不破坏 y<0 与基岩/不可破坏方块。
using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Combat;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class ExplosionTests
    {
        [TearDown]
        public void TearDown()
        {
            CombatEvents.Reset();
            Explosion.BoundRegistry = null;
            Explosion.BoundDrops = null;
        }

        // ─── 距离衰减伤害 ───────────────────────────────────────────────────

        [Test]
        public void DamageAt_爆心最高六点_线性衰减_半径外归零()
        {
            var center = new Float3(0f, 64f, 0f);
            Assert.That(Explosion.DamageAt(center, center), Is.EqualTo(6f).Within(1e-4f),
                "爆心伤害 = MaxDamage 6（卡片「最高 6 点」）");
            Assert.That(Explosion.DamageAt(center, new Float3(1.5f, 64f, 0f)), Is.EqualTo(3f).Within(1e-3f),
                "半半径处线性衰减到一半（3 点）");
            Assert.That(Explosion.DamageAt(center, new Float3(3f, 64f, 0f)), Is.EqualTo(0f).Within(1e-4f),
                "半径处伤害归零");
            Assert.That(Explosion.DamageAt(center, new Float3(10f, 64f, 0f)), Is.EqualTo(0f),
                "半径外不受伤害");
        }

        // ─── 球判定：破坏清单 ───────────────────────────────────────────────

        [Test]
        public void CollectBlocks_只收半径内方块_基岩与y0以下跳过()
        {
            var world = new World();
            world.SetBlock(0, 64, 0, BlockIds.Stone);   // 方块中心 (0.5,64.5,0.5) = 爆心 → 收
            world.SetBlock(1, 64, 0, BlockIds.Stone);   // 中心距爆心 1.0 → 收
            world.SetBlock(0, 65, 0, BlockIds.Stone);   // 中心距爆心 1.0 → 收
            world.SetBlock(3, 64, 0, BlockIds.Stone);   // 中心距爆心 3.0 > 半径 1.6 → 不收
            world.SetBlock(0, 64, 1, BlockIds.Bedrock); // 基岩：半径内也不破坏（确定性守卫）
            world.SetBlock(0, -1, 0, BlockIds.Stone);   // y<0：爆炸不炸穿世界底

            var blocks = Explosion.CollectBlocks(world, new Float3(0.5f, 64.5f, 0.5f), 1.6f, registry: null);

            Assert.That(blocks.Count, Is.EqualTo(3),
                "半径 1.6 内只有三块石头可破坏（远处石头/基岩/y<0 石头均不算），实际 " + blocks.Count);
            Assert.That(blocks, Does.Contain((0, 64, 0)));
            Assert.That(blocks, Does.Contain((1, 64, 0)));
            Assert.That(blocks, Does.Contain((0, 65, 0)));
        }

        [Test]
        public void CollectBlocks_带注册表_不可破坏方块按硬度拦截()
        {
            // 无注册表时只按 BlockIds.Bedrock 拦；带注册表后任何 hardness<0 的方块都拦
            // （自动分配 numericId 的不可破坏方块不在内置常量表里，只有硬度这一条通用判据）
            var registry = BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true, ""hardness"": 1.0 }",
                @"{ ""id"": ""obsidian"", ""numericId"": 900, ""textures"": { ""all"": ""obsidian"" }, ""solid"": true, ""opaque"": true, ""hardness"": -1 }",
            });

            var world = new World();
            world.SetBlock(0, 64, 0, BlockIds.Stone);       // 可破坏
            world.SetBlock(1, 64, 0, BlockIds.Bedrock);     // 内置基岩（硬度 -1）
            world.SetBlock(0, 64, 1, 900);                  // 注册表内 hardness=-1 的自定义不可破坏方块

            var blocks = Explosion.CollectBlocks(world, new Float3(0.5f, 64.5f, 0.5f), 1.6f, registry);

            Assert.That(blocks.Count, Is.EqualTo(1), "硬度 <0 的两块（bedrock / obsidian）都应被拦下");
            Assert.That(blocks, Does.Contain((0, 64, 0)));
        }

        // ─── Detonate：世界改动 + 掉落 + 玩家伤害事件 ───────────────────────

        [Test]
        public void Detonate_半径内方块变空气_复用BlockDrops掉落()
        {
            var world = new World();
            for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
            {
                world.SetBlock(x, 63, z, BlockIds.Stone); // 石板地面（5×5 全在半径内：最远角 (±2,63,±2) 中心距 ≈2.9 < 3）
            }
            world.SetBlock(0, 63, 0, BlockIds.Bedrock);   // 中心一格换成基岩（确定性幸存）

            // 最小物品表 + block_drops：石头 → 石料 ×1（复用 BlockDrops 表的既有语义）
            var items = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""stone_drop"", ""displayName"": ""石料"", ""numericId"": 1900, ""maxStack"": 64 }",
            });
            var drops = BlockDrops.FromJson(new[]
            {
                @"{ ""blockId"": ""stone"", ""blockNumericId"": 1, ""drops"": [ { ""itemId"": ""stone_drop"", ""countMin"": 1, ""countMax"": 1 } ] }",
            }, items);

            int taken = 0;
            DamageEvent got = default;
            CombatEvents.OnDamageTaken += ev => { got = ev; taken++; };

            var center = new Float3(0.5f, 64.5f, 0.5f); // 石板上方 1 格处起爆（5×5 全在半径内）
            var playerPos = new Float3(2.5f, 64.5f, 0.5f); // 距爆心 2 格
            var result = Explosion.Detonate(world, center, playerPos, attackerEntityId: 26,
                radius: 3f, registry: null, drops: drops);

            // 25 块石头 - 1 块基岩 = 24 块被破坏
            Assert.That(result.DestroyedBlocks.Count, Is.EqualTo(24),
                "5×5 石板应破坏 24 块（中心基岩幸存），实际 " + result.DestroyedBlocks.Count);
            Assert.That(world.GetBlock(0, 63, 0), Is.EqualTo(BlockIds.Bedrock), "基岩确定性幸存");
            Assert.That(world.GetBlock(2, 63, 2), Is.EqualTo(BlockIds.Air), "角落石板已被炸空");
            Assert.That(result.Drops.Count, Is.EqualTo(24), "每块被破坏的石头都按 BlockDrops 表掉 1 个石料");

            // 玩家伤害：距离 2 → 6×(1-2/3) = 2 点，事件走 Environmental 源（MobManager 转发给 TakeDamage）
            Assert.That(taken, Is.EqualTo(1), "爆心半径内应发一次玩家伤害事件");
            Assert.That(got.Source, Is.EqualTo(DamageSource.Environmental));
            Assert.That(got.Amount, Is.EqualTo(2f).Within(1e-3f), "6×(1-2/3)=2 点距离衰减伤害");
            Assert.That(got.AttackerEntityId, Is.EqualTo(26), "伤害归属起爆的苦力怕");
        }

        [Test]
        public void Detonate_玩家在半径外_不发伤害事件()
        {
            var world = new World();
            world.SetBlock(0, 64, 0, BlockIds.Stone);
            int taken = 0;
            CombatEvents.OnDamageTaken += _ => taken++;

            var result = Explosion.Detonate(world, new Float3(0.5f, 64.5f, 0.5f),
                new Float3(50f, 64f, 50f), attackerEntityId: 0);

            Assert.That(result.PlayerDamage, Is.EqualTo(0f), "半径外玩家不受伤害");
            Assert.That(taken, Is.EqualTo(0), "伤害为零时不应发事件");
        }

        [Test]
        public void Detonate_同输入两次_破坏清单完全一致()
        {
            // 确定性守卫：整数范围枚举 + 方块中心判定，没有随机因子——
            // 同一份世界与参数跑两次，破坏清单（含顺序）必须一字不差
            var first = DetonateCopy();
            var second = DetonateCopy();
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i], Is.EqualTo(first[i]),
                    $"第 {i} 个破坏方块应一致（确定性），实际 {second[i]} vs {first[i]}");
            }
        }

        private static List<(int X, int Y, int Z)> DetonateCopy()
        {
            var world = new World();
            for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
            {
                world.SetBlock(x, 63, z, (x + z & 1) == 0 ? BlockIds.Stone : BlockIds.Dirt);
            }
            return Explosion.CollectBlocks(world, new Float3(0.5f, 65.5f, 0.5f), 3f, registry: null);
        }
    }
}
