using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 玩家主手攻击。左键挥击，0.5 秒冷却。
    /// 命中 mob 时 raise <see cref="CombatEvents.OnDamageDealt"/>。
    /// <para>
    /// m9 A1（修断环①）：删掉「选中物品 attackDamage &gt; 0 才可攻击」的门禁——旧逻辑下
    /// 空手 / 木板对动物完全无效。现在任何左键都能挥击，伤害按
    /// <see cref="ResolveAttackDamage"/> 解析（空手/非武器 1 点，武器取物品表 attackDamage）。
    /// mob 命中优先于挖掘：<see cref="MyWorld.Unity.Player.BlockInteraction"/> 挖掘前查
    /// <see cref="IsMobInCrosshair"/>，准星 4m 内瞄着 mob 时同帧不挖。
    /// </para>
    /// <para>
    /// m9 A1 fix1（评审 I1+I2）：I1——攻击入口挂与 BlockInteraction 同款的模态 UI 指针门
    /// （<see cref="MyWorld.Unity.Player.BlockInteraction.InputLocked"/> /
    /// <see cref="UiCursorGate.IsOpen"/>），菜单开着点 UI 不再隔着界面打 mob；I2——chunk mesh
    /// 没有 Physics collider，mob 射线会穿墙命中，命中前用 <see cref="VoxelRaycaster"/> 做
    /// 体素视线复核（玩家→mob 之间有实心方块则不命中）。
    /// </para>
    /// <para>
    /// m7 A3：右键吃食物已移交 <see cref="MyWorld.Unity.Player.BlockInteraction"/>（统一右键路由，
    /// 经 HungerSystem.Eat 喂饥饿）。原先这里右键吃食物回的是 Health——既绕过了饥饿系统，
    /// 又和放方块抢同一次右键，留着必然双重消耗物品，故整支删除。
    /// </para>
    /// <para>
    /// m9 A3（修断环③）：伤害统一走 <see cref="MyWorld.Core.Entities.MobAI.TakeHit"/>——
    /// 扣血/红闪/受击逃跑/死亡序列（Dying + LastDrops + 死因标记）全在 Core 序列内，
    /// 本组件不再直扣 Health、不再直置 Dying。打死掉肉由
    /// <see cref="MyWorld.Unity.Combat.MobManager.SpawnDropsForMob"/> 消费 LastDrops，
    /// 击杀经验由 <see cref="MyWorld.Unity.Combat.MobManager.KillExperience"/> 入账。
    /// </para>
    /// </summary>
    public sealed class CombatController : MonoBehaviour
    {
        /// <summary>攻击范围（米）：准星射线找 mob 的最大距离。m9 A1 提为常量（曾是与
        /// 僵尸射程相同的实例字段，全仓库无外部引用，收敛成常量防散落改一处漏一处）。</summary>
        public const float AttackRange = 4f;

        /// <summary>攻击冷却（秒）：两次挥击的最小间隔。</summary>
        public const float AttackCooldown = 0.5f;

        public HandController Hand;
        public PlayerController Player;

        /// <summary>fix1（I2）：视线复核用的世界。由 WorldBootstrap 注入；
        /// null（旧场景 / 无世界测试）时与 <see cref="Registry"/> 一起跳过复核，保持纯射线行为。</summary>
        public World World;

        /// <summary>fix1（I2）：视线复核用的方块表（判 Solid）。与 <see cref="World"/> 成对注入。</summary>
        public BlockRegistry Registry;

        /// <summary>上次挥击时刻（<c>Time.time</c> 基准，含挥空的挥击）。public 是给 EditMode
        /// 测试的时间注入口（brief：时间注入或字段直改）——EditMode 下 <c>Time.time</c> 冻结，
        /// 测试直改本字段模拟冷却流逝；运行时代码只写不读外部值。</summary>
        public float LastAttackTime = float.NegativeInfinity;

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
            var ctx = PlayerContext.Instance;
            if (ctx == null || Player == null || Player.Eye == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                TryAttack();
            }
        }

        /// <summary>
        /// m9 A1：主手攻击统一入口。Update 的左键路由与 EditMode 测试共用（EditMode 驱动不了
        /// <c>Input.GetMouseButtonDown</c>，直接调本方法，与 BlockInteraction.UseAt / BreakAt
        /// 同款做法）。流程：冷却节流 → 挥手动画 → <see cref="ResolveAttackDamage"/> 解析伤害 →
        /// 准星射线找 4m 内 mob 并扣血。
        /// </summary>
        /// <returns>true = 本次挥击命中了 mob。挖矿分流不走这个返回值——两个 Update 的执行顺序
        /// 无保证，<see cref="MyWorld.Unity.Player.BlockInteraction"/> 用同源射线
        /// <see cref="IsMobInCrosshair"/> 独立判定，与本方法永不漂移。</returns>
        public bool TryAttack()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || Player == null || Player.Eye == null) return false;

            // fix1（I1）：模态 UI 开着时不攻击——与 BlockInteraction.Update 同款指针门。
            // 去门禁（m9 A1）后这里成了新漏洞：菜单里点滑条/格子会隔着 UI 打到准星后的 mob。
            if (BlockInteraction.InputLocked || UiCursorGate.IsOpen) return false;

            // 冷却节流：距上次挥击不足 AttackCooldown 直接吞掉本次点击（含挥空也算冷却）
            if (Time.time - LastAttackTime < AttackCooldown) return false;
            LastAttackTime = Time.time;

            // 空手也有挥拳动画（旧代码只有握武器才挥手）
            if (Hand != null) Hand.TriggerSwing();

            var def = ctx.GetSelectedDefinition();
            int damage = ResolveAttackDamage(ctx.Inventory.GetSelected(), ctx.Items);
            bool hitMob = DoAttack(damage);

            // 工具耐久：选中的工具如果还没设过 max durability，先设一次（plan3c）
            if (def != null && def.IsTool)
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

            return hitMob;
        }

        /// <summary>
        /// m9 A1：主手攻击伤害解析（纯函数，三态）：
        /// 空手（null 栈 / 空栈 / 无物品表 / 表里查不到定义）→ 1；
        /// 非武器（<see cref="ItemDefinition.AttackDamage"/> 为 null 或 ≤0，如木板、食物）→ 1；
        /// 武器 → 物品表 attackDamage（向上取整——stone_sword 5.5 进位 6，不丢半点伤害）。
        /// </summary>
        public static int ResolveAttackDamage(ItemStack? selected, ItemDatabase items)
        {
            if (selected == null || selected.Value.IsEmpty || items == null) return 1;
            if (!items.TryGetByNumericId(selected.Value.ItemId, out var def) || def == null) return 1;
            return def.AttackDamage.HasValue && def.AttackDamage.Value > 0f
                ? Mathf.CeilToInt(def.AttackDamage.Value)
                : 1;
        }

        /// <summary>
        /// m9 A1：准星 <see cref="AttackRange"/> 内是否瞄着 mob——「mob 命中优先于挖掘」的
        /// 分流信号，<see cref="MyWorld.Unity.Player.BlockInteraction"/> 挖掘前调用
        /// （准星瞄着 mob 时本帧左键归攻击，不挖 mob 身后的方块）。与 <see cref="TryAttack"/>
        /// 的找目标共用同一条射线（<see cref="FindMobHit"/>），含 fix1（I2）的视线复核——
        /// 墙后有 mob 时不抑制挖矿（正好挖那堵墙）。
        /// </summary>
        public static bool IsMobInCrosshair(Transform eye, World world, BlockRegistry registry)
        {
            return FindMobHit(eye, world, registry, out _);
        }

        /// <summary>
        /// 准星射线找 mob：从眼睛朝 <see cref="AttackRange"/> 米，命中最近 collider 且挂着
        /// <see cref="MobView"/> 才算命中（判定方式自旧 DoAttack 原样沿用——部位 cube 的
        /// collider 已被 MobAssembly 移除，射线只认 host 的 BoxCollider）。
        /// fix1（I2）：chunk mesh 没有 Physics collider，这条射线<b>会穿墙</b>——命中后必须经
        /// <see cref="IsOccluded"/> 体素视线复核（玩家→mob 之间无实心方块）才算数。
        /// </summary>
        private static bool FindMobHit(Transform eye, World world, BlockRegistry registry, out RaycastHit hit)
        {
            hit = default;
            if (eye == null) return false;
            var ray = new Ray(eye.position, eye.forward);
            if (!Physics.Raycast(ray, out hit, AttackRange)) return false;
            if (hit.collider == null || hit.collider.GetComponent<MobView>() == null) return false;
            return !IsOccluded(eye, world, registry, hit.distance);
        }

        /// <summary>
        /// fix1（I2）：体素视线复核——沿同一条眼射线跑 <see cref="VoxelRaycaster"/>
        /// （<see cref="WorldSolidSource"/> 判 Solid），命中点比 mob 更近处有实心方块即被遮挡。
        /// 世界/方块表未注入（null）时跳过复核，保持纯射线旧行为（旧场景 / 无世界测试）。
        /// </summary>
        private static bool IsOccluded(Transform eye, World world, BlockRegistry registry, float mobDistance)
        {
            if (world == null || registry == null) return false;
            var source = new WorldSolidSource(world, registry);
            var origin = new Float3(eye.position.x, eye.position.y, eye.position.z);
            var direction = new Float3(eye.forward.x, eye.forward.y, eye.forward.z);
            var voxel = VoxelRaycaster.Cast(source, origin, direction, mobDistance);
            return voxel.Hit && voxel.Distance < mobDistance;
        }

        private bool DoAttack(float damage)
        {
            // 简单射线：从眼睛朝 4 米。命中 mob 的 collider 即受击（视线被墙挡则不命中）
            if (!FindMobHit(Player.Eye, World, Registry, out var hit)) return false;

            var mobComp = hit.collider.GetComponent<MobView>();
            var mob = mobComp.Mob;
            // m9 A3（修断环③）：伤害改走 MobAI.TakeHit 统一入口——扣血 + 受击红闪 +
            // 被动逃跑都在 Core 序列内；致死时由 TakeHit 死亡分支转 Dying + 写 LastDrops
            // + 置 KilledByPlayer。Unity 侧不再直扣 Health / 直置 Dying——旧直置路径绕过
            // MobAI 的 LastDrops 写入（Tick 首行 !IsAlive 早退令其不可达），打死不掉肉。
            // 伤害值由 ResolveAttackDamage 解析后传入（空手 1 / 武器 attackDamage）。
            var attackerPos = new Float3(
                Player.Eye.position.x, Player.Eye.position.y, Player.Eye.position.z);
            bool killed = MobAI.TakeHit(mob, attackerPos, damage);
            CombatEvents.RaiseDealt(new DamageEvent(
                DamageSource.Melee, damage, attacker: 0, victim: mob.EntityId,
                hit: new Float3(hit.point.x, hit.point.y, hit.point.z)));
            CombatEvents.RaiseTaken(new DamageEvent(
                DamageSource.Melee, damage, attacker: 0, victim: mob.EntityId,
                hit: new Float3(hit.point.x, hit.point.y, hit.point.z)));
            if (killed)
            {
                // 击杀事件只在致死一击发（TakeHit 返回 true；尸体补刀经守卫短路不再重发）
                CombatEvents.RaiseDied(new DamageEvent(
                    DamageSource.Melee, damage, attacker: 0, victim: mob.EntityId,
                    hit: new Float3(hit.point.x, hit.point.y, hit.point.z)));
            }
            return true;
        }
    }
}
