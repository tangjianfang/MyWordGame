using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class DurabilityTests
    {
        [Test]
        public void NewStack_HasNoDurability()
        {
            var s = new ItemStack(100, 1);
            Assert.That(s.HasDurability, Is.False);
            Assert.That(s.CurrentDurability, Is.EqualTo(0));
        }

        [Test]
        public void WithMaxDurability_EncodesCurEqualsMax()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(100);
            Assert.That(s.HasDurability, Is.True);
            Assert.That(s.CurrentDurability, Is.EqualTo(100));
            Assert.That(s.MaxDurability, Is.EqualTo(100));
        }

        [Test]
        public void DamageOnce_DecrementsCurrent()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(50);
            var damaged = s.DamageOnce();
            Assert.That(damaged.CurrentDurability, Is.EqualTo(49));
            Assert.That(damaged.MaxDurability, Is.EqualTo(50));
            Assert.That(damaged.Count, Is.EqualTo(1));
        }

        [Test]
        public void DamageOnce_BreaksAtZero()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(1);
            var broken = s.DamageOnce();
            Assert.That(broken.IsEmpty, Is.True);
        }

        [Test]
        public void DamageOnce_NonTool_ReturnsSameStack()
        {
            var s = new ItemStack(100, 1);    // 没有 max durability
            var damaged = s.DamageOnce();
            Assert.That(damaged, Is.EqualTo(s));
        }

        [Test]
        public void WithMaxDurability_ClampsAbove255()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(999);
            Assert.That(s.MaxDurability, Is.EqualTo(255));
        }

        // ─── m10 B1：WithDurabilityUsed（挖掉方块成功扣 1 点耐久） ───────────

        [Test]
        public void WithDurabilityUsed_MetadataZero_TreatsAsFullThenDecrements()
        {
            // 存量兼容：m10 之前的存档/预填/刚合成的镐 Metadata=0——0 视为满耐久，
            // 首次消耗时才落编码（先初始化再扣），绝不能把老工具当成已损坏
            var fresh = new ItemStack(1400, 1);
            Assert.That(fresh.HasDurability, Is.False, "前置：新构造的工具 Metadata=0");

            var used = fresh.WithDurabilityUsed(59);

            Assert.That(used.HasDurability, Is.True, "消耗一次后耐久位应已写入");
            Assert.That(used.MaxDurability, Is.EqualTo(59), "上限取传入的物品表 maxDurability");
            Assert.That(used.CurrentDurability, Is.EqualTo(58), "满耐久扣 1 = 58");
            Assert.That(used.Metadata, Is.EqualTo((ushort)((59 << 8) | 58)),
                "编码应为 (max<<8)|cur，沿用 m3 预留位段");
        }

        [Test]
        public void WithDurabilityUsed_ExistingMetadata_KeepsStoredMax()
        {
            // 一次写入终身有效：Metadata 已有上限时沿用存量值，
            // 后来改 JSON 的 maxDurability 不追溯旧工具（否则存档里的镐凭空变耐久）
            var worn = new ItemStack(1400, 1).WithMaxDurability(50).DamageOnce().DamageOnce();
            Assert.That(worn.CurrentDurability, Is.EqualTo(48), "前置：磨损到 48");

            var used = worn.WithDurabilityUsed(999);

            Assert.That(used.MaxDurability, Is.EqualTo(50),
                "上限应保持 Metadata 里存的 50，不被物品表新值覆盖");
            Assert.That(used.CurrentDurability, Is.EqualTo(47), "只扣 1 点");
        }

        [Test]
        public void WithDurabilityUsed_Repeated_UntilEmpty()
        {
            var stack = new ItemStack(1400, 1);
            for (int i = 0; i < 3; i++)
            {
                stack = stack.WithDurabilityUsed(3);
                if (!stack.IsEmpty)
                {
                    Assert.That(stack.MaxDurability, Is.EqualTo(3), "扣减不应动上限位");
                }
            }

            Assert.That(stack.IsEmpty, Is.True, "3 耐久用 3 次应归零损坏（变空）");
        }

        [Test]
        public void WithDurabilityUsed_MaxOne_BreaksOnFirstUse()
        {
            var used = new ItemStack(1400, 1).WithDurabilityUsed(1);
            Assert.That(used.IsEmpty, Is.True, "1 耐久的工具用一次即坏（满耐久扣 1 = 0 → 变空）");
        }

        [Test]
        public void WithDurabilityUsed_ZeroMax_ReturnsSameStack()
        {
            // 非工具 / 未声明 maxDurability 的物品：挖方块不应有任何变化
            var stack = new ItemStack(1003, 64);
            Assert.That(stack.WithDurabilityUsed(0), Is.EqualTo(stack));
        }

        [Test]
        public void Durability_RoundTrip_PreservesItemIdCountAndMaxByte()
        {
            // 编码 round-trip：任意次扣减后 id/count/上限位不漂移，只动低 8 位；
            // 用尽那一次直接变空（cur=0 状态不可达，坏工具不占背包格）
            var stack = new ItemStack(1402, 1).WithMaxDurability(250);
            for (int i = 0; i < 250; i++)
            {
                stack = stack.WithDurabilityUsed(250);
                if (stack.IsEmpty)
                {
                    break;
                }

                Assert.That(stack.ItemId, Is.EqualTo(1402), $"第 {i + 1} 次消耗后 itemId 不应变");
                Assert.That(stack.Count, Is.EqualTo(1), $"第 {i + 1} 次消耗后 count 不应变");
                Assert.That(stack.MaxDurability, Is.EqualTo(250), $"第 {i + 1} 次消耗后上限位不变");
                Assert.That(stack.CurrentDurability, Is.EqualTo(249 - i), $"第 {i + 1} 次消耗后剩余应线性递减");
            }

            Assert.That(stack.IsEmpty, Is.True, "250 耐久恰好用尽后变空");
        }
    }
}