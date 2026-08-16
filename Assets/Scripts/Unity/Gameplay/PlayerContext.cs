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

        /// <summary>世界里正在飞的掉落物（B7 <see cref="ItemDropEntity"/>）。
        /// 挖方块时 spawn；m7 B1 起 <see cref="MyWorld.Unity.Player.PlayerController.PickupNearbyDrops"/>
        /// 每帧按吸附语义推进（进 2.5m 圈飞向玩家，贴脸入包），
        /// <see cref="MyWorld.Unity.Items.ItemDropViewRegistry"/> 同步建/毁小方块视图。
        /// 永不为 null，直接 Add / Remove 即可。</summary>
        public readonly System.Collections.Generic.List<ItemDropEntity> ItemDrops =
            new System.Collections.Generic.List<ItemDropEntity>();
        public MyWorld.Unity.UI.DeathScreenUi DeathScreen;

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
            if (Items == null) return null;
            var stack = Inventory.GetSelected();
            if (stack.IsEmpty) return null;
            return Items.TryGetByNumericId(stack.ItemId, out var def) ? def : null;
        }
    }
}
