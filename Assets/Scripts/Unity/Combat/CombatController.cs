using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 玩家主手攻击。左键挥剑，0.5 秒冷却。
    /// 命中 mob 时 raise <see cref="CombatEvents.OnDamageDealt"/>。
    /// 右键带汤 / 食物：吃。
    /// </summary>
    public sealed class CombatController : MonoBehaviour
    {
        public float AttackCooldown = 0.5f;
        public float AttackRange = 4f;
        public HandController Hand;
        public PlayerController Player;

        private float _cooldown;

        private static int DefaultToolDurability(int miningLevel)
        {
            // 木/石/铁/钻石/下界合金/基岩：35/65/125/156/203/255（取整到 255 上限内）
            switch (miningLevel)
            {
                case 1: return 35;     // 木
                case 2: return 65;     // 石
                case 3: return 125;    // 铁
                case 4: return 156;    // 钻石
                case 5: return 203;    // 下界合金
                case 99: return 255;   // 基岩
                default: return 50;
            }
        }

        private void Update()
        {
            if (_cooldown > 0) _cooldown -= Time.deltaTime;
            var ctx = PlayerContext.Instance;
            if (ctx == null || Player == null || Player.Eye == null) return;

            if (Input.GetMouseButtonDown(0) && _cooldown <= 0)
            {
                _cooldown = AttackCooldown;
                var def = ctx.GetSelectedDefinition();
                if (def != null && def.AttackDamage != null && def.AttackDamage.Value > 0)
                {
                    DoAttack(def.AttackDamage.Value);
                    if (Hand != null) Hand.TriggerSwing();
                    // 工具耐久：选中的工具如果还没设过 max durability，先设一次（plan3c）
                    if (def.IsTool)
                    {
                        int idx = ctx.Inventory.SelectedHotbarIndex;
                        var stack = ctx.Inventory.GetSlot(idx);
                        if (!stack.HasDurability)
                        {
                            ctx.Inventory.SetSlot(idx, stack.WithMaxDurability(DefaultToolDurability(def.MiningLevel)));
                        }
                        var after = ctx.Inventory.GetSlot(idx).DamageOnce();
                        ctx.Inventory.SetSlot(idx, after);
                        if (after.IsEmpty && Hand != null)
                        {
                            // 工具坏掉了：手部空挥
                            Hand.TriggerSwing();
                        }
                    }
                }
            }
            else if (Input.GetMouseButtonDown(1))
            {
                var def = ctx.GetSelectedDefinition();
                if (def != null && def.HealAmount != null)
                {
                    var stack = ctx.Inventory.GetSelected();
                    if (!stack.IsEmpty)
                    {
                        ctx.Health.Heal(def.HealAmount.Value);
                        ctx.Inventory.TryRemoveOne(ctx.Inventory.SelectedHotbarIndex);
                        if (Hand != null) Hand.TriggerSwing();
                    }
                }
            }
        }

        private void DoAttack(float damage)
        {
            // 简单射线：从眼睛朝 4 米。命中 mob 的 collider 即扣血
            var ctx = PlayerContext.Instance;
            var eye = Player.Eye;
            if (eye == null) return;
            var ray = new Ray(eye.position, eye.forward);
            if (Physics.Raycast(ray, out var hit, AttackRange))
            {
                var mobComp = hit.collider.GetComponent<MobView>();
                if (mobComp != null)
                {
                    var mob = mobComp.Mob;
                    mob.Health.Damage(damage);
                    mob.HitFlashTimer = 0.2f;
                    CombatEvents.RaiseDealt(new DamageEvent(
                        DamageSource.Melee, damage, attacker: 0, victim: mob.EntityId,
                        hit: new Float3(hit.point.x, hit.point.y, hit.point.z)));
                    CombatEvents.RaiseTaken(new DamageEvent(
                        DamageSource.Melee, damage, attacker: 0, victim: mob.EntityId,
                        hit: new Float3(hit.point.x, hit.point.y, hit.point.z)));
                    if (mob.Health.IsDead)
                    {
                        mob.State = MobState.Dying;
                        mob.DeathTimer = 0.5f;
                        CombatEvents.RaiseDied(new DamageEvent(
                            DamageSource.Melee, damage, attacker: 0, victim: mob.EntityId,
                            hit: new Float3(hit.point.x, hit.point.y, hit.point.z)));
                    }
                }
            }
        }
    }
}
