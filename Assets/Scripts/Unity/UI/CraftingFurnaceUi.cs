using UnityEngine;
using MyWorld.Core.Items;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 熔炉 UI：input / fuel / output 三槽 + progress bar。
    /// m6 A2：原先只有一行黑字进度文本，改为三槽走 ItemSlotDrawer（图标 + 数量角标），
    /// 文本换白字缓存样式，深色 Box 上直接可读。
    /// m6 A2 fix2：老 bug——本类自创建（7051b08）起就没有开关、Bind 后常驻左上角。
    /// 现在与背包 E / 工作台 P 同款：F 键开关（E/P/B/V/X 已被其它 UI 占用）。
    /// m6 C2：补三槽点击交互（此前只有显示）——输入/燃料从手上（选中 hotbar 格）整组投入、
    /// 输出整组取走；取出烧炼产出的那一刻发 SmeltItem 任务事件（首章任务 7 的源头）。
    /// </summary>
    public class CraftingFurnaceUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.F;
        public float CurrentProgress { get; private set; }

        /// <summary>当前输入的烧炼时长快照（秒）——<see cref="ProgressFillFraction"/> 的分母。
        /// m10 C2 fix1（I2）：粗矿 10s / 圆石 1s 分流后，分母不能再用固定的构造时长，
        /// 否则粗矿 1s 就假满格后空等 9s。</summary>
        public float CurrentSmeltDuration { get; private set; } = 1f;

        /// <summary>进度条填充比例（0..1）：Progress / 当前输入的烧炼时长。
        /// OnGUI 画条与测试断言共用这一份计算，保证「所见即所测」。</summary>
        public float ProgressFillFraction =>
            CurrentSmeltDuration > 0f ? Mathf.Clamp01(CurrentProgress / CurrentSmeltDuration) : 0f;

        /// <summary>是否打开显示。默认关闭（fix2 起）。</summary>
        public bool IsOpen => _open;

        private bool _open;
        private FurnaceSystem _furnace;

        public void Bind(FurnaceSystem f) { _furnace = f; }

        /// <summary>程序化开关（B1 截图管线将来加 ui-furnace.png 用，也供测试）。
        /// <para>m6 终审修 C1：所有开关路径统一经 <see cref="UiCursorGate"/> 登记指针门——
        /// 熔炉投料/取料靠点击格子，指针不解锁实机不可用。</para></summary>
        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            if (open) UiCursorGate.Open();
            else UiCursorGate.Close();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(ToggleKey)) return;
            // m6 终审修 C1（B3-③）：自己开着时按键 = 关自己；其它模态 UI 开着时不叠开
            if (_open) SetOpen(false);
            else if (!UiCursorGate.IsOpen) SetOpen(true);
        }

        /// <summary>评审 04 R-6：级联关闭入口（幂等——已关再调 no-op）。</summary>
        private void CloseSelf() => SetOpen(false);

        private void OnEnable() => UiCursorGate.RegisterClose(CloseSelf);

        private void OnDisable()
        {
            UiCursorGate.UnregisterClose(CloseSelf);
            // m6 终审修 C1（B3-②）：禁用/销毁时若还开着必须把门位还回去，否则计数泄漏
            if (_open) SetOpen(false);
        }

        public void TickForTest()
        {
            if (_furnace != null)
            {
                CurrentProgress = _furnace.Progress;
                CurrentSmeltDuration = _furnace.CurrentSmeltDuration;
            }
        }

        /// <summary>玩家上下文：优先单例（运行时），EditMode 下退回同物体组件（Awake 不跑）。</summary>
        private PlayerContext Ctx =>
            PlayerContext.Instance != null ? PlayerContext.Instance : GetComponent<PlayerContext>();

        /// <summary>
        /// 把手上（选中 hotbar 格）的整组物品投入熔炉输入槽（圆石烧铁锭）。
        /// 投入成功后手上清空。返回是否成功。
        /// </summary>
        public bool DepositSelectedAsInput()
        {
            var ctx = Ctx;
            if (_furnace == null || ctx == null || ctx.Inventory == null) return false;
            var sel = ctx.Inventory.GetSelected();
            if (sel.IsEmpty || !_furnace.AddInput(sel)) return false;
            ctx.Inventory.SetSlot(ctx.Inventory.SelectedHotbarIndex, ItemStack.Empty);
            return true;
        }

        /// <summary>
        /// 把手上的整组物品投入燃料槽（只有煤合法，见 <see cref="FurnaceSystem.CoalItemId"/>）。
        /// 投入成功后手上清空。返回是否成功。
        /// </summary>
        public bool DepositSelectedAsFuel()
        {
            var ctx = Ctx;
            if (_furnace == null || ctx == null || ctx.Inventory == null) return false;
            var sel = ctx.Inventory.GetSelected();
            if (sel.IsEmpty || !_furnace.AddFuel(sel)) return false;
            ctx.Inventory.SetSlot(ctx.Inventory.SelectedHotbarIndex, ItemStack.Empty);
            return true;
        }

        /// <summary>
        /// 取走输出槽的烧炼产出：容量预检（<see cref="PlayerInventory.SpaceFor"/>）通过才取——
        /// 产出整组进背包、清空输出槽并返回 true；没有产出或背包装不下返回 false（产出留在炉里）。
        /// <para>m6 C2 fix1：与三个合成 UI 的拿产出同款预检，替代旧的
        /// 「先 TakeOutput 再 TryAdd、塞不下的掉脚下」——统一为装不下就整单不取。</para>
        /// <para>取出的那一刻发 SmeltItem 任务事件（Count=本次取出数量）。</para>
        /// </summary>
        public bool TryTakeOutput()
        {
            var ctx = Ctx;
            if (_furnace == null || ctx == null || ctx.Inventory == null) return false;
            if (_furnace.Output == null || _furnace.Output.Value.IsEmpty) return false;

            var taken = _furnace.Output.Value;
            if (ctx.Inventory.SpaceFor(taken.ItemId) < taken.Count)
            {
                return false; // 背包装不下：产出留在熔炉输出槽，等玩家腾格子
            }

            _furnace.TakeOutput();
            ctx.Inventory.TryAdd(taken, out _); // 预检过，leftover 必为 0

            // av W3-13：合成音（熔炉取出算一次烧炼产物到手）
            MyWorld.Unity.Audio.PlayerAudioSystem.Instance?.PlayCraft();

            QuestEventBus.Instance?.Raise(new QuestEvent
            {
                Type = QuestEventType.SmeltItem,
                ItemId = taken.ItemId,
                Count = taken.Count,
            });
            return true;
        }

        private void OnGUI()
        {
            if (!_open || _furnace == null) return;
            CurrentProgress = _furnace.Progress;
            CurrentSmeltDuration = _furnace.CurrentSmeltDuration;
            var items = PlayerContext.Instance != null ? PlayerContext.Instance.Items : null;

            // 背景：160(左) 宽 200、高 170，三槽 + 进度条全部框在内
            GUI.Box(new Rect(10, 80, 200, 170), GUIContent.none);
            GUI.Label(new Rect(20, 84, 180, 18), $"熔炉 {CurrentProgress:F2}", ItemSlotDrawer.WhiteStyle());

            const int size = 40;
            // 左列：上=输入、下=燃料；右列：输出
            var inputRect = new Rect(24, 110, size, size);
            var fuelRect = new Rect(24, 160, size, size);
            var outputRect = new Rect(110, 135, size, size);
            ItemSlotDrawer.Draw(inputRect, _furnace.Input ?? ItemStack.Empty, items, false);
            ItemSlotDrawer.Draw(fuelRect, _furnace.Fuel ?? ItemStack.Empty, items, false);
            ItemSlotDrawer.Draw(outputRect, _furnace.Output ?? ItemStack.Empty, items, false);

            // 烧炼进度条：输出槽下方，宽度按进度填充——分母是**当前输入**的烧炼时长
            // （m10 C2 fix1 I2：粗矿 10s，用固定 1s 分母会 1s 假满格后空等 9s）
            var bar = new Rect(110, 190, 84, 10);
            GUI.Box(bar, GUIContent.none);
            float fillW = bar.width * ProgressFillFraction;
            if (fillW > 0.5f) GUI.DrawTexture(new Rect(bar.x, bar.y, fillW, bar.height), Texture2D.whiteTexture);

            // m6 C2：三槽点击交互（与口袋合成同款左键取/放）
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (inputRect.Contains(Event.current.mousePosition))
                {
                    if (DepositSelectedAsInput()) Event.current.Use();
                }
                else if (fuelRect.Contains(Event.current.mousePosition))
                {
                    if (DepositSelectedAsFuel()) Event.current.Use();
                }
                else if (outputRect.Contains(Event.current.mousePosition))
                {
                    if (TryTakeOutput()) Event.current.Use();
                }
            }
        }
    }
}
