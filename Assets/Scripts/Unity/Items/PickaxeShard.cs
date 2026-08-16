using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Unity.Player;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Items
{
    /// <summary>
    /// m10 B2：镐碎裂散出的碎块（孩子的原创机制——「碎掉的镐子碰到会受伤」）。
    /// 耐久尽时 <see cref="BlockInteraction"/> 从玩家胸口散出 4-6 个，落到玩家脚边
    /// 扣 0.5 血（<see cref="PlayerController.TakeDamage(float, object)"/>——复活无敌帧
    /// 自动生效，不抛 CombatEvents 所以无反击/无经验），2 秒后自毁。
    /// <para>
    /// <b>独立轻实体，不走 <see cref="ItemDropEntity"/> / PlayerContext.ItemDrops</b>：
    /// 掉落物那套语义（Content 物品栈 / 吸附拾取状态机 / 存档持久化）碎块一个都用不上，
    /// 硬加 IsShard 标记会让拾取 / 视图 / 存档三处消费方各自过滤；2 秒寿命的碎片也不该
    /// 有机会被写进存档。「不可拾取」因此是<b>结构性保证</b>——不在 ItemDrops 列表里，
    /// 拾取路径（PickupNearbyDrops 只遍历该列表）根本看不见碎块。
    /// </para>
    /// <para>
    /// 视觉复用 ItemDropView 的管线（<see cref="UrpMaterialFactory.CreateLit"/> +
    /// 物品贴图均值色），尺寸是碎块自己的 0.15 格（掉落物 0.25），且<b>无浮动无自转</b>——
    /// 掉落物「转着圈等你捡」，碎块只是躺在地上的碎片，轮廓上一眼可分。
    /// </para>
    /// </summary>
    public sealed class PickaxeShard : MonoBehaviour
    {
        /// <summary>碎块边长（格）。掉落物 0.25，碎块更小（spec §2：0.15 格小方块）。</summary>
        public const float VisualSize = 0.15f;

        /// <summary>寿命（秒）。满 2 秒自毁，边界含等号（与 ItemDropEntity 宽限期的边界风格一致）。</summary>
        public const float LifetimeSeconds = 2f;

        /// <summary>接触伤害（点）。0.5——孩子的设定「伤害很低不挫败」，走小数伤害重载。</summary>
        public const float ContactDamage = 0.5f;

        /// <summary>接触判定半径（米）：碎块中心到玩家脚底中心的 3D 距离。</summary>
        public const float ContactRadius = 0.6f;

        /// <summary>碎块下落重力（米/秒²）。碎片比玩家轻，量级略缓于 PlayerMotor 的 -28。</summary>
        public const float Gravity = -20f;

        /// <summary>碎块数区间下限（spec §2：4-6 个）。</summary>
        public const int CountMin = 4;

        /// <summary>碎块数区间上限。</summary>
        public const int CountMax = 6;

        /// <summary>水平散开初速下限（米/秒）。</summary>
        public const float ScatterSpeedMin = 1.2f;

        /// <summary>水平散开初速上限（米/秒）。</summary>
        public const float ScatterSpeedMax = 2.2f;

        /// <summary>上抛初速下限（米/秒）。</summary>
        public const float UpSpeedMin = 0.6f;

        /// <summary>上抛初速上限（米/秒）。</summary>
        public const float UpSpeedMax = 1.2f;

        /// <summary>碎块出生高度：玩家脚底 +1.1（镐握在手上的高度）。刻意高于
        /// <see cref="ContactRadius"/>：出生瞬间扎不到脚，碎块落地（约 0.3-0.5s 后）才开始
        /// 判定接触——给玩家一个「碎裂了快躲开」的反应窗口，这是「伤害很低不挫败」的手感来源。</summary>
        public const float SpawnChestHeight = 1.1f;

        /// <summary>当前速度（米/秒）。落地后整向量清零（碎块不弹跳不滚动）。</summary>
        public Float3 Velocity { get; private set; }

        /// <summary>是否已扎过玩家。每个碎块最多扎一次——站着不动不会被同一块反复扎。</summary>
        public bool HasStung { get; private set; }

        /// <summary>逻辑位置（世界坐标，米）。transform 只是它的逐帧映射。</summary>
        public Float3 Position { get; private set; }

        private float _spawnTime;
        private float _restY;
        private PlayerController _player;

        /// <summary>
        /// 在玩家胸口高度散出一轮碎块（耐久尽时由 <see cref="BlockInteraction"/> 调用）。
        /// 数量 4-6 与每块的角度/初速全部走整数哈希掷点（<see cref="BlockDrops.RollCount"/>，
        /// 与掉落/生物掉落表同款确定性手法）——同一把镐在同一格碎两次，数量与散布逐块一致。
        /// <paramref name="brokenTool"/> 传磨损前的完整物品栈（碎块颜色取这把镐的贴图均值色）。
        /// 返回生成的碎块数组（EditMode 测试断言用）。
        /// </summary>
        public static PickaxeShard[] SpawnScatter(PlayerController player, ItemStack brokenTool,
            ItemDatabase items, int blockX, int blockY, int blockZ)
        {
            Vector3 feet = player.transform.position;
            // 空间哈希常量（classic spatial hashing primes）：物品 + 被挖方块坐标共同决定掷点
            int seed = unchecked(brokenTool.ItemId * 73856093 ^ blockX * 19349663
                                 ^ blockY * 83492791 ^ blockZ * 297121507);
            int count = BlockDrops.RollCount(seed, CountMin, CountMax);
            Color color = ItemDropView.ResolveColor(brokenTool, items);
            var origin = new Float3(feet.x, feet.y + SpawnChestHeight, feet.z);
            // 地面 = 出生时刻玩家脚底的平面 + 半个边长：立方体正好坐在玩家所站的地上
            float restY = feet.y + VisualSize * 0.5f;

            var shards = new PickaxeShard[count];
            for (int i = 0; i < count; i++)
            {
                // 每块独立掷角度与初速（seed + i*质数 区分，MobDropTable.RollAll 同款手法）
                int angleDegrees = BlockDrops.RollCount(unchecked(seed + i * 31 + 1), 0, 359);
                float horizontal = Lerp(ScatterSpeedMin, ScatterSpeedMax, RollTenth(seed, i, 2));
                float up = Lerp(UpSpeedMin, UpSpeedMax, RollTenth(seed, i, 3));
                float radians = angleDegrees * Mathf.Deg2Rad;
                shards[i] = Create(null, origin, restY,
                    new Float3(Mathf.Cos(radians) * horizontal, up, Mathf.Sin(radians) * horizontal),
                    color, player, Time.time);
            }
            return shards;
        }

        /// <summary>
        /// 建单个碎块。生产路径只经 <see cref="SpawnScatter"/>；EditMode 测试直接调用以注入
        /// 位置 / 速度 / 出生时刻。<paramref name="parent"/> 为 null 时挂场景根——
        /// 碎块是世界空间物体，不跟任何会移动的父节点走（挂玩家身上会被拖着满场飞）。
        /// </summary>
        public static PickaxeShard Create(Transform parent, Float3 position, float restY,
            Float3 velocity, Color color, PlayerController player, float spawnTime)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "镐碎块";
            // 碰撞体必须删（ItemDropView.Create 同款）：碎块伤害走距离判定，
            // 留着会挡玩家移动、吃挖掘射线
            var collider = cube.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);
            if (parent != null) cube.transform.SetParent(parent, false);
            cube.transform.localScale = new Vector3(VisualSize, VisualSize, VisualSize);

            var shard = cube.AddComponent<PickaxeShard>();
            shard.Position = position;
            shard.Velocity = velocity;
            shard._spawnTime = spawnTime;
            shard._restY = restY;
            shard._player = player;
            cube.GetComponent<Renderer>().sharedMaterial = UrpMaterialFactory.CreateLit(color);
            shard.SyncPose();
            return shard;
        }

        /// <summary>
        /// 步进一帧：寿命判定（过期自毁）→ 散落物理 → 接触判定 → 写 transform。
        /// <paramref name="currentTime"/> 显式注入——EditMode 测试不依赖 Time.time 就能验证
        /// 2 秒寿命的边界；运行时由 <see cref="Update"/> 喂 Time.time。
        /// </summary>
        public void Tick(Vector3 playerFeet, float currentTime, float dt)
        {
            if (currentTime - _spawnTime >= LifetimeSeconds)
            {
                DestroySelf();
                return;
            }

            // 散落物理：重力 + 落到 restY 停。落地即清速度（不弹不滚）——2 秒寿命的碎片，
            // 精致程度到此为止；地面取出生时刻玩家脚底的平面，挖掘动作发生在玩家脚下附近，
            // 1 格内的地形起伏忽略（与 ItemDropEntity.TickGravity 钉死 y=0 的简化同思路）
            if (Velocity.Y != 0f || Position.Y > _restY)
            {
                var velocity = new Float3(Velocity.X, Velocity.Y + Gravity * dt, Velocity.Z);
                Float3 next = Position + velocity * dt;
                if (next.Y <= _restY)
                {
                    next = new Float3(next.X, _restY, next.Z);
                    velocity = default;
                }
                Position = next;
                Velocity = velocity;
            }

            // 接触判定：距玩家脚底 < ContactRadius 且本块还没扎过 → 0.5 伤害。
            // TakeDamage 是玩家伤害唯一入口（复活无敌帧在此自动生效）；不抛 CombatEvents，
            // 所以碎块伤害不触发攻击系统的反击 / 经验。每块只扎一次（HasStung）。
            if (!HasStung && _player != null)
            {
                float dx = Position.X - playerFeet.x;
                float dy = Position.Y - playerFeet.y;
                float dz = Position.Z - playerFeet.z;
                if (dx * dx + dy * dy + dz * dz < ContactRadius * ContactRadius)
                {
                    _player.TakeDamage(ContactDamage, this);
                    HasStung = true;
                }
            }

            SyncPose();
        }

        private void Update()
        {
            // 玩家可能先于碎块销毁（场景卸载）：Unity 判空后喂个无害的远点
            Vector3 feet = _player != null ? _player.transform.position : Vector3.positiveInfinity;
            Tick(feet, Time.time, Time.deltaTime);
        }

        private void SyncPose()
        {
            transform.position = new Vector3(Position.X, Position.Y, Position.Z);
        }

        private void DestroySelf()
        {
            // 编辑器非播放态 Destroy 不生效，必须 DestroyImmediate（ItemDropViewRegistry 同款）
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        /// <summary>把 [0,10] 的整数掷点线性映射到 [min,max]——避免 float 直接进哈希。</summary>
        private static float Lerp(float min, float max, int tenth) => min + (max - min) * (tenth / 10f);

        /// <summary>第 i 块的 [0,10] 独立掷点（salt 区分用途）。</summary>
        private static int RollTenth(int seed, int i, int salt)
            => BlockDrops.RollCount(unchecked(seed + i * 17 + salt), 0, 10);
    }
}
