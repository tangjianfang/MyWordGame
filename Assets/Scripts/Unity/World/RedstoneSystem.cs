using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Environment
{
    /// <summary>
    /// 红石交互：右键 lever 翻动、右键 iron_door / wooden_door 切换、右键 redstone_dust 拆掉。
    /// 每 0.1s 推进一次 Core 的 RedstoneCircuit.Tick()，并把信号同步到门。
    /// <para>
    /// 简化版：Core 已有 circuit + door controller，Unity 侧只负责：
    /// (1) 玩家右键触发 lever / door 切换；(2) 周期 Tick；(3) 用 GetBlock 的方块 id 决定
    /// hit 的是什么；(4) door 开/关决定方块是否 solid（半实现）。
    /// </para>
    /// <para>
    /// m11 ②：木门走与铁门同一条既有切换通道（HandleHit 手动切换 + SyncDoors 信号同步）；
    /// 右键处理补上模态 UI 指针门与死亡画面让位——与 <see cref="MyWorld.Unity.Player.BlockInteraction"/>
    /// 同款（本组件自带独立射线，不走 UseAt，必须自查）。BlockInteraction 侧对两种门方块
    /// 只消费右键不放方块，门的实际开合全在本类。
    /// </para>
    /// </summary>
    public sealed class RedstoneSystem : MonoBehaviour
    {
        public ushort LeverId = 1006;
        public ushort IronDoorId = 1005;

        /// <summary>木门（m11 W1-4 注册，与 blocks/wooden_door.json 的 numericId=1026 手动一致）。</summary>
        public ushort WoodenDoorId = MyWorld.Core.Voxel.BlockIds.WoodenDoor;

        public ushort RedstoneDustId = 1007;
        public float TickInterval = 0.1f;

        public RedstoneCircuit Circuit = new RedstoneCircuit();
        public DoorController Doors = new DoorController();

        private World _world;
        private ChunkViewRegistry _views;
        private MyWorld.Unity.Player.PlayerController _player;
        private float _accum;

        public void Bind(World world, ChunkViewRegistry views, MyWorld.Unity.Player.PlayerController player)
        {
            _world = world;
            _views = views;
            _player = player;
        }

        private void Update()
        {
            if (_world == null || _player == null || _player.Eye == null) return;

            // 1) 玩家右键处理（m11 ②：先过指针门 + 死亡画面让位，与 BlockInteraction 同款）
            if (CanHandleRightClick() && Input.GetMouseButtonDown(1))
            {
                // m11 ②修复：旧代码把 null 注册表塞给 WorldSolidSource——每次右键
                // TryGetByNumericId 直接 NRE，拉杆/门/红石粉的右键分支从未真正跑通过。
                // 本组件 Bind 不接收注册表，改用「非空气即命中」的无注册表源：
                // lever/iron_door/wooden_door/redstone_dust 都是实心注册方块，命中面判定
                // 与注册表版本对它们完全一致（差异只在水这类非实体方块会挡射线，可接受）。
                var source = new AnyBlockSource(_world);
                var origin = new Float3(
                    _player.Eye.position.x, _player.Eye.position.y, _player.Eye.position.z);
                var dir = new Float3(
                    _player.Eye.forward.x, _player.Eye.forward.y, _player.Eye.forward.z);
                var hit = VoxelRaycaster.Cast(source, origin, dir, _player.Settings.ReachDistance);
                if (hit.Hit) HandleHit(hit.X, hit.Y, hit.Z);
            }

            // 2) 周期 Tick 推进电路 + 同步门
            _accum += Time.deltaTime;
            if (_accum >= TickInterval)
            {
                _accum = 0f;
                SyncSourcesAndWiresFromWorld();
                // 推进直到稳定（通常 1-2 次就够）
                for (int i = 0; i < 16; i++)
                {
                    if (!Circuit.Tick()) break;
                }
                SyncDoorsFromCircuit();
            }
        }

        /// <summary>
        /// m11 ②：右键交互的三重门——模态 UI 指针门（UiCursorGate / InputLocked）开着不响应
        /// （点 UI 格子不该顺手扳拉杆/开关门），死亡画面可见时让位给复活（m10 C2 fix1 同款，
        /// DestroyImmediate 后 DeathScreen 为 fake-null 视同「没有死亡画面」）。
        /// </summary>
        private bool CanHandleRightClick()
        {
            if (MyWorld.Unity.Player.BlockInteraction.InputLocked
                || MyWorld.Unity.UI.UiCursorGate.IsOpen)
            {
                return false;
            }

            var ctx = MyWorld.Unity.Gameplay.PlayerContext.Instance;
            return !(ctx != null && ctx.DeathScreen != null && ctx.DeathScreen.IsVisible);
        }

        private void HandleHit(int x, int y, int z)
        {
            ushort id = _world.GetBlock(x, y, z);
            if (id == LeverId)
            {
                // 拉杆：读现有 power；>0 表示已 power，关闭；否则打开
                bool wasOn = Circuit.IsPowered(x, y, z);
                Circuit.SetSource(x, y, z, !wasOn);
            }
            else if (id == IronDoorId || id == WoodenDoorId)
            {
                // 门：手动切换（即使没接红石也能右键开）。m11 ②起木门同通道。
                var state = Doors.GetState(x, y, z);
                Doors.UpdateSignal(x, y, z, state != DoorState.Open);
            }
            else if (id == RedstoneDustId)
            {
                // 右键红石粉 = 拆掉
                _world.SetBlock(x, y, z, BlockIds.Air);
                _views.MarkBlockChanged(x, y, z);
                Circuit.RemoveWire(x, y, z);
            }
        }

        /// <summary>
        /// 从 World 读所有 lever + redstone_dust 重建 circuit 的 source / wire 列表。
        /// </summary>
        private void SyncSourcesAndWiresFromWorld()
        {
            // 简化：用 player 周围 16x16x32 立方区域扫
            if (_player == null) return;
            int px = Mathf.FloorToInt(_player.transform.position.x);
            int pz = Mathf.FloorToInt(_player.transform.position.z);
            int py = Mathf.FloorToInt(_player.transform.position.y);

            int r = 8;
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            for (int dy = -r; dy <= r; dy++)
            {
                int wx = px + dx, wy = py + dy, wz = pz + dz;
                ushort id = _world.GetBlock(wx, wy, wz);
                if (id == LeverId)
                {
                    // 已经存在就是已加过源；保留原状态（由 HandleHit 切换）
                    long key = ((long)(wx & 0xFFFFFF) << 40) | ((long)(wy & 0xFFFFFF) << 16) | (long)(wz & 0xFFFF);
                    // 不主动清除 sources，HandleHit 负责；只在字典里没记录时插 0（默认拉起）
                    if (!Circuit.Wires.ContainsKey(key) && !Circuit.IsPowered(wx, wy, wz))
                    {
                        // 跳过：避免每 tick 重置
                    }
                }
                else if (id == RedstoneDustId)
                {
                    long key = ((long)(wx & 0xFFFFFF) << 40) | ((long)(wy & 0xFFFFFF) << 16) | (long)(wz & 0xFFFF);
                    if (!Circuit.Wires.ContainsKey(key)) Circuit.SetWire(wx, wy, wz, 1);
                }
                else if (id == IronDoorId || id == WoodenDoorId)
                {
                    Doors.Register(wx, wy, wz);
                }
            }
        }

        private void SyncDoorsFromCircuit()
        {
            // 简化：检查所有门邻居的红石信号
            var keys = new System.Collections.Generic.List<long>(Doors.Doors.Keys);
            foreach (var key in keys)
            {
                Unpack(key, out int x, out int y, out int z);
                bool powered = Circuit.IsPowered(x + 1, y, z)
                             || Circuit.IsPowered(x - 1, y, z)
                             || Circuit.IsPowered(x, y, z + 1)
                             || Circuit.IsPowered(x, y, z - 1);
                Doors.UpdateSignal(x, y, z, powered);
            }
        }

        private static long Key(int x, int y, int z)
        {
            return ((long)(x & 0xFFFFFF) << 40) | ((long)(y & 0xFFFFFF) << 16) | (long)(z & 0xFFFF);
        }

        /// <summary>
        /// m11 ②：右键拾取的无注册表射线源——非空气即命中（struct + 泛型约束，
        /// 照 <see cref="WorldSolidSource"/> 的写法让射线器特化）。lever/门/红石粉
        /// 都是实心注册方块，对它们的命中判定与注册表版本一致。
        /// </summary>
        private readonly struct AnyBlockSource : MyWorld.Core.Physics.ISolidBlockSource
        {
            private readonly World _world;

            public AnyBlockSource(World world)
            {
                _world = world;
            }

            public bool IsSolidAt(int x, int y, int z) => _world.GetBlock(x, y, z) != BlockIds.Air;
        }

        private static void Unpack(long key, out int x, out int y, out int z)
        {
            x = (int)((key >> 40) & 0xFFFFFF);
            if (x >= 0x800000) x |= unchecked((int)0xFF000000);
            y = (int)((key >> 16) & 0xFFFFFF);
            if (y >= 0x800000) y |= unchecked((int)0xFF000000);
            z = (int)(key & 0xFFFF);
            if (z >= 0x8000) z |= unchecked((int)0xFFFF0000);
        }
    }
}