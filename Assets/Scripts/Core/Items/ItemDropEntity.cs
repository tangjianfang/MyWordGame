using System;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// 世界里的掉落物实体。方块被挖后会 spawn 一个，受重力下落、停在 y=0，
    /// 玩家靠近时经 <see cref="TickPickup"/> 吸附拾取。
    /// <para>
    /// 数据形态用 <see cref="ItemStack"/>?（nullable，对齐
    /// <see cref="FurnaceSystem.Output"/>：被拾走后置 null，上层不要持有引用）。
    /// Content 为 null 时表示已拾走，TryPickupBy 会拒绝再次拾取。
    /// </para>
    /// <para>
    /// 本类是 Core 层（无 UnityEngine 依赖）。m7 B1 起拾取是**吸附式**两段状态机
    /// （进 <see cref="PickupRadius"/> 圈 → 飞向玩家 → 贴脸入包），Unity 侧的
    /// 视觉（ItemDropView + ItemDropViewRegistry）只是把 Position 映射成小方块。
    /// </para>
    /// </summary>
    public class ItemDropEntity
    {
        /// <summary>拾取宽限期（秒）。Spec B7：掉落物生成后 <c>PickupGraceSeconds</c> 秒内
        /// 不可拾取（也不开始吸附），让玩家有反应时间看到掉落物出现。</summary>
        public const float PickupGraceSeconds = 0.5f;

        /// <summary>吸附拾取半径（米）。m7 B1：1.5 → 2.5——挖掘射程 5m 远超旧拾取半径，
        /// 远挖的掉落物默默躺着没人管；现在玩家进这个圈掉落物就开始飞向玩家。</summary>
        public const float PickupRadius = 2.5f;

        /// <summary>完成拾取距离（米）。吸附飞行把掉落物拉到距玩家这么近时才真正入包
        /// （m7 B1 起「进半径立即入包」改为「吸附到位后入包」）。</summary>
        public const float PickupDistance = 0.3f;

        /// <summary>吸附飞行速度（米/秒）。从 2.5m 飞到 0.3m 约 0.28s——肉眼是「吸过来」
        /// 的动画而不是瞬移消失。</summary>
        public const float AttractSpeed = 8f;

        /// <summary>m7 B1：吸附中标记。由 <see cref="TickPickup"/> 每帧步进驱动：
        /// 玩家进入 <see cref="PickupRadius"/> 且过了宽限期置 true，之后掉落物
        /// 逐帧飞向玩家。Core 纯数据标记（不持有任何 Unity 状态），读档 / 背包塞不下
        /// 重建的新实体自然回到 false。</summary>
        public bool Attracting;

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
        /// m7 B1：吸附拾取状态机，Unity 侧每帧（<c>PlayerController.PickupNearbyDrops</c>）步进一次：
        /// <para>
        /// 1) 玩家进入 <see cref="PickupRadius"/> 且过了宽限期 → <see cref="Attracting"/>=true 开始吸附；<br/>
        /// 2) 吸附中沿直线以 <see cref="AttractSpeed"/> 米/秒向玩家推进（步长钳到剩余距离，最多贴到玩家位置）；<br/>
        /// 3) 距玩家 &lt; <see cref="PickupDistance"/> 时返回 true——本帧完成拾取，调用方入包、
        /// <see cref="MarkPicked"/>、从列表移除。
        /// </para>
        /// <para>已被拾走（Content=null）、宽限期内、玩家不在半径内都返回 false（原地不动，不吸附）。
        /// 掉落物本来就贴脸（挖脚下方块 spawn 在脚边，&lt;0.3m）时同帧直接完成，不飞。</para>
        /// </summary>
        public bool TickPickup(Float3 playerPosition, float currentTime, float dt)
        {
            if (Content == null) return false;

            if (!Attracting)
            {
                if (!PastGrace(currentTime)) return false;
                if (DistanceSqTo(playerPosition) >= PickupRadius * PickupRadius) return false;
                Attracting = true;
            }

            // 已在完成距离内（贴脸 spawn / 上一帧飞到位）：本帧直接完成，不移动
            if (TryPickupBy(playerPosition, currentTime, out _)) return true;

            // 向玩家直线飞一步：步长最多是剩余距离，保证不越过玩家
            float distance = MathF.Sqrt(DistanceSqTo(playerPosition));
            Float3 direction = (playerPosition - Position) / distance;
            Position += direction * MathF.Min(AttractSpeed * dt, distance);
            return TryPickupBy(playerPosition, currentTime, out _);
        }

        /// <summary>
        /// 距玩家 &lt; <see cref="PickupDistance"/>（完成拾取距离）时把 count 写到 out 并返回 true；
        /// 已被拾走（Content=null）、未到完成距离、<b>或仍在 0.5s 拾取宽限期内</b>都返回 false。
        /// <para>
        /// m7 B1 起拾取判定从「进 <see cref="PickupRadius"/> 立即入包」改为「吸附到位后入包」——
        /// 本方法只负责最终完成判定（&lt;0.3m），进圈 / 飞行推进由 <see cref="TickPickup"/> 状态机承担。
        /// </para>
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
            if (!PastGrace(currentTime)) return false;

            if (DistanceSqTo(playerPosition) < PickupDistance * PickupDistance)
            {
                picked = Content.Value.Count;
                return true;
            }
            return false;
        }

        /// <summary>宽限期是否已过。<see cref="SpawnTime"/> 为 0（Core 单元测试默认值）时视为已过——
        /// 让 Core 单元测试不必注入时间也能验证范围逻辑；Unity 侧 spawn 后必须 set 一次 Time.time。</summary>
        private bool PastGrace(float currentTime)
        {
            return SpawnTime <= 0f || currentTime - SpawnTime >= PickupGraceSeconds;
        }

        /// <summary>当前位置到 <paramref name="p"/> 的距离平方。</summary>
        private float DistanceSqTo(Float3 p)
        {
            float dx = Position.X - p.X;
            float dy = Position.Y - p.Y;
            float dz = Position.Z - p.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>把 Content 置 null，标记已被拾走。调用方在收到 TryPickupBy=true 后负责调用本方法。</summary>
        public void MarkPicked()
        {
            Content = null;
        }
    }
}
