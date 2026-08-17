using MyWorld.Core.Items;

namespace MyWorld.Core.Items
{
    public class FurnaceSystem
    {
        /// <summary>唯一合法燃料：真煤（items/coal.json，numericId 1007）。
        /// m3 占位时期写死 10（物品表里不存在），实机永远投不进燃料——m6 C2 修正。</summary>
        public const int CoalItemId = 1007;

        /// <summary>烧炼输入：圆石（items/cobblestone.json，numericId 1003）。
        /// 物品表 / 方块表里都没有 iron_ore，圆石是「挖石头」直接产出的最近似矿石物品。</summary>
        public const int SmeltInputItemId = 1003;

        /// <summary>烧炼输出：铁锭（items/iron_ingot.json，numericId 1004）——
        /// 首章任务 7 要求 SmeltItem iron_ingot(1004)。</summary>
        public const int SmeltOutputItemId = 1004;

        // ─── m10 C2：粗矿冶炼——挖矿掉落的粗金/粗铁 10s 炼成锭 ─────────────────
        // （圆石→铁锭的旧映射保持不动：首章任务链靠它，见上）

        /// <summary>粗金（items/raw_gold.json，numericId 1023，挖金矿石掉落）。</summary>
        public const int RawGoldItemId = 1023;

        /// <summary>粗铁（items/raw_iron.json，numericId 1024，挖粗铁矿石掉落）。</summary>
        public const int RawIronItemId = 1024;

        /// <summary>金锭（items/gold_ingot.json，numericId 1027——m10 材料段顺延。
        /// brief 写的「1005」是笔误：1005 早已被 diamond 占用，真表里金锭是 1027。</summary>
        public const int GoldIngotItemId = 1027;

        // ─── m11 W1-4：沙子烧玻璃（10s）──────────────────────────────────────────
        // 熔炉配方目前没有 JSON 通道（RecipeDatabase 只认合成 tier），既有「熔炉配方格式」
        // 就是本类的常量映射——沙→玻璃照粗矿同款 10s 档接入，配方外置留给后续波次。

        /// <summary>沙子（items/sand.json，numericId 1355——m11 W1-4 物品段）。</summary>
        public const int SandItemId = 1355;

        /// <summary>玻璃（items/glass.json，numericId 1354——m11 W1-4 物品段）。</summary>
        public const int GlassItemId = 1354;

        /// <summary>沙子烧玻璃时长（秒）。与粗矿同档（<see cref="RawOreSmeltSeconds"/> 的等待感）。</summary>
        public const float SandSmeltSeconds = 10f;

        /// <summary>粗矿烧炼时长（秒）。矿石精炼比圆石压锭（构造时长，实机 1s）慢一个
        /// 量级——10s 是「值得开熔炉等一等」的等待感，又不至于磨掉孩子耐心。</summary>
        public const float RawOreSmeltSeconds = 10f;

        public ItemStack? Input { get; private set; }
        public ItemStack? Fuel { get; private set; }
        public ItemStack? Output { get; private set; }
        public float Progress { get; private set; }
        public int MaxInputStack { get; set; } = 64;

        private readonly int _coalFuelValue;
        private readonly float _smeltTimeSeconds;
        private float _fuelRemaining;
        private bool _hasFuel;

        public FurnaceSystem(int coalFuelValue, float smeltTimeSeconds)
        {
            _coalFuelValue = coalFuelValue;
            _smeltTimeSeconds = smeltTimeSeconds;
        }

        public bool AddInput(ItemStack stack)
        {
            if (Input == null) { Input = stack; return true; }
            if (Input.Value.ItemId != stack.ItemId) return false;
            if (Input.Value.Count + stack.Count > MaxInputStack) return false;
            Input = new ItemStack(Input.Value.ItemId, Input.Value.Count + stack.Count);
            return true;
        }

        public bool AddFuel(ItemStack stack)
        {
            // 简化：coal 是唯一燃料（真实 numericId 见 CoalItemId）
            if (stack.ItemId != CoalItemId) return false;
            _fuelRemaining += _coalFuelValue * stack.Count;
            _hasFuel = _fuelRemaining > 0;
            return true;
        }

        /// <summary>剩余燃料燃烧时间（秒）。Tick 每秒扣 1，归零后停止烧炼（只读，改值走 <see cref="Restore"/>）。</summary>
        public float FuelRemaining => _fuelRemaining;

        /// <summary>当前输入的烧炼时长（秒）：圆石按构造时长，粗矿 10s（m10 C2），
        /// 沙子 10s（m11 W1-4 烧玻璃），无输入也按构造时长（UI 空闲时不除零不乱跳）。
        /// m10 C2 fix1（I2）：熔炉 UI 进度条以此为分母——旧实现拿 Progress 秒数直接当
        /// 比例，10s 粗矿配方下 1s 就假满格后空等 9s。</summary>
        public float CurrentSmeltDuration
            => TryGetSpecialSmelt(Input?.ItemId ?? 0, out _, out float seconds) ? seconds : _smeltTimeSeconds;

        /// <summary>存档恢复专用：直接覆写三个槽位、烧炼进度与剩余燃料（运行时不要调用）。</summary>
        public void Restore(ItemStack? input, ItemStack? fuel, ItemStack? output, float progress, float fuelRemaining)
        {
            Input = input;
            Fuel = fuel;
            Output = output;
            Progress = progress;
            _fuelRemaining = fuelRemaining;
            _hasFuel = _fuelRemaining > 0f;
        }

        public ItemStack? TakeOutput()
        {
            var out_ = Output;
            Output = null;
            Progress = 0f;
            return out_;
        }

        public void Tick(float dt)
        {
            if (Input == null || Output != null) return;
            if (_fuelRemaining <= 0f) return;

            _fuelRemaining -= dt;
            Progress += dt;

            // 时长与产物都按输入物品分流（CurrentSmeltDuration 同一判定，UI 与 Tick 不漂移）：
            // 粗金 → 金锭 / 粗铁 → 铁锭（m10 C2，10s）；沙子 → 玻璃（m11 W1-4，10s）；
            // 圆石 → 铁锭（m6 C2 修正 m3 占位映射 1→2，构造时长）；
            // 其它物品照旧原样「烧成自己」（旧 passthrough 行为不变）
            if (Progress >= CurrentSmeltDuration)
            {
                Output = new ItemStack(ResolveSmeltOutput(Input.Value.ItemId), 1);
                Input = (Input.Value.Count > 1) ? new ItemStack(Input.Value.ItemId, Input.Value.Count - 1) : (ItemStack?)null;
                Progress = 0f;
            }
        }

        /// <summary>输入→产物映射：查表命中用表值，其余原样透传。</summary>
        private int ResolveSmeltOutput(int inputItemId)
            => TryGetSpecialSmelt(inputItemId, out int outputItemId, out _) ? outputItemId : inputItemId;

        /// <summary>
        /// 特殊烧炼映射（产物 + 时长的单一真源，UI 分母与 Tick 判定共用）：
        /// 圆石→铁锭（构造时长，m6 C2）/ 粗金→金锭、粗铁→铁锭（10s，m10 C2）/
        /// 沙子→玻璃（10s，m11 W1-4）。返回 false = 无特殊映射，走透传 + 构造时长。
        /// </summary>
        private bool TryGetSpecialSmelt(int inputItemId, out int outputItemId, out float seconds)
        {
            switch (inputItemId)
            {
                case SmeltInputItemId:
                    outputItemId = SmeltOutputItemId;
                    seconds = _smeltTimeSeconds;
                    return true;
                case RawGoldItemId:
                    outputItemId = GoldIngotItemId;
                    seconds = RawOreSmeltSeconds;
                    return true;
                case RawIronItemId:
                    outputItemId = SmeltOutputItemId;  // 铁锭 1004，与圆石冶炼殊途同归
                    seconds = RawOreSmeltSeconds;
                    return true;
                case SandItemId:
                    outputItemId = GlassItemId;
                    seconds = SandSmeltSeconds;
                    return true;
                default:
                    outputItemId = 0;
                    seconds = 0f;
                    return false;
            }
        }
    }
}