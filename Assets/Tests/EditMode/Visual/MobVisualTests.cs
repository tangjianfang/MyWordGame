#if UNITY_EDITOR
// Task D4: MobView 视觉回归测试。
// 验证 4 种 MobKind 的 mesh + 缩放：猪/牛/鸡/僵尸（spec line 173 type-specific 视觉）。
// 整个文件用 #if UNITY_EDITOR 包裹：dotnet 链跑纯 Core 测试时跳过，Unity EditMode 链跑。
// 视觉/AI 角色测试统一走 Unity EditMode，namespace 与同目录 MobViewPerKindTests 一致。
using MyWorld.Core.Entities;
using MyWorld.Unity.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class MobVisualTests
    {
        /// <summary>
        /// 猪应创建 Body + Head 两个 cube（spec line 173 "type-specific 视觉"）。
        /// 单 cube 路径是旧 Passive/Hostile 行为，新 MobKind.Pig 走 Body+Head 双段。
        /// </summary>
        [Test]
        public void SpawnPig_ProducesMesh()
        {
            var go = new GameObject("PigTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Pig);
                int cubeCount = 0;
                foreach (Transform child in go.transform) cubeCount++;
                Assert.That(cubeCount, Is.EqualTo(2), "猪应有 body + head 2 个 cube");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 僵尸 body 缩放 y = 1.8（远高于猪 0.6 / 牛 0.8 / 鸡 0.4）——视觉上区分「人形怪物」
        /// 与「四足动物」。断言 y > 1.5 留 0.3 buffer，覆盖后续微调。
        /// </summary>
        [Test]
        public void SpawnZombie_ProducesTallMesh()
        {
            var go = new GameObject("ZombieTest");
            try
            {
                var view = go.AddComponent<MobView>();
                view.Setup(MobKind.Zombie);
                // 僵尸应比猪高
                var body = go.transform.GetChild(0);
                Assert.That(body.localScale.y, Is.GreaterThan(1.5f), "僵尸身体 y > 1.5");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
