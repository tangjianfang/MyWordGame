using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 图鉴解锁喂点（m12 W2）——把两路事件翻译成 <see cref="Core.Codex.CodexSystem"/> 解锁：
    /// <list type="bullet">
    /// <item><see cref="CombatEvents.OnDamageDealt"/>（玩家命中 mob）=「见过这只生物」
    /// ——比击杀更宽（打到就算遇到），照 QuestEventBus.HandleEntityDied 同款
    /// MobManager.TryGetMobById 反查（含懒找宿主）</item>
    /// <item><see cref="BlockInteraction.BlockBroken"/>（挖掉方块）=「挖到这个方块」
    /// ——花草/作物两条破坏路径都发这同一个事件，挂它即全覆盖</item>
    /// </list>
    /// 击杀与获得钻石走总线事件（QuestEventBus.Raise 转发 CodexSystem.OnQuestEvent），
    /// 不在这里重复订阅。挂 WorldBootstrap（PlayerContext 建好之后）。
    /// </summary>
    public sealed class CodexHost : MonoBehaviour
    {
        private PlayerContext _ctx;
        private BlockRegistry _registry;
        private MobManager _mobManager;

        public void Bind(PlayerContext context, BlockRegistry registry)
        {
            _ctx = context;
            _registry = registry;
        }

        private void OnEnable()
        {
            CombatEvents.OnDamageDealt += HandleDamageDealt;
            BlockInteraction.BlockBroken += HandleBlockBroken;
        }

        private void OnDisable()
        {
            CombatEvents.OnDamageDealt -= HandleDamageDealt;
            BlockInteraction.BlockBroken -= HandleBlockBroken;
        }

        private void HandleDamageDealt(DamageEvent ev)
        {
            if (_ctx == null || _ctx.Codex == null) return;
            if (ev.VictimEntityId == 0) return;   // 打的不是 mob
            if (ev.AttackerEntityId != 0) return; // 不是玩家打的

            if (_mobManager == null)
            {
                _mobManager = FindObjectOfType<MobManager>();
                if (_mobManager == null) return;
            }

            if (_mobManager.TryGetMobById(ev.VictimEntityId, out var mob))
            {
                _ctx.Codex.SeeMob(mob.Kind);
            }
        }

        private void HandleBlockBroken(int x, int y, int z, ushort blockId)
        {
            if (_ctx == null || _ctx.Codex == null || _registry == null) return;
            var def = _registry.GetByNumericId(blockId);
            if (def != null)
            {
                _ctx.Codex.UnlockBlock(def.Id);
            }
        }
    }
}
