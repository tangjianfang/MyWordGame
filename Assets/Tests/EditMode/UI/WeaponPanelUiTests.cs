#if UNITY_EDITOR
// m13 W4 武器面板：R 键开关、武器清单分类（近战/弓/枪）、换装路由、点外关闭。
// 测试与 OnGUI 共用公共入口（EnumerateWeapons / ClickWeaponRow / ShouldCloseOnMouseDown），
// EditMode 下 Event.current 不可构造，走 raw 值版（照 DeathScreenUiTests / CraftingGridInteractionTests 模式）。
using System.Linq;
using System.Reflection;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    [TestFixture]
    public class WeaponPanelUiTests
    {
        // 用合成 numericId 隔离真实物品表——保证测试不依赖 StreamingAssets/items/*.json 加载路径
        private const int MeleeSword = 9001;   // 铁剑占位（AttackDamage > 0，Range = 0）
        private const int WoodenAxe = 9002;    // 木斧占位（AttackDamage > 0，Range = 0）
        private const int Bow = 9003;          // 弓占位（Range > 0，id 含 "bow"）
        private const int Musket = 9004;       // 火枪占位（Range > 0，id 含 "musket"）
        private const int Bullet = 9005;       // 子弹（弹药）
        private const int Plank = 9006;        // 木板（非武器：AttackDamage = 0，Range = 0）
        private const int Stone = 9007;        // 圆石（同上：非武器）

        private GameObject _go;
        private PlayerContext _ctx;
        private WeaponPanelUi _ui;
        private ItemDatabase _db;

        [SetUp]
        public void SetUp()
        {
            UiCursorGate.Reset();
            _db = ItemDatabase.FromJson(new[]
            {
                $@"{{ ""id"": ""w4_melee_sword"", ""numericId"": {MeleeSword}, ""maxStack"": 1, ""attackDamage"": 5 }}",
                $@"{{ ""id"": ""w4_wooden_axe"", ""numericId"": {WoodenAxe}, ""maxStack"": 1, ""attackDamage"": 7 }}",
                $@"{{ ""id"": ""w4_bow"", ""numericId"": {Bow}, ""maxStack"": 1, ""attackDamage"": 1, ""range"": 60 }}",
                $@"{{ ""id"": ""w4_musket"", ""numericId"": {Musket}, ""maxStack"": 1, ""attackDamage"": 6, ""range"": 25 }}",
                $@"{{ ""id"": ""w4_bullet"", ""numericId"": {Bullet}, ""maxStack"": 64 }}",
                $@"{{ ""id"": ""w4_plank"", ""numericId"": {Plank}, ""maxStack"": 64 }}",
                $@"{{ ""id"": ""w4_stone"", ""numericId"": {Stone}, ""maxStack"": 64 }}",
            });

            _go = new GameObject("武器面板");
            _ctx = _go.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，显式调 Awake 让 PlayerContext.Instance
            // 指向本测试的 _ctx。WeaponPanelUi.EnumerateWeapons / ClickWeaponRow 走
            // PlayerContext.Instance 拿 ctx，没有这一步直接返空列表（EnumerateWeapons）
            // 或返 false（ClickWeaponRow 越界分支）。Awake 内部若发现 Instance 已被占用
            // 会调 Destroy(this) —— EditMode 不允许；目前一个测试只建一个 PlayerContext，
            // 不会有双 Awake 冲突（TearDown DestroyImmediate → OnDestroy 清 Instance）。
            // 走反射是因为 WeaponPanelUiTests 没自带 InvokeAwake 助手（BlockInteraction 系
            // 的私有静态方法不跨文件复用）；BlockInteractionUseRoutingTests 同款做法。
            var ctxAwake = typeof(PlayerContext).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(ctxAwake, Is.Not.Null, "PlayerContext 应有私有 Awake");
            ctxAwake.Invoke(_ctx, null);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = _db;
            _ctx.Inventory.SetMaxStackLookup(
                id => _db.TryGetByNumericId(id, out var d) ? d.MaxStack : 64);
            _ui = _go.AddComponent<WeaponPanelUi>();
            // 默认 BulletNumericId = 1608（真实 bullet.json），测试用合成 ID 走属性覆盖
            _ui.BulletNumericId = Bullet;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            UiCursorGate.Reset();
        }

        private void PutInBag(int slot, int itemId, int count = 1)
            => _ctx.Inventory.SetSlot(slot, new ItemStack(itemId, count));

        // ─── Classify：武器分类谓词（公共静态，EditMode 直测） ───────────────

        [Test]
        public void Classify_近战刀剑斧_返回Melee()
        {
            Assert.That(WeaponPanelUi.Classify(_db.GetByNumericId(MeleeSword)), Is.EqualTo(WeaponPanelUi.WeaponKind.Melee));
            Assert.That(WeaponPanelUi.Classify(_db.GetByNumericId(WoodenAxe)), Is.EqualTo(WeaponPanelUi.WeaponKind.Melee));
        }

        [Test]
        public void Classify_弓_返回Bow_枪_返回Musket()
        {
            Assert.That(WeaponPanelUi.Classify(_db.GetByNumericId(Bow)), Is.EqualTo(WeaponPanelUi.WeaponKind.Bow));
            Assert.That(WeaponPanelUi.Classify(_db.GetByNumericId(Musket)), Is.EqualTo(WeaponPanelUi.WeaponKind.Musket));
        }

        [Test]
        public void Classify_非武器_返回null()
        {
            Assert.That(WeaponPanelUi.Classify(_db.GetByNumericId(Plank)), Is.Null, "木板无攻击/射程");
            Assert.That(WeaponPanelUi.Classify(_db.GetByNumericId(Stone)), Is.Null, "圆石同上");
            Assert.That(WeaponPanelUi.Classify(_db.GetByNumericId(Bullet)), Is.Null, "子弹不归类为武器（弹药）");
        }

        [Test]
        public void Classify_null安全_返回null()
        {
            Assert.That(WeaponPanelUi.Classify(null), Is.Null, "Classify 必须对 null 物品安全返回 null");
        }

        // ─── 开关 & 门登记（与现有模态 UI 同款） ─────────────────────────────

        [Test]
        public void SetOpen_登记指针门_关闭释放()
        {
            _ui.SetOpen(true);
            Assert.That(_ui.IsOpen, Is.True);
            Assert.That(UiCursorGate.IsOpen, Is.True, "R 面板是模态 UI，打开必须解锁指针");

            _ui.SetOpen(false);
            Assert.That(UiCursorGate.IsOpen, Is.False, "关闭必须把门位还回去（计数不泄漏）");
        }

        [Test]
        public void SetOpen_幂等_重复设同值无副作用()
        {
            _ui.SetOpen(true);
            _ui.SetOpen(true);
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(1), "重复开不开不开门（计数制幂等）");

            _ui.SetOpen(false);
            _ui.SetOpen(false);
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "重复关不把计数打成负");
        }

        // ─── 武器清单 ────────────────────────────────────────────────────────

        [Test]
        public void EnumerateWeapons_背包无武器_返回空()
        {
            Assert.That(_ui.EnumerateWeapons().Count, Is.EqualTo(0));
        }

        [Test]
        public void EnumerateWeapons_混合武器与非武器_只列出武器_按槽位升序()
        {
            // 故意打乱顺序，断言按 SlotIndex 升序——hotbar 0..8 在主背包 9..35 之前
            PutInBag(15, Plank);     // 木板在主背包 15 槽
            PutInBag(3, Bow);        // 弓在 hotbar 3 槽
            PutInBag(20, MeleeSword);// 剑在主背包 20 槽
            PutInBag(0, Stone);      // 圆石在 hotbar 0 槽
            PutInBag(28, Musket);    // 枪在主背包 28 槽
            PutInBag(10, WoodenAxe); // 斧在主背包 10 槽

            var rows = _ui.EnumerateWeapons();
            Assert.That(rows.Count, Is.EqualTo(4), "4 把武器（弓/剑/斧/枪），木板/圆石/子弹过滤");
            Assert.That(rows.Select(r => r.SlotIndex).ToArray(),
                Is.EqualTo(new[] { 3, 10, 20, 28 }), "按槽位升序");
            Assert.That(rows.Select(r => r.Kind).ToArray(),
                Is.EqualTo(new[] {
                    WeaponPanelUi.WeaponKind.Bow,
                    WeaponPanelUi.WeaponKind.Melee,
                    WeaponPanelUi.WeaponKind.Melee,
                    WeaponPanelUi.WeaponKind.Musket,
                }), "分类正确：弓→Bow / 剑+斧→Melee / 枪→Musket");
        }

        [Test]
        public void EnumerateWeapons_空背包与未绑定上下文_安全返回空()
        {
            // 拆掉 ctx：EnumerateWeapons 必须能在 ctx = null 时不炸
            Object.DestroyImmediate(_go);
            _go = new GameObject("武器面板-noctx");
            _ui = _go.AddComponent<WeaponPanelUi>();
            Assert.That(_ui.EnumerateWeapons().Count, Is.EqualTo(0), "无 PlayerContext 时返回空清单");
            Object.DestroyImmediate(_go);
        }

        // ─── 换装路由（点击行 → hotbar 选中槽） ───────────────────────────────

        [Test]
        public void ClickWeaponRow_武器在主背包_也允许选中为SelectedHotbarIndex()
        {
            PutInBag(20, MeleeSword);
            _ctx.Inventory.SelectedHotbarIndex = 0; // 前置：选中 hotbar 0

            bool ok = _ui.ClickWeaponRow(0);

            Assert.That(ok, Is.True);
            Assert.That(_ctx.Inventory.SelectedHotbarIndex, Is.EqualTo(20),
                "剑在主背包 20 槽——点行后选中态指向源槽位（与 hotbar 数字键同语义）");
        }

        [Test]
        public void ClickWeaponRow_选中槽已是该武器_不抖动()
        {
            PutInBag(3, Bow);
            _ctx.Inventory.SelectedHotbarIndex = 3;

            bool ok = _ui.ClickWeaponRow(0);

            Assert.That(ok, Is.True);
            Assert.That(_ctx.Inventory.SelectedHotbarIndex, Is.EqualTo(3),
                "已选中同槽不重复写——避免下游 GearBonus / 选中框不必要刷新");
        }

        [Test]
        public void ClickWeaponRow_越界_返回false_不动选中态()
        {
            PutInBag(0, Bow);
            int original = 7;
            _ctx.Inventory.SelectedHotbarIndex = original;

            Assert.That(_ui.ClickWeaponRow(99), Is.False);
            Assert.That(_ctx.Inventory.SelectedHotbarIndex, Is.EqualTo(original), "越界不动选中态");
        }

        [Test]
        public void ClickWeaponRow_背包空_返回false()
        {
            int original = 4;
            _ctx.Inventory.SelectedHotbarIndex = original;

            Assert.That(_ui.ClickWeaponRow(0), Is.False, "零行越界同款——拒绝");
            Assert.That(_ctx.Inventory.SelectedHotbarIndex, Is.EqualTo(original));
        }

        // ─── 点外关闭（m13 W4 模态 UI 统一行为） ───────────────────────────────

        [Test]
        public void ShouldCloseOnMouseDown_面板未开_任何点击都不触发关闭()
        {
            // BackgroundBounds 默认 Rect.zero 包含 (0,0)——_open=false 时必须拦掉
            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(100, 100)),
                Is.False, "未开时点外不构成关闭谓词");
        }

        [Test]
        public void ShouldCloseOnMouseDown_非左键_不关闭()
        {
            _ui.SetOpen(true);
            // 需要先有 BackgroundBounds——测试用 mock 不实际跑 OnGUI，手动塞一个矩形
            SetBackgroundBoundsForTest(new Rect(0, 0, 400, 300));

            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.MouseDown, 1, false, new Vector2(500, 500)),
                Is.False, "右键不是关闭谓词");
            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.MouseUp, 0, false, new Vector2(500, 500)),
                Is.False, "MouseUp 不是关闭谓词");
            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.Layout, 0, false, new Vector2(500, 500)),
                Is.False, "Layout 事件不算");
        }

        [Test]
        public void ShouldCloseOnMouseDown_点在矩形内_不关闭()
        {
            _ui.SetOpen(true);
            SetBackgroundBoundsForTest(new Rect(0, 0, 400, 300));

            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(200, 150)),
                Is.False, "鼠标在面板内不触发关闭——只有关在 UI 矩形外才算");
        }

        [Test]
        public void ShouldCloseOnMouseDown_点在矩形外_关闭()
        {
            _ui.SetOpen(true);
            SetBackgroundBoundsForTest(new Rect(0, 0, 400, 300));

            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(500, 500)),
                Is.True, "面板外左键 = 关闭");
            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(-5, -5)),
                Is.True, "面板外（左上角之前）也触发关闭");
        }

        [Test]
        public void ShouldCloseOnMouseDown_带SHIFT_不关闭_为合成UI等留路()
        {
            _ui.SetOpen(true);
            SetBackgroundBoundsForTest(new Rect(0, 0, 400, 300));

            // 武器面板自身没有 SHIFT 修饰路径，但 SHIFT 是合成 UI 入料的修饰键——
            // 若用户在 R 面板开着时按 SHIFT+click 背包格想入合成区，
            // 背包面板的 SHIFT 优先分支必须先判，R 面板不应误关
            Assert.That(_ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, true, new Vector2(500, 500)),
                Is.False, "SHIFT+click 不算点外——为合成 UI SHIFT 优先级留路");
        }

        // ─── 弹药显示数：枪 → bullet 数 ─────────────────────────────────────────

        [Test]
        public void 枪子弹计数_走InventoryCountOf_与背包现状同源()
        {
            PutInBag(5, Bullet, 17);
            // 走公共路径：OnGUI 调 Inventory.CountOf(_ui.BulletNumericId)
            Assert.That(_ctx.Inventory.CountOf(_ui.BulletNumericId), Is.EqualTo(17),
                "子弹数从 PlayerContext.Inventory.CountOf 读——OnGUI 与 EditMode 测试同源");
        }

        // ─── 工具方法：直接写 BackgroundBounds（OnGUI 在测试外不跑） ───────────────

        private void SetBackgroundBoundsForTest(Rect r)
        {
            // BackgroundBounds 是 { get; private set; }——测试用反射注入
            var prop = typeof(WeaponPanelUi).GetProperty(
                nameof(WeaponPanelUi.BackgroundBounds));
            prop.GetBackingField().SetValue(_ui, r);
        }
    }

    /// <summary>辅助扩展：取 { get; private set; } 自动属性的 backing field。</summary>
    internal static class PropertyBackingFieldExtensions
    {
        public static System.Reflection.FieldInfo GetBackingField(this System.Reflection.PropertyInfo prop)
        {
            // 自动属性的 backing field 名字是 <PropertyName>k__BackingField
            return prop.DeclaringType.GetField(
                $"<{prop.Name}>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }
    }
}
#endif