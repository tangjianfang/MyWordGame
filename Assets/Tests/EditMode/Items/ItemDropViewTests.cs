#if UNITY_EDITOR
// m7 B1：ItemDropView / ItemDropViewRegistry——掉落物视觉 + 吸附拾取。
// 依赖 UnityEngine（MonoBehaviour / GameObject），#if UNITY_EDITOR 包裹只跑 EditMode 链；
// 掉落物状态机本身的纯逻辑断言在 ItemDropEntityTests（dotnet 链同样跑）。
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Items;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Items
{
    /// <summary>
    /// 覆盖三组行为（m7 B1，修「挖完看不到掉落物」）：
    /// 1) <see cref="ItemDropViewRegistry"/> 增删同步：实体列表加一项→视图 +1，移除→-1；
    /// 2) <see cref="ItemDropView"/> 视觉：0.25 格小方块 + 浮动（0.1*sin(t*2)）+ 绕 Y 自转（90°/s），
    ///    材质取物品主色（贴图均值），缺贴图退亮灰暗化——绝不品红；
    /// 3) 吸附拾取状态机（经 <see cref="PlayerController.PickupNearbyDrops(float)"/> 手动步进）：
    ///    2.5m 内 Attracting、每帧向玩家插值推进、距玩家 &lt;0.3m 才入包。
    /// </summary>
    public class ItemDropViewTests
    {
        private GameObject _host;
        private PlayerContext _ctx;
        private PlayerController _player;
        private ItemDropViewRegistry _registry;
        private Transform _parent;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("玩家");
            _ctx = _host.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不触发 Awake，单例/库存都要显式赋值（与 PlayerPickupDamageTests 一致）
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _player = _host.AddComponent<PlayerController>();

            _parent = new GameObject("掉落物根").transform;
            _registry = new GameObject("掉落物注册表").AddComponent<ItemDropViewRegistry>();
            _registry.Bind(_ctx, _parent);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_registry.gameObject);
            Object.DestroyImmediate(_parent.gameObject);
            Object.DestroyImmediate(_host);
        }

        // ─── 1) Registry 增删同步 ─────────────────────────────────────────────

        [Test]
        public void 新实体建视图_移除实体毁视图()
        {
            var dropA = new ItemDropEntity(new ItemStack(7, 1), new Float3(1f, 70f, 1f));
            var dropB = new ItemDropEntity(new ItemStack(7, 2), new Float3(2f, 70f, 1f));
            _ctx.ItemDrops.Add(dropA);
            _ctx.ItemDrops.Add(dropB);

            _registry.SyncNow();

            Assert.That(_registry.ViewCount, Is.EqualTo(2), "两个实体应各建一个视图");
            Assert.That(_parent.childCount, Is.EqualTo(2), "视图 GameObject 应挂在父节点下");

            _ctx.ItemDrops.Remove(dropA);
            _registry.SyncNow();

            Assert.That(_registry.ViewCount, Is.EqualTo(1), "消失实体的视图应销毁");
            Assert.That(_parent.childCount, Is.EqualTo(1), "视图 GameObject 应真的销毁（不是只改计数）");
            Assert.That(_registry.TryGetView(dropB, out _), Is.True, "留下的实体视图还在");
            Assert.That(_registry.TryGetView(dropA, out _), Is.False, "移除实体的视图索引应清掉");
        }

        [Test]
        public void 同一实体重复同步不重复建视图()
        {
            var drop = new ItemDropEntity(new ItemStack(7, 1), new Float3(1f, 70f, 1f));
            _ctx.ItemDrops.Add(drop);

            _registry.SyncNow();
            _registry.SyncNow();
            _registry.SyncNow();

            Assert.That(_registry.ViewCount, Is.EqualTo(1), "diff 应幂等：同一实体只建一个视图");
            Assert.That(_parent.childCount, Is.EqualTo(1), "父节点下不应堆出重复小方块");
        }

        [Test]
        public void 已拾空的壳实体不建视图且销毁既有视图()
        {
            var drop = new ItemDropEntity(new ItemStack(7, 1), new Float3(1f, 70f, 1f));
            _ctx.ItemDrops.Add(drop);
            _registry.SyncNow();
            Assert.That(_registry.ViewCount, Is.EqualTo(1), "先建出一个视图");

            // 拾空的壳：Content=null 但实体还留在列表里（正常路径会同时 RemoveAt，
            // 这里单独构造以锁死「视图不渲染空壳」的契约）
            drop.MarkPicked();
            _registry.SyncNow();

            Assert.That(_registry.ViewCount, Is.EqualTo(0), "Content=null 的壳实体视图应销毁");
            Assert.That(_parent.childCount, Is.EqualTo(0), "空壳不应在场景里留残影");
        }

        // ─── 2) 视觉：尺寸 / 浮动 / 自转 / 颜色 ───────────────────────────────

        [Test]
        public void 视图姿态_跟随实体并叠加浮动与自转()
        {
            var drop = new ItemDropEntity(new ItemStack(7, 1), new Float3(5f, 70f, 6f));
            _ctx.ItemDrops.Add(drop);
            _registry.SyncNow();
            Assert.That(_registry.TryGetView(drop, out var view), Is.True, "实体应有对应视图");
            Assert.That(view.transform.localScale,
                Is.EqualTo(Vector3.one * ItemDropView.VisualSize), "0.25 格小方块");

            // t=0：sin(0)=0，视图应贴实体位置
            view.SyncPose(0f);
            Assert.That(view.transform.position,
                Is.EqualTo(new Vector3(5f, 70f, 6f)).Within(1e-4f), "t=0 无浮动偏移");

            // t=π/4：sin(π/2)=1 → y+0.1；自转角 = 90°/s × π/4s ≈ 70.69°
            view.SyncPose(Mathf.PI / 4f);
            Assert.That(view.transform.position.y,
                Is.EqualTo(70f + 0.1f).Within(1e-3f), "浮动 y 偏移 = 0.1 * sin(t*2)");
            Assert.That(view.transform.position.x, Is.EqualTo(5f).Within(1e-4f), "浮动只动 Y");
            Assert.That(view.transform.rotation.eulerAngles.y,
                Is.EqualTo(Mathf.PI / 4f * ItemDropView.SpinDegreesPerSecond).Within(0.1f),
                "绕 Y 轴 90°/s 自转");
        }

        [Test]
        public void 物品未知或贴图缺失_退亮灰暗化而非品红()
        {
            // 品红是 UI「贴图缺失」占位语义（ItemSlotDrawer.Missing），3D 掉落物再用品红
            // 就分不清「贴图没配」和「正常渲染」了——锁死兜底色是亮灰暗化
            var unknown = ItemDropView.ResolveColor(new ItemStack(999999, 1), null);
            Assert.That(unknown, Is.EqualTo(ItemDropView.FallbackColor), "物品未注册退亮灰暗化");
            Assert.That(unknown, Is.Not.EqualTo(new Color(1f, 0f, 1f)), "绝不品红");
        }

        [Test]
        public void 真实物品贴图_取均值主色且不品红()
        {
            var items = ItemDatabaseLoader.Load();
            Assert.That(items, Is.Not.Null, "StreamingAssets/items 应可加载");
            Assert.That(items.TryGetById("plank", out var def), Is.True, "物品库应有 plank（WorldBootstrap 预填物品）");

            var color = ItemDropView.ResolveColor(new ItemStack(def.NumericId, 1), items);
            Assert.That(color, Is.Not.EqualTo(new Color(1f, 0f, 1f)), "有贴图的物品不该落到品红");
            Assert.That(color, Is.Not.EqualTo(ItemDropView.FallbackColor), "贴图存在时应取贴图均值而非兜底灰");
        }

        // ─── 3) 吸附拾取状态机（EditMode 手动步进） ───────────────────────────

        [Test]
        public void 两米内开始吸附_标记Attracting但不立即入包()
        {
            _host.transform.position = new Vector3(10f, 70f, 10f);
            var drop = new ItemDropEntity(new ItemStack(7, 3), new Float3(12f, 70f, 10f)); // 距玩家 2m < 2.5m
            _ctx.ItemDrops.Add(drop);

            int picked = _player.PickupNearbyDrops(1f / 60f);

            Assert.That(drop.Attracting, Is.True, "2m < 2.5m 吸附半径，应开始吸附");
            Assert.That(picked, Is.EqualTo(0), "吸附刚开始未到位，本帧不应入包");
            Assert.That(_ctx.Inventory.CountOf(7), Is.EqualTo(0), "背包应仍为空");
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(1), "掉落物应还在列表里飞行");
            Assert.That(drop.Position.X, Is.LessThan(12f), "掉落物应已向玩家推进（不再原地）");
        }

        [Test]
        public void 三米外不吸附不移动()
        {
            _host.transform.position = Vector3.zero;
            var drop = new ItemDropEntity(new ItemStack(7, 3), new Float3(3f, 0f, 0f)); // 距玩家 3m > 2.5m
            _ctx.ItemDrops.Add(drop);

            int picked = _player.PickupNearbyDrops(1f / 60f);

            Assert.That(picked, Is.EqualTo(0), "3m > 2.5m 吸附半径，不应拾取");
            Assert.That(drop.Attracting, Is.False, "半径外不应进入吸附态");
            Assert.That(drop.Position.X, Is.EqualTo(3f).Within(1e-5f), "半径外掉落物不应移动");
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(1), "掉落物应保留");
        }

        [Test]
        public void 吸附逐帧插值推进_到位后入包移除()
        {
            _host.transform.position = Vector3.zero;
            var drop = new ItemDropEntity(new ItemStack(7, 3), new Float3(2f, 0f, 0f));
            _ctx.ItemDrops.Add(drop);
            float lastX = drop.Position.X;

            // 手动步进（EditMode 不自动跑 Update）：每帧 1/60s × 8m/s ≈ 0.13m，
            // 2m → 0.3m 大约 13 帧；步到列表清空为止，600 帧上限防死循环
            int steps = 0;
            while (_ctx.ItemDrops.Count > 0 && steps < 600)
            {
                _player.PickupNearbyDrops(1f / 60f);
                steps++;

                // 飞行期间位置必须单调逼近玩家（插值推进，不瞬移不倒退）
                if (_ctx.ItemDrops.Count > 0)
                {
                    Assert.That(drop.Position.X, Is.LessThan(lastX + 1e-6f),
                        $"第 {steps} 步掉落物不应远离玩家");
                    lastX = drop.Position.X;
                }
            }

            Assert.That(steps, Is.GreaterThan(1), "2m 距离不该一步到位——必须有飞行过程");
            Assert.That(steps, Is.LessThan(600), "吸附应在有限帧内完成拾取（没有卡死）");
            Assert.That(_ctx.Inventory.CountOf(7), Is.EqualTo(3), "到位后 3 个应入包");
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(0), "拾取后实体应从列表移除");
            Assert.That(drop.Content, Is.Null, "实体内容应置空（MarkPicked）");
        }

        [Test]
        public void 贴脸掉落物单帧完成拾取()
        {
            // 挖脚下方块 spawn 的掉落物距玩家 0.2m < 0.3m 完成距离——无需飞行，同帧入包
            _host.transform.position = new Vector3(10f, 70f, 10f);
            _ctx.ItemDrops.Add(new ItemDropEntity(new ItemStack(7, 2), new Float3(10.2f, 70f, 10f)));

            int picked = _player.PickupNearbyDrops(1f / 60f);

            Assert.That(picked, Is.EqualTo(2), "已贴脸（<0.3m）应单帧完成拾取");
            Assert.That(_ctx.Inventory.CountOf(7), Is.EqualTo(2), "2 个应入包");
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(0), "拾取后实体应移除");
        }
    }
}
#endif
