using System.Collections.Generic;

namespace MyWorld.Core.Entities
{
    /// <summary>门的状态。</summary>
    public enum DoorState { Closed, Open }

    /// <summary>
    /// 简易门控制器：保存每个门位置的状态，外部（Unity 侧红石系统）调用 <see cref="UpdateSignal"/>
    /// 决定该门开还是关。Core 侧不渲染，只管状态与查询。
    /// </summary>
    public sealed class DoorController
    {
        private readonly Dictionary<long, DoorState> _doors = new Dictionary<long, DoorState>();

        public IReadOnlyDictionary<long, DoorState> Doors => _doors;

        public DoorState GetState(int x, int y, int z) => GetState(Key(x, y, z));

        public DoorState GetState(long key)
        {
            _doors.TryGetValue(key, out var s);
            return s;
        }

        /// <summary>
        /// 接收红石信号：有信号开门，无信号关门。
        /// 返回值：本次是否实际改变了门状态。
        /// </summary>
        public bool UpdateSignal(int x, int y, int z, bool powered)
        {
            long key = Key(x, y, z);
            var newState = powered ? DoorState.Open : DoorState.Closed;
            if (_doors.TryGetValue(key, out var old) && old == newState) return false;
            _doors[key] = newState;
            return true;
        }

        /// <summary>放置门（默认关）时调用，确保出现在字典里。</summary>
        public void Register(int x, int y, int z)
        {
            long key = Key(x, y, z);
            if (!_doors.ContainsKey(key)) _doors[key] = DoorState.Closed;
        }

        public bool IsOpen(int x, int y, int z) => GetState(x, y, z) == DoorState.Open;

        private static long Key(int x, int y, int z)
        {
            return ((long)(x & 0xFFFFFF) << 40) | ((long)(y & 0xFFFFFF) << 16) | (long)(z & 0xFFFF);
        }
    }
}