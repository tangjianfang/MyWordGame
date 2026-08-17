#if UNITY_EDITOR
// m11 W1-1（战斗）：手持盾减伤 ×0.5 且耐久 -1（PlayerController.TakeDamage 接线）。
// 依赖 MyWorld.Unity 的 PlayerContext / PlayerController 与真实 items/*.json，
// dotnet 链跑不动，整个文件用 #if UNITY_EDITOR 包裹（与 GearBonusEquipTests 同款）。
using System.IO;
using System.Linq;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class ShieldMitigationTests
    {
        private GameObject _go;
        private PlayerController _player;
        private PlayerContext _ctx;
        private ItemDatabase _db;
        private int _shieldMaxDurability;

        [SetUp]
        public void SetUp()
        {
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            _db = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));
            _shieldMaxDurability = _db.GetById("shield").MaxDurability;

            _go = new GameObject("持盾玩家");
            _ctx = _go.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，显式初始化（与 GearBonusEquipTests 同款）
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _ctx.Items = _db;
            _player = _go.AddComponent<PlayerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private void Hold(string itemId)
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(_db.GetById(itemId).NumericId, 1));
            _ctx.Inventory.SelectedHotbarIndex = 0;
        }

        [Test]
        public void 手持盾_伤害减半_耐久减一()
        {
            Hold("shield");

            _player.TakeDamage(4f, null);

            Assert.That(_ctx.Health.Current, Is.EqualTo(18f).Within(1e-4f),
                "20 - 4×0.5 = 18（手持盾减伤一半）");
            var stack = _ctx.Inventory.GetSelected();
            Assert.That(stack.HasDurability, Is.True, "挨打后盾应启用耐久编码");
            Assert.That(stack.MaxDurability, Is.EqualTo(_shieldMaxDurability),
                $"耐久上限取物品表声明值（{_shieldMaxDurability}）");
            Assert.That(stack.CurrentDurability, Is.EqualTo(_shieldMaxDurability - 1),
                "挨一次打盾耐久 -1");
        }

        [Test]
        public void 手持盾_连续挨打_耐久连续递减()
        {
            Hold("shield");
            _player.TakeDamage(2f, null);
            _player.TakeDamage(2f, null);

            Assert.That(_ctx.Health.Current, Is.EqualTo(18f).Within(1e-4f), "两次 2 伤各减半 = 共扣 2 血");
            Assert.That(_ctx.Inventory.GetSelected().CurrentDurability,
                Is.EqualTo(_shieldMaxDurability - 2), "两次挨打耐久 -2");
        }

        [Test]
        public void 空手_伤害原样进入()
        {
            _player.TakeDamage(4f, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(16f).Within(1e-4f),
                "没拿盾不触发减半（防误伤既有平衡）");
        }

        [Test]
        public void 手持非盾物品_不触发减半()
        {
            Hold("plank"); // 普通物品不是盾
            _player.TakeDamage(4f, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(16f).Within(1e-4f),
                "只有盾走 ×0.5 通道，其他物品原样伤害");
        }

        [Test]
        public void 盾耐久尽_当场碎裂_碎裂那一下仍然减半()
        {
            var def = _db.GetById("shield");
            // 手工构造耐久只剩 1 的盾（Metadata：bits 8-15 上限 / bits 0-7 剩余）
            ushort lastHit = (ushort)((_shieldMaxDurability << 8) | 1);
            _ctx.Inventory.SetSlot(0, new ItemStack(def.NumericId, 1, lastHit));
            _ctx.Inventory.SelectedHotbarIndex = 0;

            _player.TakeDamage(4f, null);

            Assert.That(_ctx.Health.Current, Is.EqualTo(18f).Within(1e-4f),
                "耐久归零的那一下仍然减半（先结算伤害再碎盾）");
            Assert.That(_ctx.Inventory.GetSelected().IsEmpty, Is.True,
                "耐久 1→0 盾碎成空格（与镐碎裂同语义）");
        }
    }
}
#endif
