#if UNITY_EDITOR
// 评审 04 R-8：items/recipes 是唯一没有解析级兜底的主数据源——坏 JSON 会打断
// WorldBootstrap.Awake（启动黑屏）。本文件钉死「坏 JSON 降级空表不抛」的契约。
// 目录参数为 internal 注入（loader 公开签名不动），dotnet 链跑不动 Unity 侧，
// 整文件 #if UNITY_EDITOR 包裹（与 BlockInteractionPlaceRoutingTests 同款）。
using System.IO;
using MyWorld.Core.Items;
using MyWorld.Unity.Bootstrap;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Bootstrap
{
    [TestFixture]
    public class ItemDatabaseLoaderTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Application.temporaryCachePath, $"items-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Load_坏JSON_降级空表不抛()
        {
            File.WriteAllText(Path.Combine(_dir, "broken.json"), "{ 这不是合法 JSON");

            var db = ItemDatabaseLoader.Load(_dir);

            Assert.That(db.TryGetById("任何物品", out _), Is.False,
                "坏 JSON 必须降级为空表——游戏能起（评审 04 R-8：此前炸 Awake 黑屏）");
        }

        [Test]
        public void LoadRecipes_坏JSON_降级空表不抛()
        {
            var db = ItemDatabase.FromJson(new string[0]);
            File.WriteAllText(Path.Combine(_dir, "broken.json"), "{ 这不是合法 JSON");

            var recipes = ItemDatabaseLoader.LoadRecipes(_dir, db);

            Assert.That(recipes, Is.Not.Null, "坏 JSON 必须降级为空配方表而非抛异常");
        }
    }
}
#endif
