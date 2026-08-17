using MyWorld.Core.Lighting;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Lighting
{
    /// <summary>
    /// m11 W1-4：方块光源进 Core 光照传播。火把（blocks/torch.json lightEmission=14）这类
    /// 自发光方块通过体积的发光通道（<see cref="ArrayLightVolume.SetLightEmission"/>）登记，
    /// <see cref="LightPropagator.PropagateBlockLight"/> 全量扫描后洪泛——与天光
    /// <see cref="LightPropagator.PropagateSkyLight"/> 同一通道（同一字节光值），取更亮者。
    /// </summary>
    [TestFixture]
    public class TorchLightPropagationTests
    {
        private const int Size = 16;

        [Test]
        public void 火把发光14_本体满亮_邻格衰减1()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            volume.SetLightEmission(8, 8, 8, 14);

            LightPropagator.PropagateBlockLight(volume);

            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(14), "光源本体 = 发光强度");
            Assert.That(volume.GetLight(9, 8, 8), Is.EqualTo(13), "每格衰减 1");
            Assert.That(volume.GetLight(11, 8, 8), Is.EqualTo(11), "3 格外 = 14-3");
        }

        [Test]
        public void 发光14_5格外降到9_14格外全黑()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            volume.SetLightEmission(0, 8, 8, 14); // 贴边放，向 +X 有 15 格传播余量

            LightPropagator.PropagateBlockLight(volume);

            Assert.That(volume.GetLight(5, 8, 8), Is.EqualTo(9));
            Assert.That(volume.GetLight(13, 8, 8), Is.EqualTo(1));
            Assert.That(volume.GetLight(14, 8, 8), Is.EqualTo(0), "14 级光最远照到 13 格，第 14 格起全黑");
        }

        [Test]
        public void 光不穿不透明墙_绕拐角可达()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            for (var y = 0; y < Size; y++)
            {
                for (var z = 0; z < Size; z++)
                {
                    volume.SetOpaque(9, y, z, true); // x=9 整面墙
                }
            }

            volume.SetOpaque(9, 10, 8, false); // 墙上开一个洞
            volume.SetLightEmission(8, 8, 8, 15);

            LightPropagator.PropagateBlockLight(volume);

            // 路径 (8,8,8)→(8,9,8)→(8,10,8)→洞(9,10,8)→(10,10,8)：4 步 → 15-4=11
            Assert.That(volume.GetLight(10, 10, 8), Is.EqualTo(11), "BFS 能绕过拐角");
            // 洞后一格继续向下扩散：(10,10,8)→(10,9,8)→(10,8,8) 共 6 步 → 15-6=9。
            // 墙正后方不是全黑——光从洞进来后又「流」回来了，这正是 BFS 洪泛与直线投射的区别
            Assert.That(volume.GetLight(10, 8, 8), Is.EqualTo(9), "洞后一格下方应被绕进来的光照到（9 级）");
        }

        [Test]
        public void 先天光后方块光_天光不被覆盖_阴影里火把照样亮()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            // 给整层铺天光（模拟露天），再在顶上盖一片不透明屋顶制造阴影
            LightPropagator.PropagateSkyLight(volume);
            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(15), "露天处天光满级");

            for (var x = 0; x < Size; x++)
            {
                for (var z = 0; z < Size; z++)
                {
                    volume.SetOpaque(x, 12, z, true);
                }
            }

            // 重新铺天光：屋顶下方柱状直射归零，室内全黑
            LightPropagator.PropagateSkyLight(volume);
            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(0), "屋顶下没有天光");

            volume.SetLightEmission(8, 8, 8, 14);
            LightPropagator.PropagateBlockLight(volume);

            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(14), "阴影里靠火把补亮");
            Assert.That(volume.GetLight(8, 11, 8), Is.EqualTo(11), "屋顶正下方一格也在火把范围内");
        }

        [Test]
        public void 方块光只增不减_不覆盖更亮的天光()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            LightPropagator.PropagateSkyLight(volume);

            volume.SetLightEmission(8, 15, 8, 14); // 露天处放火把（该处天光 15）
            LightPropagator.PropagateBlockLight(volume);

            Assert.That(volume.GetLight(8, 15, 8), Is.EqualTo(15), "天光 15 不被火把 14 覆盖（同通道取 max）");
        }

        [Test]
        public void 无光源_传播后保持全黑()
        {
            var volume = new ArrayLightVolume(4, 4, 4);

            LightPropagator.PropagateBlockLight(volume);

            for (var x = 0; x < 4; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    for (var z = 0; z < 4; z++)
                    {
                        Assert.That(volume.GetLight(x, y, z), Is.EqualTo(0));
                    }
                }
            }
        }

        [Test]
        public void 两个火把_重叠处取更亮()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            volume.SetLightEmission(4, 8, 8, 14);
            volume.SetLightEmission(12, 8, 8, 14);

            LightPropagator.PropagateBlockLight(volume);

            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(10), "中点距两火把各 4 格：14-4=10（两路同值取其一）");
            Assert.That(volume.GetLight(5, 8, 8), Is.EqualTo(13), "距左火把 1 格处取更亮的一路");
        }
    }
}
