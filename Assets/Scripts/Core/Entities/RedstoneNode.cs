using System.Collections.Generic;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 简易红石节点：每根红石粉（dust）= 一个节点，含 (x,y,z) + 当前 power (0-15)。
    /// </summary>
    public readonly struct RedstoneNode
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly byte Power;

        public RedstoneNode(int x, int y, int z, byte power)
        {
            X = x;
            Y = y;
            Z = z;
            Power = power;
        }
    }

    /// <summary>
    /// 红石电路状态机。Core 侧只管信号传播，Unity 侧负责放置 / 翻动拉杆 / 放置红石粉。
    /// <para>
    /// 模型：每根红石粉的 power = 其 4 邻居（红石粉 / 拉杆源）的最大 power - 1，最低 0。
    /// 拉杆作为源节点 power = 15（拉下）或 0（拉开）。
    /// Tick() 一次性把所有节点 power 重新计算（稳定后再退出，O(N)）。
    /// </para>
    /// </summary>
    public sealed class RedstoneCircuit
    {
        public const byte MaxPower = 15;
        public const byte DecayPerHop = 1;

        // (x,y,z) -> 当前 power（节点不一定存在：值为 0 表示没有信号）
        private readonly Dictionary<long, byte> _wires = new Dictionary<long, byte>();
        // (x,y,z) -> 当前 power（拉杆源：power=15 表示打开）
        private readonly Dictionary<long, byte> _sources = new Dictionary<long, byte>();

        public IReadOnlyDictionary<long, byte> Wires => _wires;

        public void SetWire(int x, int y, int z, byte power)
        {
            long key = Key(x, y, z);
            if (power == 0) _wires.Remove(key);
            else _wires[key] = power;
        }

        public void SetSource(int x, int y, int z, bool powered)
        {
            long key = Key(x, y, z);
            if (powered) _sources[key] = MaxPower;
            else _sources.Remove(key);
        }

        public void RemoveWire(int x, int y, int z)
        {
            _wires.Remove(Key(x, y, z));
        }

        public byte GetPower(int x, int y, int z)
        {
            long key = Key(x, y, z);
            byte p = 0;
            _wires.TryGetValue(key, out p);
            return p;
        }

        public bool IsPowered(int x, int y, int z)
        {
            long key = Key(x, y, z);
            byte p = 0;
            if (_wires.TryGetValue(key, out p) && p > 0) return true;
            if (_sources.TryGetValue(key, out p) && p > 0) return true;
            return false;
        }

        /// <summary>
        /// 推进一格信号传播。返回本次是否发生变化（false 表示已稳定）。
        /// <para>
        /// 用 BFS 从所有源节点向外辐射：每个 wire 的 power = max(4 邻居当前 power) - 1，
        /// 邻居使用本 tick 已计算出的新值。多次 Tick 直到稳定（每次传播 1 跳）。
        /// </para>
        /// </summary>
        public bool Tick()
        {
            bool changed = false;
            var next = new Dictionary<long, byte>(_wires.Count);

            // 用本轮已写入的 next 查邻居，保证信号沿当前波前传播（不是只读旧值）
            foreach (var kv in _wires)
            {
                long key = kv.Key;
                UnpackKey(key, out int x, out int y, out int z);
                byte maxNeighbor = 0;

                ProbeNeighborPower(x + 1, y, z, next, out var p); if (p > maxNeighbor) maxNeighbor = p;
                ProbeNeighborPower(x - 1, y, z, next, out p); if (p > maxNeighbor) maxNeighbor = p;
                ProbeNeighborPower(x, y, z + 1, next, out p); if (p > maxNeighbor) maxNeighbor = p;
                ProbeNeighborPower(x, y, z - 1, next, out p); if (p > maxNeighbor) maxNeighbor = p;

                byte newPower = maxNeighbor > DecayPerHop ? (byte)(maxNeighbor - DecayPerHop) : (byte)0;
                next[key] = newPower;
                if (newPower != kv.Value) changed = true;
            }

            _wires.Clear();
            foreach (var kv in next)
            {
                if (kv.Value > 0) _wires[kv.Key] = kv.Value;     // power=0 的自动消失
            }
            return changed;
        }

        private void ProbeNeighborPower(int x, int y, int z, Dictionary<long, byte> next, out byte power)
        {
            long key = Key(x, y, z);
            if (_sources.TryGetValue(key, out power)) return;
            if (next.TryGetValue(key, out power) && power > 0) return;
            if (_wires.TryGetValue(key, out power) && power > 0) return;
            power = 0;
        }

        private static long Key(int x, int y, int z)
        {
            // 24 位有符号坐标：(x & 0xFFFFFF) << 40 | (y & 0xFFFFFF) << 16 | (z & 0xFFFF)
            // 项目世界范围 [-64, 320) y、[-30000,30000) x/z，足够
            return ((long)(x & 0xFFFFFF) << 40) | ((long)(y & 0xFFFFFF) << 16) | (long)(z & 0xFFFF);
        }

        private static void UnpackKey(long key, out int x, out int y, out int z)
        {
            x = (int)((key >> 40) & 0xFFFFFF);
            if (x >= 0x800000) x |= unchecked((int)0xFF000000);   // sign extend
            y = (int)((key >> 16) & 0xFFFFFF);
            if (y >= 0x800000) y |= unchecked((int)0xFF000000);
            z = (int)(key & 0xFFFF);
            if (z >= 0x8000) z |= unchecked((int)0xFFFF0000);
        }
    }
}