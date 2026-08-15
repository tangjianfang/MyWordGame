#if UNITY_EDITOR
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// m5 A2：单血条统一。摔落 / 饥饿伤害必须写 <see cref="PlayerContext.Health"/>
    /// （血条 UI、死亡判定、存档的唯一真源），不再走 PlayerController 私有 int Health
    /// ——旧实现玩家挨了摔落伤但血条纹丝不动，「进角色莫名掉血」不可见也不可存档。
    /// </summary>
    [TestFixture]
    public class PlayerHealthUnificationTests
    {
        [Test]
        public void TakeDamage_WritesPlayerContextHealth()
        {
            var go = new GameObject();
            try
            {
                var ctx = go.AddComponent<PlayerContext>();
                ctx.Health = new MyWorld.Core.Entities.Health(20f);
                var player = go.AddComponent<PlayerController>();

                player.TakeDamage(4, null);

                Assert.That(ctx.Health.Current, Is.EqualTo(16f).Within(0.001f),
                    "伤害必须落到 PlayerContext.Health——血条和存档读的是它");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FallDamage_WritesPlayerContextHealth()
        {
            var go = new GameObject();
            try
            {
                var ctx = go.AddComponent<PlayerContext>();
                ctx.Health = new MyWorld.Core.Entities.Health(20f);
                var player = go.AddComponent<PlayerController>();

                // 模拟一次 8 格摔落（驱动方式与 PlayerPickupDamageTests.摔落超过三格 一致）：
                // Jump 离地（未绑定 World 时 _defaultIsGrounded=false）→ 记峰值 100
                // → 落到 92 → ForceGroundedForTest 切回着地 → 结算 (8-3)=5 伤
                player.Jump();
                player.transform.position = new Vector3(0f, 100f, 0f);
                player.TickFallDamage();
                player.transform.position = new Vector3(0f, 92f, 0f);
                player.ForceGroundedForTest();
                player.TickFallDamage();

                Assert.That(ctx.Health.Current, Is.EqualTo(15f).Within(0.001f),
                    "8 格摔落应扣 5 血并写入 PlayerContext.Health");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
