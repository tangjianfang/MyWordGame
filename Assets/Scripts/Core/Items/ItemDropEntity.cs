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
        /// <summary>拾取宽限期（秒）。Spec B7：掉落物生成后 <c>PickupGraceSeconds</c> 秒内
        /// 不可拾取，让玩家有反应时间看到掉落物出现。<c>TryPickupBy</c> 在 spawn 后
        /// 不足这个时长一律返回 false。</summary>
        public const float PickupGraceSeconds = 0.5f;

        /// <summary>当前掉落物内容。被拾走后置 null。</summary>
        public ItemStack? Content { get; private set; }

        /// <summary>世界坐标位置（米）。</summary>
        public Float3 Position { get; private set; }

        /// <summary>Y 方向速度（米/秒），仅受重力影响，撞地板后归零。</summary>
        public float VelocityY { get; private set; }

        /// <summary>
        /// 生成时刻（Unity 侧应 set 为 <c>Time.time</c>，Core 单元测试保持 0 默认值）。
        /// <para>
        /// 0 = 未设置，宽限期检查自动失效——让 Core 单元测试不依赖时间注入也能验证
        /// 范围判断逻辑。Unity 侧在 spawn 之后必须 set 一次（<c>Time.time</c> 在 batchmode
        /// 第一帧后就 > 0），让 <c>TryPickupBy</c> 的 grace 检查真正生效。
        /// </para>
        /// </summary>
        public float SpawnTime { get; set; }

        public ItemDropEntity(ItemStack content, Float3 position)
        {
            Content = content;
            Position = position;
            VelocityY = 0f;
            SpawnTime = 0f;
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
        /// 已被拾走（Content=null）、超出范围、<b>或仍在 0.5s 拾取宽限期内</b>都返回 false。
        /// <para>
        /// <paramref name="currentTime"/> 与 <see cref="SpawnTime"/> 的差小于
        /// <see cref="PickupGraceSeconds"/> 时拒绝拾取——spec B7 "0.5s 后可拾取" 的边界
        /// 含等号（恰好 0.5s 即可拾取）。<see cref="SpawnTime"/> 为 0（Core 单元测试默认值）
        /// 时跳过此检查，保留原范围判断行为。
        /// </para>
        /// </summary>
        public bool TryPickupBy(Float3 playerPosition, float currentTime, out int picked)
        {
            picked = 0;
            if (Content == null) return false;

            // 0.5s 拾取宽限期：SpawnTime > 0（已 set）才检查。
            // SpawnTime = 0 时跳过，让 Core 单元测试不必注入时间也能验证范围逻辑。
            if (SpawnTime > 0f && currentTime - SpawnTime < PickupGraceSeconds)
            {
                return false;
            }

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
