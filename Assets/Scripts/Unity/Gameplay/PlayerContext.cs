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

        // ─── 装备三属性（m10 C1 手持模型 → m11 W2-1 升级双源）──────────────────
        //
        // m10 简化模型「手持即生效」升级为「**穿戴盔甲 + 手持装备**」双源汇总：
        //   - 穿戴源：ArmorSlots 4 槽（头/胸/腿/脚）逐件累计，只收对应部位盔甲
        //     （items/*.json 的 armorPart），穿脱走 ArmorInventory.TryEquipFrom/TryUnequipTo
        //   - 手持源：选中格一件（m10 语义**原样保留**——拿着带 gearBonus 的任何物品，
        //     含还没穿上的盔甲，照样生效；拿走即失效）
        // 金系攻击不参与汇总：永远走手持物品表 attackDamage（CombatController 挥击读选中物品），
        // 穿戴不改变攻击。三属性仍由 RefreshGearBonuses 每帧重建，读取方零改动。

        /// <summary>穿戴栏（头/胸/腿/脚 4 槽，m11 W2-1）。只收对应部位盔甲
        ///（<see cref="ArmorInventory.TryEquipFrom"/> 按部位定槽），进档随
        /// PlayerSnapshot.ArmorSlots 往返。UI 侧 <see cref="MyWorld.Unity.UI.ArmorSlotsUi"/>。</summary>
        public readonly ArmorInventory ArmorSlots = new ArmorInventory();

        // ─── m12 第 1 波三系统（照 FurnaceSystem 挂法：WorldBootstrap 建实例挂上来，可空降级） ───

        /// <summary>成就进度（m12 W1）。可空：achievements.json 缺失时降级（无成就无飘字）。
        /// 进度经 SaveLoadService 与 LevelData.Stats 往返（键 ach:&lt;id&gt;）。</summary>
        public MyWorld.Core.Achievements.AchievementSystem Achievements;

        /// <summary>图鉴解锁集（m12 W2）。可空降级同上；键 codex:mob/block/item:*。</summary>
        public MyWorld.Core.Codex.CodexSystem Codex;

        /// <summary>药水 buff 状态（m12 W3）。短时状态（30s）不进存档——与飞行态同取舍。</summary>
        public readonly MyWorld.Core.Buffs.PotionSystem Potions = new MyWorld.Core.Buffs.PotionSystem();

        /// <summary>防御点数：受伤时伤害 - 本值（下限 1 伤不无敌，
        /// <see cref="GearBonusMath.MitigateDamage"/>）。</summary>
        public int Defense { get; private set; }

        /// <summary>移速加成（比例，0.05 = +5%）：PlayerMotor 水平目标速度乘 (1 + 本值)。</summary>
        public float MoveSpeedBonus { get; private set; }

        /// <summary>生命上限加成（点数）：有效血上限 = <see cref="Health"/>.Max + 本值。</summary>
        public int MaxHealthBonus { get; private set; }

        /// <summary>有效血上限（m10 C1）：基础 <see cref="Health"/>.Max + 装备加成
        /// + 水肺药水的临时上限（m12 W3 占位效果，30s）。回满按本值。</summary>
        public float EffectiveMaxHealth =>
            GearBonusMath.EffectiveMaxHealth(Health.Max, MaxHealthBonus)
            + Potions.MaxHealthBonus(UnityEngine.Time.time);

        /// <summary>
        /// 双源刷新三属性（m11 W2-1）：穿戴盔甲逐件累计 + 手持选中格一件。
        /// 本组件挂 DefaultExecutionOrder(-1000)，Update 先于 PlayerController 执行，
        /// 运行时每帧自动刷；EditMode 测试与穿脱交互（ArmorSlotsUi.ClickSlot）改完状态后手动调。
        /// 同一件物品不可能既穿着又拿着，双源不会重复计同一件。
        /// 刷新末尾把 <see cref="Health"/>.Current 钳到有效上限内——脱下生命上限装备的瞬间，
        /// 多出来的血当场收回（血量刷新处钳制，只收不加）。
        /// </summary>
        public void RefreshGearBonuses()
        {
            // 穿戴源：4 槽逐件累计（纯求和，无分配）
            int defense = 0;
            float moveSpeed = 0f;
            int maxHealth = 0;
            bool fullIron = true; // m11 W2-4 B6：四槽是否恰好是铁套四件（边沿触发 EquipArmorFull）
            if (ArmorSlots != null && Items != null)
            {
                for (int i = 0; i < ArmorInventory.SlotCount; i++)
                {
                    ItemStack stack = ArmorSlots.GetSlot(i);
                    if (stack.IsEmpty)
                    {
                        fullIron = false;
                        continue;
                    }
                    if (!Items.TryGetByNumericId(stack.ItemId, out var worn))
                    {
                        fullIron = false;
                        continue;
                    }
                    fullIron &= worn.Id == IronSetIds[i];
                    GearBonuses piece = GearBonuses.FromDefinition(worn);
                    defense += piece.Defense;
                    moveSpeed += piece.MoveSpeedBonus;
                    maxHealth += piece.MaxHealthBonus;
                }
            }
            else
            {
                fullIron = false;
            }

            // 手持源：m10 语义原样（选中格一件，拿走失效）
            GearBonuses held = GearBonuses.FromDefinition(GetSelectedDefinition());
            Defense = defense + held.Defense;
            MoveSpeedBonus = moveSpeed + held.MoveSpeedBonus;
            MaxHealthBonus = maxHealth + held.MaxHealthBonus;
            Health.Current = GearBonusMath.ClampCurrentToEffectiveMax(
                Health.Current, Health.Max, MaxHealthBonus);

            // m11 W2-4 B6：四槽穿齐铁套的瞬间发一次 EquipArmorFull（chapter2 ch2_05）。
            // 边沿触发（非满→满才发）：本方法每帧调，不判边沿会每帧重复 Raise。
            // 「铁套」按 items 表 id 精确对表（铁盔/铁胸/铁护腿/铁靴各在其部位槽），
            // 金/合金/机元套不认——任务文案就是「穿齐铁盔甲」。
            if (fullIron && !_fullIronArmorRaised)
            {
                QuestEventBus.Instance?.Raise(new MyWorld.Core.Quests.QuestEvent
                {
                    Type = MyWorld.Core.Quests.QuestEventType.EquipArmorFull,
                });
            }
            _fullIronArmorRaised = fullIron;
        }

        /// <summary>铁套四件的物品 id，下标 = 穿戴槽号（0 头 / 1 胸 / 2 腿 / 3 脚），
        /// 与 items/iron_helmet|iron_chest|iron_legs|iron_boots.json 一致。</summary>
        private static readonly string[] IronSetIds =
            { "iron_helmet", "iron_chest", "iron_legs", "iron_boots" };

        /// <summary>上一帧是否处于「铁套穿齐」状态（EquipArmorFull 的边沿检测）。</summary>
        private bool _fullIronArmorRaised;

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
