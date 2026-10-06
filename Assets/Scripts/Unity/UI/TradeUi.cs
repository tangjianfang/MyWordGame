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
            if (!Input.GetKeyDown(ToggleKey)) return;
            // m6 终审修 C1（B3-③）：面板开着时按键 = 关面板；其它模态 UI 开着时不叠开
            if (_activeVillager != null) CloseTrade();
            else if (!UiCursorGate.IsOpen) TryOpenTrade();
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
            if (closest != null)
            {
                _activeVillager = closest;
                // m6 终审修 C1：交易面板是模态 UI——「买」按钮靠点击，登记指针门解锁指针
                UiCursorGate.Open();
            }
        }

        private void CloseTrade()
        {
            _activeVillager = null;
            _selectedOffer = -1;
            UiCursorGate.Close();
        }

        /// <summary>评审 04 R-6：级联关闭入口（幂等——已关再调 no-op）。</summary>
        private void CloseSelf() => CloseTrade();

        private void OnEnable() => UiCursorGate.RegisterClose(CloseSelf);

        private void OnDisable()
        {
            UiCursorGate.UnregisterClose(CloseSelf);
            // m6 终审修 C1（B3-②）：禁用/销毁时若面板还开着必须把门位还回去，否则计数泄漏
            if (_activeVillager != null) CloseTrade();
        }

        private void OnGUI()
        {
            if (_activeVillager == null) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Items == null) return;

            float w = 360, h = 280;
            var bg = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(bg, GUIContent.none);
            GUI.Label(new Rect(bg.x + 20, bg.y + 6, w - 40, 18),
                $"与 {_activeVillager.Profession} 交易 (V 关闭)", ItemSlotDrawer.WhiteStyle());

            var white = ItemSlotDrawer.WhiteStyle();
            for (int i = 0; i < _activeVillager.Offers.Count; i++)
            {
                var o = _activeVillager.Offers[i];
                var row = new Rect(bg.x + 20, bg.y + 40 + i * 50, w - 40, 40);
                GUI.Box(row, GUIContent.none);

                // m6 A2：buy/sell 槽换 ItemSlotDrawer 图标（旧实现是一行黑字叠深色 Box 不可读）
                const int iconSize = 32;
                float midY = row.y + (row.height - iconSize) * 0.5f;
                var buyRect = new Rect(row.x + 8, midY, iconSize, iconSize);
                var sellRect = new Rect(row.x + 130, midY, iconSize, iconSize);
                GUI.Label(new Rect(row.x + 44, midY + 7, 44, 18), "×" + o.BuyCount, white);
                GUI.Label(new Rect(row.x + 92, midY + 7, 32, 18), "→", white);
                GUI.Label(new Rect(row.x + 166, midY + 7, 44, 18), "×" + o.SellCount, white);
                if (ctx.Items.TryGetById(o.BuyItem, out var buyDef))
                    ItemSlotDrawer.Draw(buyRect, new ItemStack(buyDef.NumericId, o.BuyCount), ctx.Items, false);
                if (ctx.Items.TryGetById(o.SellItem, out var sellDef))
                    ItemSlotDrawer.Draw(sellRect, new ItemStack(sellDef.NumericId, o.SellCount), ctx.Items, false);

                GUI.Label(new Rect(row.x + 214, midY + 7, 44, 18), $"{o.Uses}/{o.MaxUses}", white);

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