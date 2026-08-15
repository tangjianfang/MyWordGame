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

            if (Progress >= _smeltTimeSeconds)
            {
                // 圆石 → 铁锭（真实物品 id，m6 C2 修正 m3 占位映射 1→2）；
                // 其它物品照旧原样「烧成自己」，保持旧 passthrough 行为不变
                int outputId = (Input.Value.ItemId == SmeltInputItemId) ? SmeltOutputItemId : Input.Value.ItemId;
                Output = new ItemStack(outputId, 1);
                Input = (Input.Value.Count > 1) ? new ItemStack(Input.Value.ItemId, Input.Value.Count - 1) : (ItemStack?)null;
                Progress = 0f;
            }
        }
    }
}