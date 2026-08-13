using MyWorld.Core.Entities;
using MyWorld.Core.Math;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// 世界里的掉落物实体。方块被挖后会 spawn 一个，受重力下落、停在 y=0，
    /// 玩家靠近时通过 <see cref="TryPickupBy"/> 拾取。
    /// <para>
    /// 数据形态用 <see cref="ItemStack"/>?（nullable，对齐
    /// <see cref="FurnaceSystem.Output"/>：被拾走后置 null，上层不要持有引用）。
    /// Content 为 null 时表示已拾走，TryPickupBy 会拒绝再次拾取。
    /// </para>
    /// <para>
    /// 本类是 Core 层（无 UnityEngine 依赖），未来 Unity 侧的展示（ItemDropView）、
    /// 拾取响应在 B8 任务里挂上去。
    /// </para>
    /// </summary>
    public class ItemDropEntity
    {
        /// <summary>当前掉落物内容。被拾走后置 null。</summary>
        public ItemStack? Content { get; private set; }

        /// <summary>世界坐标位置（米）。</summary>
        public Float3 Position { get; private set; }

        /// <summary>Y 方向速度（米/秒），仅受重力影响，撞地板后归零。</summary>
        public float VelocityY { get; private set; }

        public ItemDropEntity(ItemStack content, Float3 position)
        {
            Content = content;
            Position = position;
            VelocityY = 0f;
        }

        /// <summary>
        /// 应用重力 dt 秒：先累加速度再位移，撞到 y=0 地板后停在地板上且速度归零。
        /// </summary>
        public void TickGravity(float dt, float gravity)
        {
            VelocityY += gravity * dt;
            float newY = Position.Y + VelocityY * dt;
            if (newY <= 0f)
            {
                Position = new Float3(Position.X, 0f, Position.Z);
                VelocityY = 0f;
            }
            else
            {
                Position = new Float3(Position.X, newY, Position.Z);
            }
        }

        /// <summary>
        /// 玩家是否在拾取范围内（默认 1.5m 半径）；是则把 count 写到 out 并返回 true。
        /// 已被拾走（Content=null）或超出范围都返回 false。
        /// </summary>
        public bool TryPickupBy(Float3 playerPosition, out int picked)
        {
            picked = 0;
            if (Content == null) return false;

            float dx = Position.X - playerPosition.X;
            float dy = Position.Y - playerPosition.Y;
            float dz = Position.Z - playerPosition.Z;
            const float pickupRadius = 1.5f;
            if (dx * dx + dy * dy + dz * dz < pickupRadius * pickupRadius)
            {
                picked = Content.Value.Count;
                return true;
            }
            return false;
        }

        /// <summary>把 Content 置 null，标记已被拾走。调用方在收到 TryPickupBy=true 后负责调用本方法。</summary>
        public void MarkPicked()
        {
            Content = null;
        }
    }
}