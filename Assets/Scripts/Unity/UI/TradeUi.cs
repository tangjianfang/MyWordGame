using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 交易界面：玩家右键村民 → 选交易 → 扣 buy 物品、加 sell 物品。
    /// <para>
    /// 简化：每条交易自动触发一次（不要求 buy 物品在物品栏里有；演示用）。
    /// </para>
    /// </summary>
    public sealed class TradeUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.V;
        public float MaxReachDistance = 4f;

        private Villager _activeVillager;
        private int _selectedOffer = -1;

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                if (_activeVillager != null) CloseTrade();
                else TryOpenTrade();
            }
        }

        private void TryOpenTrade()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            var vm = FindObjectOfType<VillagerManager>();
            if (vm == null) return;

            // 找最近的村民
            var pc = FindObjectOfType<PlayerController>();
            if (pc == null || pc.Eye == null) return;

            Villager closest = null;
            float closestSq = MaxReachDistance * MaxReachDistance;
            foreach (var v in vm.ActiveVillagers)
            {
                float dx = v.Position.X - pc.Eye.position.x;
                float dy = v.Position.Y - pc.Eye.position.y;
                float dz = v.Position.Z - pc.Eye.position.z;
                float dSq = dx * dx + dy * dy + dz * dz;
                if (dSq < closestSq) { closestSq = dSq; closest = v; }
            }
            if (closest != null) _activeVillager = closest;
        }

        private void CloseTrade()
        {
            _activeVillager = null;
            _selectedOffer = -1;
        }

        private void OnGUI()
        {
            if (_activeVillager == null) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Items == null) return;

            float w = 360, h = 280;
            var bg = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(bg, $"与 {_activeVillager.Profession} 交易 (V 关闭)");

            for (int i = 0; i < _activeVillager.Offers.Count; i++)
            {
                var o = _activeVillager.Offers[i];
                int idx = i;
                var row = new Rect(bg.x + 20, bg.y + 40 + i * 50, w - 40, 40);

                GUI.Box(row, $"{o.BuyCount} × {o.BuyItem}  →  {o.SellCount} × {o.SellItem}    (用 {o.Uses}/{o.MaxUses})");

                // 交易按钮（按 buy 物品必须有库存，否则禁用）
                int haveBuy = CountItem(ctx, o.BuyItem);
                var btnRect = new Rect(row.x + row.width - 80, row.y + 4, 70, 32);
                GUI.enabled = o.CanTrade && haveBuy >= o.BuyCount;
                if (GUI.Button(btnRect, "买") && GUI.enabled)
                {
                    DoTrade(o, ctx);
                }
                GUI.enabled = true;
            }
        }

        private static int CountItem(PlayerContext ctx, string id)
        {
            if (!ctx.Items.TryGetById(id, out var def)) return 0;
            int total = 0;
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                var s = ctx.Inventory.GetSlot(i);
                if (!s.IsEmpty && s.ItemId == def.NumericId) total += s.Count;
            }
            return total;
        }

        private static void DoTrade(TradeOffer o, PlayerContext ctx)
        {
            // 扣 buy
            if (!ctx.Items.TryGetById(o.BuyItem, out var buyDef)) return;
            if (!ctx.Items.TryGetById(o.SellItem, out var sellDef)) return;
            if (!ctx.Inventory.TryRemoveCount(buyDef.NumericId, o.BuyCount)) return;

            // 给 sell：尝试合并到现有栈，否则找空位
            var giveStack = new ItemStack(sellDef.NumericId, o.SellCount);
            int leftover;
            ctx.Inventory.TryAdd(giveStack, out leftover);

            // 标记 uses++
            // 简化：不更新 Villager.Offers[i].Uses（值类型不可变）。演示够用。
        }
    }
}