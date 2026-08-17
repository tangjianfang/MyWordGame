using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 跨 MonoBehaviour 共享的玩家状态（背包 / 生命 / 时间 / 物品注册）。
    /// 走单例：场景里挂一个，所有 UI / 战斗 / 动物系统读它。
    /// 避免在 OnGUI 里 FindObjectOfType（IMGUI 每帧调一次，开销不可忽略）。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class PlayerContext : MonoBehaviour
    {
        public static PlayerContext Instance { get; private set; }

        public PlayerInventory Inventory;
        public Health Health;
        public TimeOfDay Time;
        public ItemDatabase Items;
        public RecipeDatabase Recipes;
        public Experience Experience;
        public DeathSystem Death;
        public HungerSystem HungerSystem;
        public FurnaceSystem FurnaceSystem;

        // ─── m11 第 1 波（集成点②）：农业 / 箱子 / 床 / 繁殖四个 Core 系统的汇聚点 ───
        // 模式照 FurnaceSystem：WorldBootstrap 建好实例挂上来，SaveLoadService 读写快照，
        // tick 宿主（FarmingHost）推进。可空 = 数据表缺失时降级（右键路由与存档层都判 null）。

        /// <summary>农田系统（锄地/播种/生长/收获，进度进 LevelData.FarmStates）。</summary>
        public MyWorld.Core.Farming.FarmSystem FarmSystem;

        /// <summary>喂食繁殖系统（发情配对→孕期→幼崽记录；孕期/幼崽不入档，读档重置）。</summary>
        public MyWorld.Core.Farming.BreedingSystem BreedingSystem;

        /// <summary>箱子内容存储（坐标→内容行，全量往返 LevelData.ChestContents）。</summary>
        public MyWorld.Core.Blocks.ChestSystem ChestSystem;

        /// <summary>床系统（两格放置/夜间跳早晨/重生点，LevelData.BedSpawnPoints）。</summary>
        public MyWorld.Core.Blocks.BedSystem BedSystem;

        /// <summary>世界里正在飞的掉落物（B7 <see cref="ItemDropEntity"/>）。
        /// 挖方块时 spawn；m7 B1 起 <see cref="MyWorld.Unity.Player.PlayerController.PickupNearbyDrops"/>
        /// 每帧按吸附语义推进（进 2.5m 圈飞向玩家，贴脸入包），
        /// <see cref="MyWorld.Unity.Items.ItemDropViewRegistry"/> 同步建/毁小方块视图。
        /// 永不为 null，直接 Add / Remove 即可。</summary>
        public readonly System.Collections.Generic.List<ItemDropEntity> ItemDrops =
            new System.Collections.Generic.List<ItemDropEntity>();
        public MyWorld.Unity.UI.DeathScreenUi DeathScreen;

        // ─── m10 C1：手持装备三属性（手持即生效，切走失效） ─────────────────────
        //
        // 简化模型（spec §3）：不做穿戴栏，加成只看**选中物品一件**——
        // 金系攻击走物品表 attackDamage（既有通道），防御/移速/生命上限三新属性
        // 走 items/*.json 的 gearBonus，由 RefreshGearBonuses 每帧从选中物品重建。

        /// <summary>防御点数：受伤时伤害 - 本值（下限 1 伤不无敌，
        /// <see cref="GearBonusMath.MitigateDamage"/>）。</summary>
        public int Defense { get; private set; }

        /// <summary>移速加成（比例，0.05 = +5%）：PlayerMotor 水平目标速度乘 (1 + 本值)。</summary>
        public float MoveSpeedBonus { get; private set; }

        /// <summary>生命上限加成（点数）：有效血上限 = <see cref="Health"/>.Max + 本值。</summary>
        public int MaxHealthBonus { get; private set; }

        /// <summary>有效血上限（m10 C1）：基础 <see cref="Health"/>.Max + 手持装备加成。
        /// <see cref="MyWorld.Unity.Player.PlayerController.Respawn"/> 回满到这里而不是基础值。</summary>
        public float EffectiveMaxHealth =>
            GearBonusMath.EffectiveMaxHealth(Health.Max, MaxHealthBonus);

        /// <summary>
        /// 从选中物品刷新三属性（m10 C1）。本组件挂 DefaultExecutionOrder(-1000)，
        /// Update 先于 PlayerController 执行，运行时每帧自动刷；EditMode 测试
        /// 改完选中格后手动调。刷新末尾把 <see cref="Health"/>.Current 钳到有效上限内——
        /// 切走生命上限装备的瞬间，多出来的血当场收回（血量刷新处钳制，只收不加）。
        /// </summary>
        public void RefreshGearBonuses()
        {
            var bonuses = GearBonuses.FromDefinition(GetSelectedDefinition());
            Defense = bonuses.Defense;
            MoveSpeedBonus = bonuses.MoveSpeedBonus;
            MaxHealthBonus = bonuses.MaxHealthBonus;
            Health.Current = GearBonusMath.ClampCurrentToEffectiveMax(
                Health.Current, Health.Max, MaxHealthBonus);
        }

        private void Update() => RefreshGearBonuses();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (Inventory == null) Inventory = new PlayerInventory();
            if (Health.Current <= 0) Health = new Health(20);
            if (Time == null) Time = new TimeOfDay();
            if (HungerSystem == null) HungerSystem = new HungerSystem();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public ItemDefinition GetSelectedDefinition()
        {
            // 读容忍：Inventory 为 null（EditMode 测试没显式初始化 / 早期场景）视同空手，
            // 不抛——m10 C1 起 Respawn 也会经 RefreshGearBonuses 走到这里
            if (Items == null || Inventory == null) return null;
            var stack = Inventory.GetSelected();
            if (stack.IsEmpty) return null;
            return Items.TryGetByNumericId(stack.ItemId, out var def) ? def : null;
        }
    }
}
