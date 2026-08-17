using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Farming;
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
    /// <para>
    /// m11 ②C（任务 2）：右键喂食繁殖路由 <see cref="TryFeedMobInCrosshair"/>——
    /// 手持对应饲料（<see cref="BreedingSystem.FeedItemFor"/>）+ 准星 4m 内瞄着 mob 时，
    /// 右键改走 <see cref="PlayerContext.BreedingSystem"/> 的喂食（发情/配对/孕期全在
    /// Core 系统内），扣 1 个饲料并<b>让出本次右键</b>（置 InputLocked 一帧让
    /// BlockInteraction 早退——beet/mung_bean 既是玩家食物又是饲料，不让位会被
    /// 它的「食物优先」分支双扣，见 <see cref="YieldRightClickToFeed"/>）。
    /// 左键攻击（<see cref="TryAttack"/>）完全不受影响。
    /// 为保证「喂食置锁一定赶在 BlockInteraction 读锁之前」，本组件挂
    /// <c>DefaultExecutionOrder(-500)</c>——攻击/喂食路径与其它组件无同帧先后依赖
    /// （挖矿分流走独立射线 <see cref="IsMobInCrosshair"/>，不读本组件状态），提前无副作用。
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-500)]
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

        /// <summary>m9 B1：命中打击音。挂玩家宿主链上的 PlayerAudioSystem
        /// （与 BlockInteraction.Bind 同款查找手法），首次命中懒解析一次缓存；
        /// 缺失 null 安全跳过（nice-to-have，不阻断战斗）。</summary>
        private MyWorld.Unity.Audio.PlayerAudioSystem _hitAudio;

        /// <summary>上次挥击时刻（<c>Time.time</c> 基准，含挥空的挥击）。public 是给 EditMode
        /// 测试的时间注入口（brief：时间注入或字段直改）——EditMode 下 <c>Time.time</c> 冻结，
        /// 测试直改本字段模拟冷却流逝；运行时代码只写不读外部值。</summary>
        public float LastAttackTime = float.NegativeInfinity;

        /// <summary>本帧右键已被喂食路由消费（<see cref="TryFeedMobInCrosshair"/> 置位）——
        /// <see cref="LateUpdate"/> 据此释放 <see cref="MyWorld.Unity.Player.BlockInteraction.InputLocked"/>。
        /// 一帧一个 bool 的记账，不 new 不闭包。</summary>
        private bool _yieldedRightClick;

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
            else if (Input.GetMouseButtonDown(1))
            {
                // m11 ②C：右键先给喂食路由一次机会——手持对应饲料且准星 4m 内是 mob
                // 才消费（TryFeedMobInCrosshair 内部自判，不适用返回 false 不拦截）；
                // 其余右键原样留给 BlockInteraction 的既有路由（吃/弓/锄/种/骨粉/床/放）。
                // else if 与 BlockInteraction 的「左键优先（同帧双按时不吃也不放）」同约定。
                TryFeedMobInCrosshair();
            }
        }

        /// <summary>
        /// 喂食让位标记的释放点：所有组件的 Update 都跑完（含 BlockInteraction 读到
        /// InputLocked 早退）之后，本组件的 LateUpdate 把锁放掉——锁的窗口恰好一帧，
        /// 下一帧 BlockInteraction 恢复既有行为。EditMode 不自动驱动，测试用反射调。
        /// </summary>
        private void LateUpdate()
        {
            if (!_yieldedRightClick) return;
            BlockInteraction.InputLocked = false;
            _yieldedRightClick = false;
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

            // 工具耐久：选中的工具如果还没设过 max durability，先设一次（plan3c）。
            // m10 B1 起上限优先取 items/*.json 的 maxDurability（镐类已声明，与挖矿扣减
            // 同源）；未声明的旧工具（剑/斧/锹）沿用 m3 的 MiningLevel 档位表——
            // 两条初始化路径永不给同一把工具写不同的 max
            if (def != null && def.IsTool)
            {
                int idx = ctx.Inventory.SelectedHotbarIndex;
                var stack = ctx.Inventory.GetSlot(idx);
                if (!stack.HasDurability)
                {
                    int max = def.MaxDurability > 0
                        ? def.MaxDurability
                        : DefaultToolDurability(def.MiningLevel);
                    ctx.Inventory.SetSlot(idx, stack.WithMaxDurability(max));
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
        /// m11 ②C（任务 2）：右键喂食繁殖路由（Update 的右键分流与 EditMode 测试共用，
        /// 与 <see cref="TryAttack"/> / <see cref="MyWorld.Unity.Player.BlockInteraction.UseAt"/>
        /// 同款做法——EditMode 驱动不了 <c>Input.GetMouseButtonDown</c>，直接调本方法）。
        /// 流程：指针门 → 手持物品解析 → 与攻击共用同一条准星射线
        /// （<see cref="FindMobHit"/>，含视线复核——喂/打看到的是同一只 mob）→
        /// 手持物品必须恰是这只 mob 的饲料（<see cref="BreedingSystem.FeedItemFor"/>）
        /// 才进 <see cref="BreedingSystem.TryFeed"/>（发情登记/配对/孕期全在 Core 系统内，
        /// 幼崽不发情、已发情重复喂都会被它拒掉）。喂成 → 挥手 + 扣 1 个饲料 +
        /// <see cref="YieldRightClickToFeed"/> 让出本次右键。
        /// <para>
        /// 左键攻击（<see cref="TryAttack"/>）与本路由互不影响：TryAttack 仍按攻击冷却
        /// 照常挥击，本路由不占冷却（喂食可连点，但 TryFeed 自己挡重复喂）。
        /// </para>
        /// </summary>
        /// <returns>true = 本次右键喂成了一只 mob（调用方不应再有其它右键行为）。</returns>
        public bool TryFeedMobInCrosshair()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || Player == null || Player.Eye == null) return false;

            // 与 TryAttack 同款指针门：模态 UI 开着时点 UI 不隔着界面喂 mob
            if (BlockInteraction.InputLocked || UiCursorGate.IsOpen) return false;

            // 系统未接好（数据表缺失时 WorldBootstrap 置 null）＝ 右键整体落回既有路由
            var breeding = ctx.BreedingSystem;
            if (breeding == null || ctx.Inventory == null) return false;

            var def = ctx.GetSelectedDefinition();
            if (def == null) return false;

            if (!FindMobHit(Player.Eye, World, Registry, out var hit)) return false;
            var mob = hit.collider.GetComponent<MobView>().Mob;
            if (!mob.IsAlive) return false; // 尸体不喂（右键不消费，落回既有路由）

            // 不是这只 mob 的饲料 → 本次右键不归喂食（BlockInteraction 照常吃/放：
            // 拿小麦对着鸡（要麦种）右键，玩家自己把小麦吃了是合理归宿）
            if (BreedingSystem.FeedItemFor(mob.Kind) != def.Id) return false;

            if (!breeding.TryFeed(mob.EntityId, mob.Kind, mob.Position, def.Id)) return false;

            // 喂食成立：挥手 + 扣 1 个饲料（与 BlockInteraction 吃食物同款扣法）+ 让出右键
            if (Hand != null) Hand.TriggerSwing();
            ctx.Inventory.TryRemoveOne(ctx.Inventory.SelectedHotbarIndex);
            YieldRightClickToFeed();
            return true;
        }

        /// <summary>
        /// m11 ②C：把本次右键整体让给喂食——置
        /// <see cref="MyWorld.Unity.Player.BlockInteraction.InputLocked"/>（跨组件共享的
        /// 静态输入门，语义「玩家组件这一帧不该响应世界交互」）让 BlockInteraction.Update
        /// 本帧早退：既不会把 beet/mung_bean 这类「既是玩家食物又是饲料」的物品再吃掉一份
        /// （m7 A3 起右键食物优先，双路并存一次右键双扣），也不会对着 mob 身后放方块。
        /// <para>
        /// 时序保障：本组件挂 <c>DefaultExecutionOrder(-500)</c>，Update 先于
        /// BlockInteraction（默认 order 0）执行，置锁一定赶在它读锁之前；
        /// <see cref="LateUpdate"/>（所有 Update 之后）释放，锁窗口恰好一帧。
        /// 已知边角：喂食同帧恰好按 H/Esc 打开帮助/暂停菜单时，菜单置的锁会被
        /// LateUpdate 误放（一次同帧双输入），菜单关开一次即自愈，接受。
        /// </para>
        /// </summary>
        private void YieldRightClickToFeed()
        {
            BlockInteraction.InputLocked = true;
            _yieldedRightClick = true;
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

            // m9 B1：战斗手感四件套——闪红 / 击退 / 命中音效，致死一击加缩小动画。
            // 反馈组件由 MobManager.SpawnMob 挂在 mob 宿主上（与 MobView 同 GameObject）；
            // 旧测试宿主没挂它则跳过（null 安全，不破坏既有行为）。
            // 击退方向 = 远离玩家的水平分量（归一与零向量兜底在 ApplyKnockback 内做）。
            var feedback = mobComp.GetComponent<MobHitFeedback>();
            if (feedback != null)
            {
                feedback.FlashRed();
                feedback.ApplyKnockback(new Vector3(
                    mob.Position.X - attackerPos.X, 0f, mob.Position.Z - attackerPos.Z));
                if (killed) feedback.PlayDeathShrink();
            }
            if (_hitAudio == null) _hitAudio = GetComponentInParent<MyWorld.Unity.Audio.PlayerAudioSystem>();
            _hitAudio?.PlayHit();
            // av W3-13：mob 受击音（按 kind 优先查专属，缺文件回退 generic-small/large）
            mobComp.GetComponent<MyWorld.Unity.Audio.MobAudioSystem>()?.PlayHurt();

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
