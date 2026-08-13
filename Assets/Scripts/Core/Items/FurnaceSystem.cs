using MyWorld.Core.Items;

namespace MyWorld.Core.Items
{
    public class FurnaceSystem
    {
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
            // 简化：coal 是唯一燃料（itemId=10）
            if (stack.ItemId != 10) return false;
            _fuelRemaining += _coalFuelValue * stack.Count;
            _hasFuel = _fuelRemaining > 0;
            return true;
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
                // 简化：iron_ore → iron_ingot（itemId 1 → 2）
                int outputId = (Input.Value.ItemId == 1) ? 2 : Input.Value.ItemId;
                Output = new ItemStack(outputId, 1);
                Input = (Input.Value.Count > 1) ? new ItemStack(Input.Value.ItemId, Input.Value.Count - 1) : (ItemStack?)null;
                Progress = 0f;
            }
        }
    }
}