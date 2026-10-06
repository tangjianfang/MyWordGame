// m12 P1：世界目录编目纯逻辑契约（dotnet / EditMode 双链同源）。
// 列表排序 / 回收站式删除 / 种子输入校验——主菜单三态的底座。
using System;
using System.IO;
using MyWorld.Core.Persistence;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    [TestFixture]
    public class WorldCatalogTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"worldcat-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        /// <summary>建一个世界目录并写 level.dat（mtime 可指定，驱动排序）。</summary>
        private void MakeWorld(long seed, DateTime? levelMtime = null)
        {
            string dir = Path.Combine(_root, seed.ToString());
            Directory.CreateDirectory(dir);
            string level = Path.Combine(dir, "level.dat");
            File.WriteAllText(level, "{}");
            if (levelMtime.HasValue)
            {
                File.SetLastWriteTimeUtc(level, levelMtime.Value);
            }
        }

        [Test]
        public void List_MissingRoot_ReturnsEmpty()
        {
            Assert.That(WorldCatalog.List(Path.Combine(_root, "nope")), Is.Empty);
        }

        [Test]
        public void List_SortsByLastPlayed_Descending()
        {
            var now = DateTime.UtcNow;
            MakeWorld(111, now.AddHours(-2));
            MakeWorld(222, now);
            MakeWorld(333, now.AddHours(-5));

            var worlds = WorldCatalog.List(_root);

            Assert.That(worlds.Count, Is.EqualTo(3));
            Assert.That(worlds[0].Seed, Is.EqualTo(222), "最近玩的排最前");
            Assert.That(worlds[1].Seed, Is.EqualTo(111));
            Assert.That(worlds[2].Seed, Is.EqualTo(333));
        }

        [Test]
        public void List_SkipsDeletedMarkerDirs()
        {
            MakeWorld(555);
            Directory.Move(
                Path.Combine(_root, "555"),
                Path.Combine(_root, "555.deleted-20260821000000-ab12"));

            Assert.That(WorldCatalog.List(_root), Is.Empty, "回收站式改名后的目录不出现在列表");
        }

        [Test]
        public void List_SkipsNonSeedAndEmptyDirs()
        {
            Directory.CreateDirectory(Path.Combine(_root, "backup"));      // 非种子名
            Directory.CreateDirectory(Path.Combine(_root, "777"));          // 空目录：无 level.dat 也无 regions

            Assert.That(WorldCatalog.List(_root), Is.Empty);
        }

        [Test]
        public void List_AcceptsRegionsOnlyNewWorld()
        {
            // 还没写过 level.dat、只落过脏区块的新世界也算（目录 mtime 兜底排序）
            Directory.CreateDirectory(Path.Combine(_root, "888", "regions"));

            var worlds = WorldCatalog.List(_root);

            Assert.That(worlds.Count, Is.EqualTo(1));
            Assert.That(worlds[0].Seed, Is.EqualTo(888));
        }

        [Test]
        public void Latest_ReturnsMostRecent_OrNull()
        {
            Assert.That(WorldCatalog.Latest(_root), Is.Null, "空根 → null");

            MakeWorld(111, DateTime.UtcNow.AddHours(-1));
            MakeWorld(222);
            Assert.That(WorldCatalog.Latest(_root).Seed, Is.EqualTo(222));
        }

        [Test]
        public void TryDelete_RenamesInsteadOfDeleting()
        {
            MakeWorld(999);

            bool deleted = WorldCatalog.TryDelete(_root, 999, out string renamedTo);

            Assert.That(deleted, Is.True);
            Assert.That(Directory.Exists(Path.Combine(_root, "999")), Is.False, "原目录不再出现");
            Assert.That(renamedTo, Does.StartWith(Path.Combine(_root, "999") + WorldCatalog.DeletedMarker),
                "改名保留 .deleted 标记（回收站式，文件还在盘上）");
            Assert.That(Directory.Exists(renamedTo), Is.True, "没有真删——防手滑误删孩子的世界");
            Assert.That(WorldCatalog.List(_root), Is.Empty, "列表同步消失");
        }

        [Test]
        public void TryDelete_MissingWorld_ReturnsFalse()
        {
            Assert.That(WorldCatalog.TryDelete(_root, 12345, out _), Is.False);
        }

        [TestCase("123", true, 123L)]
        [TestCase("-42", true, -42L)]
        [TestCase("  7  ", true, 7L)]
        [TestCase("", false, 0L)]
        [TestCase("   ", false, 0L)]
        [TestCase("abc", false, 0L)]
        [TestCase("12x3", false, 0L)]
        [TestCase("99999999999999999999", false, 0L)] // 超 long 溢出
        public void TryParseSeed_ValidationMatrix(string text, bool expectedOk, long expectedSeed)
        {
            bool ok = WorldCatalog.TryParseSeed(text, out long seed);

            Assert.That(ok, Is.EqualTo(expectedOk), $"输入「{text}」");
            if (expectedOk)
            {
                Assert.That(seed, Is.EqualTo(expectedSeed));
            }
        }

        [Test]
        public void TryDelete_文件被占用_返回false不抛()
        {
            // 评审 05 T-B2：目录被杀毒/备份工具占用时 Directory.Move 抛 IOException——
            // 此前无兜底，异常会在 OnGUI 每帧冒泡（删除按钮永久失效）。Windows 上用
            // 独占句柄锁住 level.dat 模拟占用，断言失败可控、释放后可重删成功。
            MakeWorld(42);
            string level = Path.Combine(_root, "42", "level.dat");
            using (new FileStream(level, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                bool ok = WorldCatalog.TryDelete(_root, 42, out string renamedTo);
                Assert.That(ok, Is.False, "占用时删除必须失败而非抛异常");
                Assert.That(renamedTo, Is.Null, "失败时输出参数清空（UI 不误判已删）");
                Assert.That(Directory.Exists(Path.Combine(_root, "42")), Is.True, "原目录保留");
            }

            Assert.That(WorldCatalog.TryDelete(_root, 42, out _), Is.True, "句柄释放后重删成功");
        }
    }
}
