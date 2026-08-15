#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m6 A1：UI 槽位贴图必须落在 StreamingAssets/ui（build 与编辑器同路径）。
    /// 旧代码走 dataPath/../Assets（编辑器专用），standalone build 里选中框退成
    /// 不透明纯白 1×1，整块盖住图标——实机「hotbar 图标不显示」的直接原因。
    /// </summary>
    [TestFixture]
    public class UiTextureFileTests
    {
        [TestCase("hotbar-slot.png")]
        [TestCase("hotbar-select.png")]
        public void StreamingUiTextures_Exist(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "ui", fileName);
            Assert.That(File.Exists(path), Is.True, $"StreamingAssets/ui/{fileName} 必须存在（build 才能加载）");
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            Assert.That(tex.width, Is.EqualTo(64));
            Assert.That(tex.height, Is.EqualTo(64));
        }

        [Test]
        public void SelectTexture_CenterIsTransparent()
        {
            // 选中框必须是「边框」不是实心块——实心会盖住图标（旧 bug 的回归守卫）
            string path = Path.Combine(Application.streamingAssetsPath, "ui", "hotbar-select.png");
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            var center = tex.GetPixel(32, 32);
            Assert.That(center.a, Is.LessThan(0.1f), "选中框中心必须透明，否则盖住物品图标");
        }
    }
}
#endif
