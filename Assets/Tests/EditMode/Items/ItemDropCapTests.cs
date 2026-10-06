// 评审 03 B-9：掉落物上限与过期的核心契约（dotnet / EditMode 双链同跑）。
using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class ItemDropCapTests
    {
        private static ItemDropEntity Drop(int id, bool attracting = false)
            => new ItemDropEntity(new ItemStack(id, 1), new Float3(id, 1f, 0f))
            {
                Attracting = attracting,
            };

        [Test]
        public void Prune_超限移除最旧_吸附中豁免()
        {
            var drops = new List<ItemDropEntity>();
            drops.Add(Drop(1, attracting: true)); // 最旧但正被吸附——豁免
            for (int i = 2; i <= ItemDropCap.MaxDrops + 3; i++) drops.Add(Drop(i));

            int removed = ItemDropCap.Prune(drops);

            Assert.That(removed, Is.EqualTo(3), "超限 3 个移除 3 个");
            Assert.That(drops.Count, Is.EqualTo(ItemDropCap.MaxDrops));
            Assert.That(drops[0].Attracting, Is.True, "吸附中的最旧实体保留（观感优先）");
            Assert.That(drops[1].Content.Value.ItemId, Is.EqualTo(5),
                "被裁的是最旧的非吸附实体：id 2/3/4 已移除，列表第二位是 5");
        }

        [Test]
        public void Prune_未超限零移除_全吸附放弃本轮()
        {
            var few = new List<ItemDropEntity> { Drop(1), Drop(2) };
            Assert.That(ItemDropCap.Prune(few), Is.EqualTo(0), "未超限不动");

            // 极端：全部吸附中 + 超限 → 本轮放弃（不裁飞行中的）
            var allAttracting = new List<ItemDropEntity>();
            for (int i = 0; i < ItemDropCap.MaxDrops + 2; i++) allAttracting.Add(Drop(i, attracting: true));
            Assert.That(ItemDropCap.Prune(allAttracting), Is.EqualTo(0),
                "全在吸附时放弃本轮裁剪——玩家正在捡，上限让位");
        }

        [Test]
        public void IsExpired_边界与保守豁免()
        {
            var drop = new ItemDropEntity(new ItemStack(1000, 1), new Float3(0, 1, 0));

            Assert.That(ItemDropCap.IsExpired(drop, 300f), Is.False,
                "SpawnTime=0（读档恢复/测试默认）不过期——保守：宁可多留不误删");

            drop.SpawnTime = 100f;
            Assert.That(ItemDropCap.IsExpired(drop, 399.9f), Is.False, "差一点没过期");
            Assert.That(ItemDropCap.IsExpired(drop, 400f), Is.True,
                "恰好 300s 过期（含等号，与拾取宽限期同款边界语义）");
            Assert.That(ItemDropCap.IsExpired(null, 100f), Is.False, "null 安全");
        }
    }
}
