using MyWorld.Core.Blocks;
using MyWorld.Core.Enchanting;
using MyWorld.Core.Entities;
using MyWorld.Core.Farming;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Audio;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Items;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.UI;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 鼠标射线选中 → 线框高亮 → 左键挖 / 右键放。
    /// <para>
    /// 数据源全部走 Core（<see cref="VoxelRaycaster"/> + <see cref="BlockPlacement"/> +
    /// <see cref="World.SetBlock"/> + <see cref="DirtySections"/>），Unity 侧只负责鼠标轮询、
    /// 调用顺序、和把脏段交给 <see cref="ChunkViewRegistry"/> 重建。
    /// </para>
    /// <para>
    /// <b>m11 ②右键路由优先级表</b>（<see cref="UseAt"/>，自上而下首中即止）：
    /// 死亡画面让位 → 食物优先（m7 A3）→ 附魔书融合（m11 W2-2，无需命中）→
    /// 弓蓄力（m11 ②，无需命中）→ 机元图腾召唤 Boss（m11 W3-3）→
    /// 锄草/泥成耕地 →
    /// 种子播上耕地 → 骨粉催熟（树苗让位 SaplingGrowth）→ 床睡觉 → 箱子开箱（m11 W2-3
    /// <see cref="UI.ChestUi.OpenAt"/>）→ 木门/铁门让位 RedstoneSystem 切换 →
    /// 放手持物品对应的方块（m11 W3-1 <see cref="ItemDefinition.BlockId"/>；
    /// 无 blockId 物品/空手回落 placeBlockId 占位，m3 语义保持）。
    /// 左键在成熟作物（*_stage2）上改走 <see cref="FarmSystem.Harvest"/>（<see cref="BreakAt"/>）。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class BlockInteraction : MonoBehaviour
    {
        [SerializeField] private ushort placeBlockId = BlockIds.Stone;
        [SerializeField] private Material selectionMaterial;

        /// <summary>m6 B3：静态输入锁。HelpMenuUi 打开期间置 true，<c>Update</c> 开头早退——
        /// 菜单里点滑条不应误挖/误放方块。静态门由 HelpMenuUi 的开关维护，
        /// 挂在玩家身上的组件实例共享这一个全局状态。</summary>
        public static bool InputLocked;

        /// <summary>
        /// m11 W3-4：方块被挖掉/收获的公开事件（坐标 + 被挖前方块 id）——纯视觉订阅点
        /// （<see cref="MyWorld.Unity.FX.ParticlePool"/> 挖掘碎屑）。本类不持订阅者、
        /// 不感知谁在听；无订阅者时 <c>?.Invoke</c> 零开销，玩法语义零变化。
        /// 静态事件与 <see cref="InputLocked"/> 同约定：挂在玩家身上的实例共享。
        /// </summary>
        public static event System.Action<int, int, int, ushort> BlockBroken;

        private PlayerController _player;
        private World _world;
        private BlockRegistry _registry;
        private ChunkViewRegistry _views;
        private SelectionBox _selection;
        private PlayerAudioSystem _audio;
        private BlockDrops _blockDrops;

        /// <summary>
        /// 农田系统（m11 ②）。运行时真源是 <see cref="PlayerContext.FarmSystem"/>
        /// （<c>WorldBootstrap</c> 建好挂上去，与 <see cref="FarmingHost"/> / 存档链共用同一实例）；
        /// <see cref="SetFarmSystem"/> 只是 EditMode fixture 的直注优先通道。
        /// 解析不到（数据表缺失时 WorldBootstrap 置 null）= 四条路由整体让位给放方块，不抛不挡。
        /// </summary>
        private FarmSystem _farm;

        /// <summary>
        /// 床系统（m11 ②）。真源 <see cref="PlayerContext.BedSystem"/>，setter 直注优先，
        /// 生命周期与存档往返（<c>BedSpawnPoints</c>）由宿主负责，这里只消费
        /// <see cref="BedSystem.Sleep"/>。
        /// </summary>
        private BedSystem _beds;

        /// <summary>
        /// 附魔存储（m11 W2-2）。PlayerContext 是并行波次热点文件挂不了字段，
        /// 运行时走 <see cref="EnchantStore.Default"/> 单例（效果接线与存档接线共用同一份）；
        /// <see cref="SetEnchantStore"/> 是 EditMode fixture 的直注优先通道（测试互不共享静态状态）。
        /// </summary>
        private EnchantStore _enchants;

        /// <summary>农田系统双源解析：EditMode 直注优先，否则读 PlayerContext.FarmSystem。</summary>
        private FarmSystem ResolveFarm()
        {
            if (_farm != null) return _farm;
            var ctx = PlayerContext.Instance;
            return ctx == null ? null : ctx.FarmSystem;
        }

        /// <summary>床系统双源解析：EditMode 直注优先，否则读 PlayerContext.BedSystem。</summary>
        private BedSystem ResolveBeds()
        {
            if (_beds != null) return _beds;
            var ctx = PlayerContext.Instance;
            return ctx == null ? null : ctx.BedSystem;
        }

        public void Bind(World world, BlockRegistry registry, ChunkViewRegistry views, Transform parent)
        {
            _player = GetComponent<PlayerController>();
            _world = world;
            _registry = registry;
            _views = views;
            // 父链上找 PlayerAudioSystem（WorldBootstrap 与 Player 同 GameObject），
            // 找不到也无所谓——_audio 为 null，place/break 时跳过音效即可（nice-to-have）。
            _audio = GetComponentInParent<PlayerAudioSystem>();

            if (selectionMaterial == null)
            {
                // m5 A4：黑 35% 半透明叠加（Minecraft 式融合变暗）。
                // 旧实现是纯白不透明 Unlit——整块 Cube mesh 把选中方块的贴图完全盖住。
                selectionMaterial = UrpMaterialFactory.CreateOverlay(new Color(0f, 0f, 0f, 0.35f));
            }

            _selection = SelectionBox.Create(parent, selectionMaterial);
            _selection.Hide();
        }

        /// <summary>
        /// X2 fix-up：注入方块→物品掉落表。无表时挖方块不产生掉落（保持旧行为）。
        /// 由 <c>WorldBootstrap</c> 在 BlockDefinitionFilesTests + ItemDatabase 配齐后调用。
        /// 传 null 也允许——和没注入表的行为一致——但会打 warning 让上游忘记注入更容易被发现。
        /// </summary>
        public void SetBlockDrops(BlockDrops drops)
        {
            if (drops == null)
            {
                Debug.LogWarning("[BlockInteraction] SetBlockDrops 传入了 null，挖方块时不会产生掉落。");
            }
            _blockDrops = drops;
        }

        /// <summary>
        /// m11 ②：EditMode fixture 直注农田系统（优先于 <see cref="PlayerContext.FarmSystem"/>）。
        /// 运行时不需要调——WorldBootstrap 已把同一实例挂到 PlayerContext 上，
        /// 这里走 <see cref="ResolveFarm"/> 双源解析。传 null 会打 warning 方便发现装配遗漏。
        /// </summary>
        public void SetFarmSystem(FarmSystem farm)
        {
            if (farm == null)
            {
                Debug.LogWarning("[BlockInteraction] SetFarmSystem 传入了 null，锄/种/骨粉/收获路由不会生效。");
            }
            _farm = farm;
        }

        /// <summary>
        /// m11 ②：EditMode fixture 直注床系统（优先于 <see cref="PlayerContext.BedSystem"/>，
        /// 见 <see cref="ResolveBeds"/>）。运行时不需要调。
        /// </summary>
        public void SetBedSystem(BedSystem beds)
        {
            if (beds == null)
            {
                Debug.LogWarning("[BlockInteraction] SetBedSystem 传入了 null，右键床不会触发睡觉。");
            }
            _beds = beds;
        }

        private void Update()
        {
            // m6 终审修 C1：InputLocked（帮助菜单维护）之外，任一模态 UI 开着也抑制——
            // 指针解锁后点击 UI 格子，绝不能顺手挖掉准星后面的方块
            if (InputLocked || UiCursorGate.IsOpen)
            {
                // 有模态 UI 开着：不射线拾取、不响应挖/放，顺便藏掉选中框
                _selection?.Hide();
                // m11 ②：弓蓄力一并取消——门开着时本方法提前 return，看不到
                // GetMouseButtonUp，蓄力会冻结成「永远拉满」，直接作废最干净
                _bowCharging = false;
                // m13 W3：火枪开火后装填锁定——门开期间输入被门吞掉，装填窗自然不会被外部刷新。
                // 装填窗本身在 UseAt → TryFireMusket 内部消费时设，不依赖 Update——门开只是
                // 把后续右键吃掉，不会让已锁的装填窗失稳。
                return;
            }

            if (_world == null || _player.Eye == null)
            {
                _bowCharging = false; // 同上：看不到松键事件的状态一律作废
                return;
            }

            // m11 ②：弓蓄力状态机（开始在 UseAt 的弓分支，松键在这里结算）
            TickBowCharge();

            // m11 ②：交互专用射线源——作物/花草/火把/家具这类 solid=false 的造型方块
            // 也要能被准星命中（骨粉/收获/拆装饰的先决条件），水仍不命中
            var source = new InteractionRaySource(_world, _registry);
            Float3 origin = ToFloat3(_player.Eye.position);
            Float3 direction = ToFloat3(_player.Eye.forward);
            float maxDist = _player.Settings.ReachDistance;

            VoxelRayHit hit = VoxelRaycaster.Cast(source, origin, direction, maxDist);

            if (hit.Hit)
            {
                _selection.ShowAt(hit.X, hit.Y, hit.Z);

                // m9 A1 分流：mob 命中优先于挖掘——准星 4m 内瞄着 mob 时本帧左键归攻击
                // （CombatController 同帧会挥击），不挖 mob 身后的方块。判定与攻击共用
                // 同一条射线（CombatController.IsMobInCrosshair ↔ TryAttack），两路永不漂移；
                // 不依赖两个 Update 的执行顺序，所以这里独立查询而非读攻击方的返回值。
                // fix1（I2）后该判定含视线复核：墙后有 mob 时不抑制——正好挖那堵墙。
                if (Input.GetMouseButtonDown(0)
                    && !CombatController.IsMobInCrosshair(_player.Eye, _world, _registry))
                {
                    // 挖：把命中格设为空气，标脏，重建，并按 BlockDrops spawn ItemDropEntity
                    BreakAt(hit.X, hit.Y, hit.Z);
                }
            }
            else
            {
                _selection.Hide();
            }

            // m7 A3：右键路由挪出 hit.Hit 分支——选中食物时看天 / 看远处（射线落空）
            // 也必须能吃，原逻辑只有命中才能右键，饿急了抬头就吃不上东西。
            // 左键仍优先（同帧双按时不吃也不放），非食物保持"命中才放"不变。
            if (Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(0))
            {
                UseAt(hit);
            }
        }

        /// <summary>
        /// m7 A3：右键交互统一入口（<c>Update</c> 与 EditMode 测试共用——EditMode 驱动不了
        /// <c>Input.GetMouseButtonDown</c>，直接调本方法，与 <see cref="BreakAt"/> 同款做法）。
        /// 选中槽是食物（<see cref="ItemDefinition.IsEdible"/>）→ 吃 1 个：经
        /// <see cref="HungerSystem.Eat"/>（唯一进食入口）恢复 Hunger/Saturation 并扣 1 个物品，
        /// <b>本次右键到此为止，不再放方块</b>（食物优先）；否则射线命中时按 m11 W3-1
        /// 放置路由落块（手持物品带 <see cref="ItemDefinition.BlockId"/> 放对应方块并扣 1 个，
        /// 否则照旧放 <see cref="placeBlockId"/> 占位）。
        /// <para>
        /// m11 ②：食物之后、放方块之前新插一排交互路由（优先级自上而下）：
        /// 附魔书融合（m11 W2-2，不需要命中方块）→
        /// 弓（蓄力，不需要命中方块）→ 机元图腾召唤 Boss（m11 W3-3，命中 2×2 机元矿石
        /// 图腾才消费）→ 锄草/泥成耕地 → 种子播上耕地 → 骨粉催熟作物
        /// （树苗让位给 <see cref="MyWorld.Unity.Environment.SaplingGrowth"/> 的既有右键即长，
        /// 这里只挡放置）→ 床睡觉 → 箱子开箱（m11 W2-3，<see cref="UI.ChestUi.OpenAt"/>）→
        /// 门让位给 <see cref="MyWorld.Unity.Environment.RedstoneSystem"/> 的既有切换。
        /// 每条路由消费右键后<b>不再放方块</b>；条件不满足则落到下一条，全部落空才放。
        /// </para>
        /// </summary>
        public void UseAt(VoxelRayHit hit)
        {
            // m10 C2 fix1（I1）：死亡画面可见时右键整次让位给复活——同一次右键不能再
            // 顺手吃掉手持食物 / 放方块（双触发）。与模态 UI 的指针门同思路，但死亡画面
            // 不开指针门，单独看 DeathScreen.IsVisible（DestroyImmediate 后为 fake-null，
            // 视同「没有死亡画面」，不影响正常游戏）。
            var gateCtx = PlayerContext.Instance;
            if (gateCtx != null && gateCtx.DeathScreen != null && gateCtx.DeathScreen.IsVisible)
            {
                return;
            }

            if (TryEatSelectedFood())
            {
                return; // 食物优先，不再放方块
            }

            // m11 W2-2：手持附魔书 → 右键整次被融合消费（书消失、背包第一件可附魔装备带魔）。
            // 照食物/弓同思路不要求命中方块：朝天也能把书拍到装备上。
            if (TryFuseEnchantedBook())
            {
                return; // 融合消费右键，不再放方块
            }

            // m11 ②：手持弓 → 右键整个被蓄力消费（无箭不开弓，给一次性提示）。
            // 弓分支不要求射线命中（照吃不要求命中的 m7 A3 思路：朝天也能拉弓）。
            if (IsBowSelected())
            {
                TryStartBowCharge();
                return;
            }

            // m13 W3：手持火枪 → 右键整个被开火/装填分支消费（无弹咔哒，装填中拒绝）。
            // 与弓同思路不要求射线命中：朝天也能开火（直弹道武器不需要视线）。
            if (IsMusketSelected())
            {
                TryFireMusket();
                return;
            }

            if (!hit.Hit) return;
            ushort target = _world.GetBlock(hit.X, hit.Y, hit.Z);

            // ── m11 ②交互路由（全部在「放方块」之前，见类注释的路由优先级表） ──
            if (TrySummonMachineGuardian(hit, target)) return;
            if (TryTillWithHoe(hit, target)) return;
            if (TryPlantSeeds(hit, target)) return;
            if (TryApplyBoneMeal(hit, target)) return;
            if (TrySleepInBed(hit, target)) return;
            if (target == BlockIds.Chest)
            {
                // m11 W2-3：右键箱子开箱子 UI（替换 v1 no-op 占位，本分支本卡独占）。
                // UI 组件懒挂在玩家宿主上（WorldBootstrap 本波禁改，不新增装配步骤）；
                // ChestSystem 未就绪（数据表缺失降级）时 OpenAt 返回 null，保持消费右键不放方块。
                ChestUi.OpenAt(gameObject, PlayerContext.Instance, hit.X, hit.Y, hit.Z);
                return;
            }
            if (target == BlockIds.WoodenDoor || target == IronDoorBlockId)
            {
                return; // 门：切换由 RedstoneSystem 自己的右键通道做，这里只挡放置（双动）
            }

            // 放：尝试解算放置位置（含 IntersectsPlayer 防卡身），合法就按 m11 W3-1 放置路由落块
            Aabb playerBox = Aabb.FromBottomCenter(_player.State.Position,
                _player.Settings.Width, _player.Settings.Height);
            if (BlockPlacement.TryResolve(hit, playerBox, out int x, out int y, out int z))
            {
                PlaceHeldItemOrPlaceholder(playerBox, x, y, z);
            }
        }

        // ─── m11 W3-1：手持物品 → 对应方块（替换 m3 恒放 placeBlockId 的占位） ──────

        /// <summary>
        /// 放置路由（m11 W3-1，集成点③发现的缺口：此前家具/箱子/床/门/附魔台全放不进世界）。
        /// 手持物品 <see cref="ItemDefinition.BlockId"/> 非空 → 放对应方块并扣 1 个物品：
        /// 床走 <see cref="BedSystem.PlaceBed"/> 双格摆法、门贴地两格竖放、其余
        /// （9 家具 / 箱子 / 附魔台）单格直放。BlockId 为空（含空手 / 无关物品）→
        /// 照旧放 <see cref="placeBlockId"/> 且<b>不扣物品</b>——m3 占位语义原样保持
        /// （BlockInteractionUseRoutingTests.UseAt_EmptyHandOnGrass_PlacesAsBefore 钉着）。
        /// 放置成功才扣 1：挖掉这些方块按 block_drops 1:1 掉回物品，不扣就是无限复制机。
        /// </summary>
        private void PlaceHeldItemOrPlaceholder(Aabb playerBox, int x, int y, int z)
        {
            var ctx = PlayerContext.Instance;
            var def = ctx == null ? null : ctx.GetSelectedDefinition();
            string blockId = def?.BlockId;
            if (string.IsNullOrEmpty(blockId))
            {
                // 占位兼容路径（m3 起）：无 blockId 物品照旧恒放 placeBlockId
                _world.SetBlock(x, y, z, placeBlockId);
                _views?.MarkBlockChanged(x, y, z);
                _audio?.PlayPlace();
                return;
            }

            // 悬空 blockId（数据表笔误）：不放也不扣——真数据守卫测试会把住这一关
            if (_registry == null || !_registry.TryGetById(blockId, out BlockDefinition block))
            {
                Debug.LogWarning(
                    $"[BlockInteraction] 物品 {def.Id} 的 blockId「{blockId}」未在方块注册表注册，本次右键不放任何方块。");
                return;
            }

            if (block.NumericId == BlockIds.Bed)
            {
                PlaceBedTwoCells(playerBox, x, y, z);
                return;
            }

            if (block.NumericId == BlockIds.WoodenDoor || block.NumericId == IronDoorBlockId)
            {
                PlaceDoorTwoCells(playerBox, x, y, z, block.NumericId);
                return;
            }

            _world.SetBlock(x, y, z, block.NumericId);
            _views?.MarkBlockChanged(x, y, z);
            _audio?.PlayPlace();
            ConsumeOneHeldItem();
        }

        /// <summary>
        /// 床：脚格 = 解算出的放置格，头格 = 脚格沿玩家视向偏移一格（<see cref="ResolveBedFacing"/>，
        /// 头朝玩家看的方向，与 MC 同款摆法）。双格落块走 <see cref="BedSystem.PlaceBed"/>
        /// 既有 API：头格或脚格非空气<b>整体拒绝</b>（不留半张床）；头格额外过一遍
        /// <see cref="BlockPlacement.IntersectsPlayer"/> 防卡身——脚格由 TryResolve 把过关，
        /// 头格是本路径新引入的占据格，同一份判定不能只查一半。BedSystem 未就绪
        /// （数据表缺失降级）时 warn 后不放（右键被消费，不回落占位——占位放的是石头，
        /// 对着床物品放石头比什么都不放更错）。
        /// </summary>
        private void PlaceBedTwoCells(Aabb playerBox, int x, int y, int z)
        {
            var beds = ResolveBeds();
            if (beds == null)
            {
                Debug.LogWarning("[BlockInteraction] BedSystem 未就绪，床放不了（本次右键不放任何方块）。");
                return;
            }

            BedFacing facing = ResolveBedFacing();
            (int hx, _, int hz) = BedSystem.HeadOffset(facing);
            if (BlockPlacement.IntersectsPlayer(x + hx, y, z + hz, playerBox))
            {
                return; // 头格会卡住玩家：整体不放（床是实心方块，放进去人被封在床里）
            }

            if (!beds.PlaceBed(_world, x, y, z, facing))
            {
                return; // 头/脚格被占：整体拒绝（PlaceBed 内部两格原子性，不留半张床）
            }

            _views?.MarkBlockChanged(x, y, z);
            _views?.MarkBlockChanged(x + hx, y, z + hz);
            _audio?.PlayPlace();
            ConsumeOneHeldItem();
        }

        /// <summary>
        /// 门：贴地两格竖放——下格 = 解算出的放置格，上格 = 其正上方（MC 门是两格高）。
        /// <para>
        /// <b>双格取舍（m11 W3-1）</b>：仓库没有 <c>wooden_door_upper</c> 方块 id，
        /// <see cref="World.SetBlock"/> 又只存 16 位方块 id、没有逐格 metadata 位可放
        /// 「上半扇」标记——因此上下两格放<b>同一个门方块 id</b>（纹理差异靠该方块自身
        /// top/bottom/side 分面，两格造型一致是可接受的简化）。这与 RedstoneSystem 的
        /// 门语义天然对齐：它把每个门格独立注册切换（Doors.Register 逐格），同 id 双格
        /// 不引入新特例。要真正区分上下半扇需新增 upper 方块 id 或给 ChunkSection 加
        /// metadata 面，留给后续里程碑。
        /// </para>
        /// 上格非空气 / 超出世界高度 / 会卡住玩家 → <b>整体拒绝</b>（不留半扇门、不扣物品）。
        /// </summary>
        private void PlaceDoorTwoCells(Aabb playerBox, int x, int y, int z, ushort doorId)
        {
            if (y + 1 >= VoxelCoords.MaxY) return;
            if (_world.GetBlock(x, y + 1, z) != BlockIds.Air) return; // 上格被占（天花板抵头）
            if (BlockPlacement.IntersectsPlayer(x, y + 1, z, playerBox)) return; // 上格卡身

            _world.SetBlock(x, y, z, doorId);
            _world.SetBlock(x, y + 1, z, doorId);
            _views?.MarkBlockChanged(x, y, z);
            _views?.MarkBlockChanged(x, y + 1, z);
            _audio?.PlayPlace();
            ConsumeOneHeldItem();
        }

        /// <summary>
        /// 床头脚取向：取玩家视向的<b>水平主轴</b>（|X| 与 |Z| 大者；相等或垂直俯仰时归
        /// Z 轴），头格朝玩家看的方向。<see cref="BedFacing"/> 的 North=+Z / East=+X 轴约定
        /// 与 MobModels「面朝 +Z」同源。没有视向信息（EditMode 无相机 / 装配失误）
        /// 默认朝北——放得出床比取向完美更重要，取向仅影响头脚哪端朝哪。
        /// </summary>
        private BedFacing ResolveBedFacing()
        {
            if (_player == null || _player.Eye == null) return BedFacing.North;
            Float3 forward = ToFloat3(_player.Eye.forward);
            if (System.Math.Abs(forward.X) > System.Math.Abs(forward.Z))
            {
                return forward.X >= 0f ? BedFacing.East : BedFacing.West;
            }
            return forward.Z >= 0f ? BedFacing.North : BedFacing.South;
        }

        /// <summary>放置成功扣选中槽 1 个物品（blockId 物品路径专用；占位路径不扣）。
        /// 与「挖掉这些方块 1:1 掉回物品」（block_drops）对称——放时扣 1、挖时还 1。</summary>
        private void ConsumeOneHeldItem()
        {
            var ctx = PlayerContext.Instance;
            ctx?.Inventory?.TryRemoveOne(ctx.Inventory.SelectedHotbarIndex);
        }

        /// <summary>铁门方块 numericId（与 blocks/iron_door.json 及 RedstoneSystem.IronDoorId 手动一致）。</summary>
        private const ushort IronDoorBlockId = 1005;

        // ─── m11 W3-3：机元图腾 → 召唤机元守卫 Boss ──────────────────────────

        /// <summary>「机元守卫苏醒了！」（召唤成功提示）。public const 给 EditMode 测试锁文案（OnGUI 本身 EditMode 不跑）。</summary>
        public const string BossSummonedHintText = "机元守卫苏醒了！";

        /// <summary>「图腾已沉寂」（已用图腾再右键的提示）。同上。</summary>
        public const string BossTotemUsedHintText = "图腾已沉寂";

        /// <summary>
        /// m11 W3-3：机元守卫召唤路由——右键命中的机元矿石属于一个完整 2×2 图腾
        /// （<see cref="MachineGuardianSummon.TryDetectTotem"/>：四块相邻同层 + y&lt;16）
        /// 时，在图腾中心上方一格生成 Boss（<see cref="MobManager.SpawnMobAt"/>）。
        /// 消耗无——矿石留着，挖掉任一块即自然拆除图腾；防重复靠
        /// <see cref="BossSummonState.Default"/> 的已用登记（键 = 2×2 最小角，
        /// 进 <see cref="MyWorld.Core.Persistence.LevelData.UsedBossTotems"/> 存档往返），
        /// 同一图腾只出一只 Boss，再右键给「图腾已沉寂」提示。
        /// 不是完整图腾（单块/缺角/y 过浅）→ false 落到后续路由（锄地/放方块等）；
        /// 场景没有 MobManager（纯逻辑测试宿主）→ 同样 false 整条让位。
        /// </summary>
        private bool TrySummonMachineGuardian(VoxelRayHit hit, ushort target)
        {
            if (target != BlockIds.MachineEssenceOre) return false;
            if (!MachineGuardianSummon.TryDetectTotem(_world, hit.X, hit.Y, hit.Z,
                    out int anchorX, out int anchorY, out int anchorZ, out Float3 spawnPos))
            {
                return false; // 不是完整图腾：不是本路由的菜，照常落到后续路由
            }

            var mgr = FindObjectOfType<MobManager>();
            if (mgr == null) return false; // 没生物系统的宿主（EditMode 纯逻辑场景）整条让位

            string key = MachineGuardianSummon.TotemKey(anchorX, anchorY, anchorZ);
            if (BossSummonState.Default.IsUsed(key))
            {
                ShowInteractionHint(BossTotemUsedHintText);
                return true; // 已用图腾：消费右键给提示，不再召唤也不放方块
            }

            Mob boss = mgr.SpawnMobAt(MobKind.MachineGuardian, spawnPos);
            BossSummonState.Default.MarkUsed(key);
            _audio?.PlayPlace();
            ShowInteractionHint(BossSummonedHintText);
            return true;
        }

        /// <summary>
        /// m11 ②：手持锄（<c>hoe_*</c> 系列）+ 准星是草/泥土 → 锄成干耕地。
        /// 成功后与挖掘共用同一条磨损通道（<see cref="ApplyDigDurability"/>：锄 -1 耐久、
        /// 耐久尽同样碎裂）。系统未接好 / 手持不是锄 / 目标不可锄 → false 落到下一条路由。
        /// </summary>
        private bool TryTillWithHoe(VoxelRayHit hit, ushort target)
        {
            var farm = ResolveFarm();
            if (farm == null || !FarmSystem.CanTill(target)) return false;

            var ctx = PlayerContext.Instance;
            var def = ctx == null ? null : ctx.GetSelectedDefinition();
            if (def == null || !def.Id.StartsWith("hoe_", System.StringComparison.Ordinal)) return false;

            if (!farm.Till(_world, hit.X, hit.Y, hit.Z)) return false;
            _views?.MarkBlockChanged(hit.X, hit.Y, hit.Z);
            _audio?.PlayHoeTill();  // av W3-13：锄地音
            ApplyDigDurability(hit.X, hit.Y, hit.Z);
            // m11 W2-4：锄地成功 → TillSoil 任务事件（chapter2 ch2_02「锄地播种」前半）
            MyWorld.Unity.Gameplay.QuestEventBus.Instance?.Raise(
                new MyWorld.Core.Quests.QuestEvent { Type = MyWorld.Core.Quests.QuestEventType.TillSoil });
            return true;
        }

        /// <summary>
        /// m11 ②：手持种子（seeds_wheat/beet/mung）+ 准星是耕地 → 在耕地正上方播 stage0 作物，
        /// 扣选中槽 1 粒种子。播种失败（上方被占）返回 false 落到下一条路由——
        /// 那个格子本来也放不进东西，行为一致。
        /// </summary>
        private bool TryPlantSeeds(VoxelRayHit hit, ushort target)
        {
            var farm = ResolveFarm();
            if (farm == null) return false;
            if (target != FarmSystem.FarmlandId && target != FarmSystem.FarmlandWetId) return false;

            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return false;
            var def = ctx.GetSelectedDefinition();
            if (def == null || !FarmSystem.TryResolveSeed(def.Id, out _)) return false;

            if (!farm.TryPlant(_world, hit.X, hit.Y + 1, hit.Z, def.Id)) return false;
            ctx.Inventory.TryRemoveOne(ctx.Inventory.SelectedHotbarIndex);
            _views?.MarkBlockChanged(hit.X, hit.Y + 1, hit.Z);
            _audio?.PlayPlant();  // av W3-13：播种音
            // m11 W2-4：播种成功 → SowSeed 任务事件（ItemId=种子 numericId，chapter2
            // ch2_02 的条件限定 itemId 1019=seeds_wheat，按种子种类记数）
            MyWorld.Unity.Gameplay.QuestEventBus.Instance?.Raise(
                new MyWorld.Core.Quests.QuestEvent
                {
                    Type = MyWorld.Core.Quests.QuestEventType.SowSeed,
                    ItemId = def.NumericId,
                    Count = 1,
                });
            return true;
        }

        /// <summary>
        /// m11 ②：手持骨粉 + 准星是作物 → 催熟一级（<see cref="FarmSystem.ApplyBoneMeal"/>，
        /// 催熟成功才扣 1 份骨粉；成熟了催不动，右键仍被消费——不能往自家作物上放方块）。
        /// 准星是树苗 → 消费右键、成长交给 <see cref="MyWorld.Unity.Environment.SaplingGrowth"/>
        /// 的既有「右键即长」通道（同帧独立射线已处理，不扣骨粉——那条通道不感知物品）。
        /// </summary>
        private bool TryApplyBoneMeal(VoxelRayHit hit, ushort target)
        {
            var ctx = PlayerContext.Instance;
            var def = ctx == null ? null : ctx.GetSelectedDefinition();
            if (def == null || def.Id != "bone_meal") return false;

            if (target == TreeFeature.SaplingId) return true; // 只挡放置

            var farm = ResolveFarm();
            if (farm == null || !FarmSystem.TryParseStageBlock(target, out _, out _)) return false;

            if (farm.ApplyBoneMeal(_world, hit.X, hit.Y, hit.Z))
            {
                if (ctx.Inventory != null)
                {
                    ctx.Inventory.TryRemoveOne(ctx.Inventory.SelectedHotbarIndex);
                }
                _views?.MarkBlockChanged(hit.X, hit.Y, hit.Z);
                _audio?.PlayPlace();
            }
            return true;
        }

        /// <summary>
        /// m11 ②：准星是床 → <see cref="BedSystem.Sleep"/>。夜间睡成（时间跳早晨 0 tick +
        /// 重生点设到该床）；白天给「只能在夜里睡觉」提示性 no-op（同样消费右键）；
        /// 床被拆（BedMissing）返回 false 落到放方块。系统未接好不消费。
        /// </summary>
        private bool TrySleepInBed(VoxelRayHit hit, ushort target)
        {
            var beds = ResolveBeds();
            if (target != BlockIds.Bed || beds == null) return false;

            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Time == null) return false;

            switch (beds.Sleep(_world, hit.X, hit.Y, hit.Z, ctx.Time))
            {
                case SleepResult.Slept:
                    return true;
                case SleepResult.NotNight:
                    ShowInteractionHint(BedNotNightHintText);
                    return true;
                default:
                    return false; // BedMissing：那格已经不是床
            }
        }

        /// <summary>
        /// m7 A3：尝试吃掉选中槽的 1 个食物。判定链：PlayerContext / Inventory / HungerSystem
        /// 就绪 → 选中物品 <see cref="ItemDefinition.IsEdible"/>。
        /// 吃成功返回 true（右键被消费）；任一条件不满足返回 false，右键落到放方块分支。
        /// </summary>
        private bool TryEatSelectedFood()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null || ctx.HungerSystem == null) return false;

            var def = ctx.GetSelectedDefinition();
            if (def == null || !def.IsEdible) return false;

            ctx.HungerSystem.Eat((int)def.HealAmount.Value);
            ctx.Inventory.TryRemoveOne(ctx.Inventory.SelectedHotbarIndex);
            // av W3-13：吃成功播 eat 音
            _audio?.PlayEat();
            return true;
        }

        // ─── m11 W2-2：附魔书融合（手持 enchanted_book 右键 → 书消失、装备带魔） ──

        /// <summary>附魔书物品 id（与 items/enchanted_book.json 一致；判定走字符串 id，同弓分支）。</summary>
        private const string EnchantedBookItemId = "enchanted_book";

        /// <summary>
        /// m11 W2-2：EditMode fixture 直注附魔存储（优先于 <see cref="EnchantStore.Default"/>）。
        /// 运行时不需要调——效果/存档接线与融合路由共用 Default 同一份。
        /// 传 null 打 warning 方便发现装配遗漏（回落 Default 不影响功能）。
        /// </summary>
        public void SetEnchantStore(EnchantStore store)
        {
            if (store == null)
            {
                Debug.LogWarning("[BlockInteraction] SetEnchantStore 传入了 null，融合将落到全局 Default 实例。");
            }
            _enchants = store;
        }

        /// <summary>附魔存储双源解析：EditMode 直注优先，否则全局 <see cref="EnchantStore.Default"/>。</summary>
        private EnchantStore ResolveEnchants() => _enchants ?? EnchantStore.Default;

        /// <summary>
        /// m11 W2-2：手持附魔书（enchanted_book）右键 → <see cref="EnchantSystem.Fuse"/>
        /// 把书融进背包里第一件可附魔装备：书消失（maxStack=1 整格清空）、装备带魔——
        /// 附魔写 <see cref="EnchantStore"/> 独立字典，装备栈 <see cref="ItemStack.Metadata"/>
        /// 一位不动（16 位全归耐久，a1fb425 的教训）。融合<b>不扣经验</b>
        /// （书的成本已在配方：书 + 青金石）。背包没有可附魔装备时给一次性提示且右键仍被消费
        /// （不能顺势放方块）；手持的不是书返回 false，落到弓/锄等后续路由。
        /// salt 用 <c>Time.frameCount</c>：运行时每次右键必不同帧，
        /// 未鉴定书的类型掷骰既均匀又可复现（Core 确定性哈希）。
        /// </summary>
        private bool TryFuseEnchantedBook()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null || ctx.Items == null) return false;

            var def = ctx.GetSelectedDefinition();
            if (def == null || def.Id != EnchantedBookItemId) return false;

            var result = EnchantSystem.Fuse(
                ResolveEnchants(), ctx.Inventory, ctx.Items,
                ctx.Inventory.SelectedHotbarIndex, Time.frameCount,
                out _, out _, out _);

            if (result == FuseResult.Ok)
            {
                // m11 W2-4：融合成功 = 完成一次附魔 → EnchantItem 任务事件
                //（chapter2 ch2_06 desc 的实机路径就是「书+青金石合书 → 右键融合」；
                //  Core 的 EnchantSystem.Enchant 供未来附魔台 UI，当前无实机调用点）
                MyWorld.Unity.Gameplay.QuestEventBus.Instance?.Raise(
                    new MyWorld.Core.Quests.QuestEvent
                    {
                        Type = MyWorld.Core.Quests.QuestEventType.EnchantItem,
                    });
            }
            else if (result == FuseResult.NoEnchantableGear)
            {
                ShowInteractionHint(FuseNoGearHintText);
            }
            return true; // 手持书时右键一律被消费（成功融合或提示），不放方块
        }

        private static Float3 ToFloat3(Vector3 v) => new Float3(v.x, v.y, v.z);

        // ─── m11 ②：弓（手持 bow 右键按住蓄力，松开发射 ProjectileEntity） ─────

        /// <summary>弓的物品 id（与 items/bow.json 一致；物品判定走字符串 id）。</summary>
        private const string BowItemId = "bow";

        /// <summary>拉满耗时（秒）：0 → 1s 线性蓄力，1s 后封顶（任务卡「0-1s」）。</summary>
        public const float BowChargeSeconds = 1f;

        /// <summary>最小蓄力箭速（格/s）——刚扣就放的软箭。</summary>
        public const float BowMinArrowSpeed = 12f;

        /// <summary>拉满箭速（格/s）——高于骷髅出膛 <see cref="ProjectileEntity.DefaultSpeed"/>18，满蓄力占优。</summary>
        public const float BowMaxArrowSpeed = 24f;

        /// <summary>最小蓄力箭伤（点）。</summary>
        public const float BowMinDamage = 1f;

        /// <summary>拉满箭伤（点）：1-4 按拉满比例线性取值（任务卡数值）。</summary>
        public const float BowMaxDamage = 4f;

        private bool _bowCharging;
        private float _bowChargeStartTime;

        /// <summary>当前是否在拉弓。public 给 EditMode 路由测试断言（Update/Input 本身 EditMode 驱动不了）。</summary>
        public bool IsBowCharging => _bowCharging;

        private bool IsBowSelected()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return false;
            var def = ctx.GetSelectedDefinition();
            return def != null && def.Id == BowItemId;
        }

        /// <summary>手持弓且背包有 arrow → 开始蓄力；无箭不开弓（提示性 no-op，右键仍被消费）。</summary>
        private void TryStartBowCharge()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return;

            if (ctx.Inventory.CountOf(ProjectileEntity.ArrowItemId) <= 0)
            {
                ShowInteractionHint(BowNoArrowHintText);
                return;
            }

            _bowCharging = true;
            _bowChargeStartTime = Time.time;
        }

        /// <summary>当前蓄力比例 [0,1]（没在拉弓为 0）。蓄力条与放箭伤害共用同一个读数。</summary>
        private float BowChargeRatio01()
            => _bowCharging
                ? Mathf.Clamp01((Time.time - _bowChargeStartTime) / BowChargeSeconds)
                : 0f;

        /// <summary>每帧步进：拉弓途中换手持 / 死亡画面出现 → 作废；松开右键 → 放箭。</summary>
        private void TickBowCharge()
        {
            if (!_bowCharging) return;

            if (!IsBowSelected())
            {
                _bowCharging = false; // 拉弓途中切走（滚轮/背包）→ 不放箭
                return;
            }

            // 死亡画面出现时蓄力作废——死了不能放箭（m10 C2 fix1 的让位语义，Update 的
            // 指针门不覆盖死亡画面，这里自查）
            var gateCtx = PlayerContext.Instance;
            if (gateCtx != null && gateCtx.DeathScreen != null && gateCtx.DeathScreen.IsVisible)
            {
                _bowCharging = false;
                return;
            }

            if (Input.GetMouseButtonUp(1))
            {
                ReleaseBowCharge();
            }
        }

        /// <summary>
        /// 松键放箭：<see cref="TickBowCharge"/> 在 GetMouseButtonUp 时调用；EditMode 测试直接调
        /// （与 UseAt/BreakAt 同款「跳过 Input」驱动模式）。箭速/箭伤按拉满比例线性取值
        /// （12-24 格/s / 1-4 点），消耗背包 arrow ×1（<see cref="PlayerInventory.TryRemoveCount"/>，
        /// 不限 hotbar 槽）。发射走 <see cref="MobAI.OnProjectileFired"/> 同一事件——
        /// 宿主（MobManager 的投射物接线，A 批）订阅后统一接管 tick/视觉/mob 命中；
        /// <c>ownerEntityId=0</c> 表示玩家箭：Core 侧不判玩家命中（不自伤），mob 命中由宿主结算，
        /// 伤害读 <see cref="ProjectileEntity.Damage"/>。
        /// </summary>
        public void ReleaseBowCharge()
        {
            if (!_bowCharging) return;
            float ratio = BowChargeRatio01();
            _bowCharging = false;

            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return;
            if (!IsBowSelected() || _player == null || _player.Eye == null) return;

            // 发射前再核一次箭：拉弓途中可能被拖走/用掉
            if (!ctx.Inventory.TryRemoveCount(ProjectileEntity.ArrowItemId, 1)) return;

            Float3 origin = ToFloat3(_player.Eye.position);
            Float3 direction = ToFloat3(_player.Eye.forward);
            var arrow = new ProjectileEntity(
                origin,
                direction * Mathf.Lerp(BowMinArrowSpeed, BowMaxArrowSpeed, ratio),
                ownerEntityId: 0)
            {
                Damage = Mathf.Lerp(BowMinDamage, BowMaxDamage, ratio),
            };
            MobAI.OnProjectileFired?.Invoke(arrow);
        }

        // ─── m13 W3：火枪（手持 musket 右键开火 + 装填 1.5s + 无弹咔哒） ─────

        /// <summary>火枪物品 id（与 items/musket.json 一致；物品判定走字符串 id）。</summary>
        private const string MusketItemId = "musket";

        /// <summary>子弹物品 id（与 items/bullet.json 一致；消耗时按 numericId 扣背包）。</summary>
        public const int MusketBulletItemId = 1608;

        /// <summary>装填耗时（秒）：开火后 1.5s 内右键被拒（"咔哒"提示）。</summary>
        public const float MusketReloadSeconds = 1.5f;

        /// <summary>出膛初速（格/s）。直弹道（无重力）按此速度向准星方向直线前进 25m。</summary>
        public const float MusketBulletSpeed = 32f;

        /// <summary>火枪伤害（点）。卡片数值：6（来自 design spec + 现行肉搏剑的 4-7 区间）。</summary>
        public const float MusketBulletDamage = 6f;

        /// <summary>开火完成时刻（Time.time）。<c>Time.time &lt; _musketReloadUntil</c> 时右键被拒。
        /// 默认 <see cref="float.NegativeInfinity"/>（已可开火）。</summary>
        private float _musketReloadUntil = float.NegativeInfinity;

        /// <summary>当前是否在装填中。public 给 EditMode 路由测试断言（Update/Input EditMode 驱动不了）。</summary>
        public bool IsMusketReloading => Time.time < _musketReloadUntil;

        private bool IsMusketSelected()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return false;
            var def = ctx.GetSelectedDefinition();
            return def != null && def.Id == MusketItemId;
        }

        /// <summary>
        /// m13 W3：手持火枪右键消费——分支三条：
        /// <list type="number">
        /// <item>装填中（<c>Time.time &lt; _musketReloadUntil</c>）→ 静默拒绝（右键被消费、不开火不放方块）</item>
        /// <item>无弹（背包 <see cref="MusketBulletItemId"/> = 0）→ 播咔哒 + 右键被消费、不开火</item>
        /// <item>可开火 → 扣 1 颗子弹 + 直射 ProjectileEntity（<see cref="ProjectileEntity.IsStraightLine"/>
        ///   = true + <see cref="ProjectileEntity.Range"/> = 25）+ 播放 fire 音 + 锁定装填窗
        ///   <c>_musketReloadUntil = Time.time + 1.5s</c></item>
        /// </list>
        /// <para>
        /// 与弓分支对齐：EditMode 测试不依赖 <c>Input.GetMouseButtonDown</c>，外部测试直调
        /// 本方法。玩家宿主 / 视野（<c>_player.Eye</c>）任一为 null 时静默 return——同弓
        /// 兜底（避免空引用崩在 <see cref="MobAI.OnProjectileFired"/> 之前）。
        /// </para>
        /// </summary>
        public void TryFireMusket()
        {
            // 装填窗：上次开火后 1.5s 内的右键一律拒绝。不播咔哒（与 MC 同：装填中拉不动扳机，
            // 不是「没弹」那种主动反馈）——咔哒是「想开但开不了」的语义，留给无弹路径。
            if (Time.time < _musketReloadUntil) return;

            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return;
            if (!IsMusketSelected() || _player == null || _player.Eye == null) return;

            // 无弹：拒绝 + 咔哒。右键仍被消费（不放过方块/吃食物——视觉上是一次开火动作）。
            if (ctx.Inventory.CountOf(MusketBulletItemId) <= 0)
            {
                _audio?.PlayClick();
                return;
            }

            // 发射前扣弹：同弓「拉弓途中可能被拖走/用掉」的二段守卫——
            // 第一次右键到 TryFireMusket 调用期间不会用弹，但保持一致语义（万一后续接入
            // 别的快速触发路径——双击/连发，不要让扣弹/开火之间有缝）。
            if (!ctx.Inventory.TryRemoveCount(MusketBulletItemId, 1))
            {
                _audio?.PlayClick();
                return;
            }

            Float3 origin = ToFloat3(_player.Eye.position);
            Float3 direction = ToFloat3(_player.Eye.forward);
            var bullet = new ProjectileEntity(origin, direction * MusketBulletSpeed, ownerEntityId: 0)
            {
                Damage = MusketBulletDamage,
                IsStraightLine = true,   // m13 W3：直射无重力
                Range = 25f,             // m13 W3：火枪射程 25m
            };
            MobAI.OnProjectileFired?.Invoke(bullet);
            _audio?.PlayFire();
            _musketReloadUntil = Time.time + MusketReloadSeconds;
        }

        // ─── m11 ②：右键交互的一次性文字提示（床白天 / 无箭） ─────────────────

        /// <summary>「只能在夜里睡觉」。public const 给 EditMode 测试锁文案（OnGUI 本身 EditMode 不跑）。</summary>
        public const string BedNotNightHintText = "只能在夜里睡觉";

        /// <summary>「没有箭了」。同上。</summary>
        public const string BowNoArrowHintText = "没有箭了";

        /// <summary>「没有可附魔的装备」（m11 W2-2 手持附魔书但背包无装备）。同上锁文案。</summary>
        public const string FuseNoGearHintText = "没有可附魔的装备";

        /// <summary>交互提示显示时长（秒），与门槛提示同款「不叠不刷」语义。</summary>
        private const float InteractionHintDuration = 2f;

        /// <summary>交互提示画在镐坏提示上方 30px（第三行，与另两条提示不叠字）。</summary>
        private const float InteractionHintBottomOffset = ToolBreakHintBottomOffset + 30f;

        /// <summary>交互提示累计触发次数。public 给 EditMode 测试断言。</summary>
        public int InteractionHintCount { get; private set; }

        private float _interactionHintUntil = float.NegativeInfinity;
        private string _interactionHintText;

        /// <summary>记录一次交互提示。显示窗口（2s）内重复触发不叠加不重置，窗口过后重新计。</summary>
        private void ShowInteractionHint(string text)
        {
            if (Time.time < _interactionHintUntil) return;
            InteractionHintCount++;
            _interactionHintText = text;
            _interactionHintUntil = Time.time + InteractionHintDuration;
        }

        /// <summary>
        /// X2 fix-up：在指定坐标挖方块。流程：<see cref="World.SetBlock"/> → 标脏 → 播放 break 音效 →
        /// 按 <see cref="BlockDrops"/> 查询该方块的掉落物条目，每条实例化为
        /// <see cref="MyWorld.Core.Items.ItemDropEntity"/> 并加入 <see cref="PlayerContext.ItemDrops"/>。
        /// <para>
        /// 暴露为 public 是为了让 EditMode 测试不依赖 <c>Input.GetMouseButtonDown</c>；
        /// <c>Update</c> 与外部测试都走同一条路径。
        /// </para>
        /// <para>
        /// m10 A3 工具门槛：选中物品的镐等级（<see cref="BlockGating.ResolveToolTier"/>）
        /// &lt; 方块 <see cref="BlockDefinition.MinToolTier"/> 时，方块<b>照样挖掉</b>（含标脏/音效），
        /// 但<b>不走 BlockDrops</b>（无掉落），并触发一次「需要更好的镐」提示——
        /// 孩子的「木镐挖铁 40 小时」按 MC 制修正为门槛而非时长（spec §需求评估存档 §130）。
        /// </para>
        /// <para>
        /// m10 B1 镐耐久：方块成功挖掉（含门槛不够的「白挖」）→ 选中镐耐久 -1
        /// （<see cref="ApplyDigDurability"/>）；耐久尽 → 镐从选中槽消失 + 一次性
        /// 「镐碎了！」提示 + 碎块散落（m10 B2：4-6 块，落地扎脚 0.5 伤，2s 消失）。
        /// </para>
        /// <para>
        /// m11 ②：目标方块是<b>成熟作物</b>（*_stage2）时改走 <see cref="FarmSystem.Harvest"/>——
        /// 掉落取其确定性掷骰结果（产物 1-2 + 种子）而非 <see cref="BlockDrops"/>，
        /// 且不磨损工具（收获不是挖掘）。未注入农田系统时退回普通挖掘路径。
        /// </para>
        /// <para>
        /// 顺序与既有 <c>Update</c> 行为对齐：先清方块 → 标脏（让玩家视觉立刻看到破坏）→ 播音效 →
        /// spawn 掉落。无 PlayerContext / 无 BlockDrops 表 / 挖空气 / drops 表里没条目均 no-op。
        /// </para>
        /// <para>
        /// m6 C2 决策：这里**不发** ObtainItem 任务事件——掉落物还没进背包，
        /// 真正进包（<see cref="PlayerController.PickupNearbyDrops"/>）的那一刻才发，
        /// 挖矿场景由拾取路径覆盖且不会双计（QuestEventBusTests 有断言守着）。
        /// </para>
        /// </summary>
        public void BreakAt(int x, int y, int z)
        {
            if (_world == null) return;
            ushort before = _world.GetBlock(x, y, z);
            if (before == BlockIds.Air) return; // 挖空气是 no-op（与 review-final B7 不冲突）

            // m11 ②：成熟作物（*_stage2）左键 = 收获。掉落走 FarmSystem.Harvest 的
            // 确定性掷骰（产物 1-2 + 种子），不走 BlockDrops、不磨损工具（收获不是挖掘）；
            // 未成熟作物照常走下方挖掘路径（block_drops 无条目 → 无掉落，挖了就没）。
            var harvestFarm = ResolveFarm();
            if (harvestFarm != null
                && FarmSystem.TryParseStageBlock(before, out _, out int cropStage)
                && cropStage == FarmSystem.MatureStage)
            {
                ItemStack[] harvest = harvestFarm.Harvest(_world, x, y, z); // 内部已清方块 + 清作物状态
                _views?.MarkBlockChanged(x, y, z);
                _audio?.PlayHarvest();  // av W3-13：成熟作物 → 收获音
                BlockBroken?.Invoke(x, y, z, before); // m11 W3-4：碎屑粒子纯视觉挂载点（收获也是一次移除方块）
                SpawnItemDrops(harvest, x, y, z);
                // m11 W2-4：收获一株成熟作物 → HarvestCrop 任务事件（chapter2 ch2_03
                // 「第一茬收获」；不区分作物种类，Count 按株记）
                MyWorld.Unity.Gameplay.QuestEventBus.Instance?.Raise(
                    new MyWorld.Core.Quests.QuestEvent
                    {
                        Type = MyWorld.Core.Quests.QuestEventType.HarvestCrop,
                        Count = 1,
                    });
                return;
            }

            // m10 A3：先判门槛再动方块。注册表缺失/方块未注册视同门槛 0（保持旧行为）
            bool tierOk = !IsTierGated(before);

            _world.SetBlock(x, y, z, BlockIds.Air);
            _views?.MarkBlockChanged(x, y, z);
            _audio?.PlayBreak();  // 普通方块挖掘 → 破方块音
            BlockBroken?.Invoke(x, y, z, before); // m11 W3-4：碎屑粒子纯视觉挂载点（门槛不够的白挖也发——方块视觉上确实没了）

            // m10 B1：挖掉即磨损（白挖也算——工具挥出去了就是用了，与 MC 一致）
            ApplyDigDurability(x, y, z);

            if (!tierOk)
            {
                ShowToolTierHint();
                return; // 门槛不够：挖得掉但白挖，不走 BlockDrops
            }

            // X2 fix-up：spawn ItemDropEntity。BlockDrops 可能未注入（旧场景 / EditMode
            // 单元测），缺了就 silently no-op，不破坏既有"挖 = 立即空一块"的视觉反馈。
            // m11 W2-4 E1：salt 传坐标——tall_grass 的 30% 麦种条目每次挖掘独立掷骰
            if (_blockDrops == null) return;
            SpawnItemDrops(_blockDrops.DropsFor(before, x * 31 + y * 7 + z), x, y, z);
        }

        /// <summary>
        /// 把一批掉落物实例化为 <see cref="ItemDropEntity"/> 并加入
        /// <see cref="PlayerContext.ItemDrops"/>（X2 fix-up 从 <see cref="BreakAt"/> 抽出，
        /// m11 ②起收获路径共用）。空数组 / 无 PlayerContext 均 no-op（后者打 warning 定位装配失误）。
        /// </summary>
        private void SpawnItemDrops(ItemStack[] drops, int x, int y, int z)
        {
            if (drops == null || drops.Length == 0) return;

            var ctx = PlayerContext.Instance;
            if (ctx == null)
            {
                // 玩家视觉看到方块消失却没掉任何东西，没 log 也找不到原因——
                // 加 warning 让「场景里没挂 PlayerContext」这种装配失误更容易定位。
                Debug.LogWarning("[BlockInteraction] 方块掉落需要 PlayerContext，但当前为 null");
                return;
            }

            // 中心 = (x+0.5, y+0.5, z+0.5)，让 1.5m 拾取半径对准方块中心。
            Float3 center = new Float3(x + 0.5f, y + 0.5f, z + 0.5f);
            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i].IsEmpty) continue;
                var drop = new ItemDropEntity(drops[i], center);
                drop.SpawnTime = Time.time; // F1 follow-up：spawn 时刻记录，TryPickupBy 据此判定 0.5s grace
                ctx.ItemDrops.Add(drop);
            }
        }

        /// <summary>
        /// 按 Biome 调整方块的挖掘耗时（秒）。
        /// 山地石头硬 ×2，沙漠沙软 ×0.5，其它默认 1f。
        /// <para>
        /// 静态纯函数：EditMode 测试可直接调用验证契约，
        /// 与 MonoBehaviour 实例化 / <see cref="World"/> / <see cref="PlayerController"/>
        /// 等上下文完全解耦。当前 <c>Update</c> 里仍是瞬时挖矿——以后接真实 MiningTimed
        /// 逻辑时这个返回值就是「按住 LMB 的目标持续时间」。
        /// </para>
        /// <para>
        /// m10 A3 起它委托给三参重载并<b>视为持达标镐</b>（<see cref="QualifiedToolTier"/>）：
        /// 旧签名没有工具维度，语义即「假设拿得动这方块」，既有 m3 C7 契约（石头平原 1s /
        /// 山地 2s、沙漠沙 0.5s、默认 1s）逐值不变。
        /// </para>
        /// </summary>
        public static float BreakTime(int blockId, Biome biome)
            => BreakTime(blockId, biome, QualifiedToolTier, float.NaN, 1f);

        /// <summary>
        /// m10 A3：加选中物品 toolTier 维度的挖掘耗时（秒）。规则分两层：
        /// <list type="bullet">
        /// <item><b>(block, toolTier) 查表</b>（<see cref="BlockGating.BreakSeconds"/>）：
        ///   达标 = 该方块基准秒（spec §1「挖掘时间（对应镐）」列），不达标 ×4
        ///   （镐帮不上忙按徒手档——徒手挖石 4s 即出自这里）</item>
        /// <item><b>群系倍率</b>（m3 C7 原样）：山地石头 ×2，沙漠沙 ×0.5，其它 ×1</item>
        /// </list>
        /// 基准秒/门槛与 <c>blocks/*.json</c> 的 hardness + minToolTier <b>全量逐块一致</b>
        /// （fix1：EditMode 一致性测试遍历注册表全部方块对表，单边改动立刻红），
        /// 这里硬编码是因为静态函数拿不到注册表。JSON hardness = 1 的方块走 default 分支不用进 switch；
        /// m11 ②起 default 分支采信调用方传入的 JSON hardness 作基准——第 1 波新方块
        /// （七新树种原木/树叶 2s、耕地 0.6 等，numericId 走自动分配不稳定）不进 switch 也能与 JSON 对表。
        /// 另有 m11 ②即挖分支：hardness 已知且落在 [0, <see cref="InstantBreakHardness"/>) 的
        /// 装饰方块（12 花草 + 9 作物）→ <see cref="InstantBreakSeconds"/>——语义是「秒挖」
        /// 而不是「1 秒」，比改 12 份 JSON 更贴约定（守卫测试的期望表生成逻辑同步对齐）。
        /// </summary>
        /// <param name="hardness">
        /// 调用方已知的 JSON hardness（守卫测试 / 未来接注册表的挖掘计时传它）。
        /// 缺省 NaN = 未知，行为与旧三参调用完全一致；负数（不可破坏）不参与即挖与基准回退。
        /// </param>
        /// <param name="digTimeMultiplier">
        /// m11 W2-2：效率附魔的挖掘时间乘数（<see cref="SelectedDigTimeMultiplier"/> 解算，
        /// 效率 L 级 = 1/1.2^L &lt; 1）。缺省 1 = 无附魔，既有调用/守卫测试零变化。
        /// </param>
        public static float BreakTime(int blockId, Biome biome, int toolTier,
            float hardness = float.NaN, float digTimeMultiplier = 1f)
        {
            // ① m11 ②即挖：hardness 已知且 ≈0 → 0.15s（NaN 与负数都不进本分支）
            if (hardness >= 0f && hardness < InstantBreakHardness)
            {
                return InstantBreakSeconds * BiomeMultiplier(blockId, biome);
            }

            float baseSeconds;
            int minToolTier = 0;
            switch (blockId)
            {
                case BlockIds.Stone:
                    baseSeconds = 1f; minToolTier = 1; break;    // 木镐 1s / 徒手 4s
                case BlockIds.RawIronOre:
                    baseSeconds = 2f; minToolTier = 2; break;    // 石镐 2s / 木镐 8s（不掉）
                case BlockIds.GoldOre:
                    baseSeconds = 3f; minToolTier = 3; break;    // 铁镐 3s
                case BlockIds.SummerAlloyOre:
                    baseSeconds = 4f; minToolTier = 3; break;    // 铁镐 4s
                case BlockIds.MachineEssenceOre:
                    baseSeconds = 6f; minToolTier = 4; break;    // 钻镐 6s / 铁镐 24s（不掉）
                case TreeFeature.LogId:
                case TreeFeature.LeavesId:
                case TreeFeature.SaplingId:
                case 1000: // planks（无 BlockIds 常量，与 planks.json 的 numericId 手动一致）
                case 1004: // crafting_table（同上）
                case 1005: // iron_door
                case 1006: // lever
                case 1007: // redstone_dust
                    baseSeconds = 2f; break;                     // 木质家族：徒手 2s
                case BlockIds.Snow:
                    baseSeconds = 0.4f; break;                   // 雪最软（fix1：以 JSON 为真源进表）
                default:
                    // ② m11 ②：无特例时以调用方给的 JSON hardness 为基准（>0 才采信；
                    //    NaN=没给 / 负数=不可破坏，均回落 m3 默认 1s——空气契约有旧测试钉着）
                    baseSeconds = hardness > 0f ? hardness : 1f; // 泥/草/沙/空气等：m3 默认 1s
                    break;
            }

            return BlockGating.BreakSeconds(baseSeconds, minToolTier, toolTier)
                   * BiomeMultiplier(blockId, biome)
                   * (digTimeMultiplier > 0f ? digTimeMultiplier : 1f);
        }

        /// <summary>
        /// m11 W2-2 C2：选中槽装备的效率附魔 → 挖掘时间乘数（<see cref="EnchantSystem.DigTimeMultiplier"/>）。
        /// 查 <see cref="EnchantStore"/>（EditMode 直注优先、运行时 <see cref="EnchantStore.Default"/>，
        /// 与融合路由同一份）；无附魔 / 无上下文返回 1（零变化直通）。
        /// 当前挖掘仍是瞬时破坏（无计时消费方），本乘数供未来 MiningTimed 接线与守卫测试。
        /// </summary>
        public float SelectedDigTimeMultiplier()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return 1f;
            int idx = ctx.Inventory.SelectedHotbarIndex;
            var stack = ctx.Inventory.GetSlot(idx);
            if (stack.IsEmpty) return 1f;
            return ResolveEnchants().TryGet(idx, stack.ItemId, out var kind, out int level)
                ? EnchantSystem.DigTimeMultiplier(kind, level)
                : 1f;
        }

        /// <summary>即挖判定的 hardness 阈值：[0, 本值) 视为秒挖方块（12 花草 + 9 作物）。</summary>
        public const float InstantBreakHardness = 0.05f;

        /// <summary>即挖方块的挖掘耗时（秒）——「秒挖」不是「1 秒」（m11 ②定值）。</summary>
        public const float InstantBreakSeconds = 0.15f;

        /// <summary>m3 C7 的群系倍率：山地石头 ×2，沙漠沙 ×0.5，其它 ×1。</summary>
        private static float BiomeMultiplier(int blockId, Biome biome)
        {
            if (blockId == BlockIds.Stone)
            {
                return biome == Biome.Mountains ? 2f : 1f;
            }

            if (blockId == BlockIds.Sand)
            {
                return biome == Biome.Desert ? 0.5f : 1f;
            }

            return 1f;
        }

        /// <summary>
        /// 「需要更好的镐」提示的显示时长（秒）。m10 A3：不叠不刷——显示期间再挖
        /// 不达标方块不重新计时，窗口过了才允许下一次提示。
        /// </summary>
        private const float ToolTierHintDuration = 2f;

        /// <summary>
        /// 门槛提示显示的位置：hotbar 顶到屏幕底 16px，提示再抬高 30px（hotbar 上方）。
        /// </summary>
        private const float ToolTierHintBottomOffset = 64f + 16f + 30f;

        /// <summary>
        /// 视为「持达标镐」的 toolTier（m10 A3）：高于全部门槛（最高钻石镐=4），
        /// 旧两参 <see cref="BreakTime"/> 用它委托给三参重载，永远走达标耗时分支。
        /// </summary>
        private const int QualifiedToolTier = 99;

        /// <summary>m10 A3：门槛提示累计触发次数。public 是给 EditMode 测试断言
        /// 「首次触发、显示窗口内不重复」用的（OnGUI 本身 EditMode 不跑）。</summary>
        public int ToolTierHintCount { get; private set; }

        private float _toolTierHintUntil = float.NegativeInfinity;
        private GUIStyle _toolTierHintStyle;

        /// <summary>
        /// m10 A3：门槛判定——挖 <paramref name="blockId"/> 前查选中镐等级是否达标。
        /// 无注册表 / 方块未注册 / 无 PlayerContext（EditMode 单元场景）一律视同门槛 0，
        /// 保持「没声明门槛的方块挖矿行为不变」。
        /// </summary>
        private bool IsTierGated(ushort blockId)
        {
            if (_registry == null || !_registry.TryGetByNumericId(blockId, out BlockDefinition def))
            {
                return false;
            }

            // 与 TryEatSelectedFood 同款防御：PlayerContext / Inventory 任一未就绪视同空手
            var ctx = PlayerContext.Instance;
            int toolTier = ctx == null || ctx.Inventory == null
                ? 0
                : BlockGating.ResolveToolTier(ctx.GetSelectedDefinition());
            return !BlockGating.CanDrop(def.MinToolTier, toolTier);
        }

        /// <summary>
        /// m10 A3：记录一次「需要更好的镐」提示。显示窗口（2s）内重复不达标不叠加不重置；
        /// 窗口过后再次不达标才重新提示——孩子连续乱挖不会满屏刷字。
        /// </summary>
        private void ShowToolTierHint()
        {
            if (Time.time < _toolTierHintUntil) return;
            ToolTierHintCount++;
            _toolTierHintUntil = Time.time + ToolTierHintDuration;
        }

        /// <summary>
        /// m10 B1：挖掉方块成功 → 选中镐耐久 -1。判定链与 TryEatSelectedFood 同款防御：
        /// PlayerContext / Inventory / 物品定义任一未就绪 no-op；只有物品表声明了
        /// <see cref="ItemDefinition.MaxDurability"/> 的物品（当前=六把镐）才磨损。
        /// 扣减走 <see cref="ItemStack.WithDurabilityUsed"/>：Metadata=0 的存量工具
        /// 视为满耐久，首次挖掘才落编码。耐久尽 → 选中槽清空 + 一次性「镐碎了！」提示
        /// + <see cref="PickaxeShard.SpawnScatter"/> 碎块散落（m10 B2，孩子的原创机制：
        /// 4-6 块、落地扎脚 0.5 伤、2s 消失）。坐标参数参与碎块数掷点的哈希。
        /// </summary>
        private void ApplyDigDurability(int x, int y, int z)
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return;

            var def = ctx.GetSelectedDefinition();
            if (def == null || def.MaxDurability <= 0) return;

            int idx = ctx.Inventory.SelectedHotbarIndex;
            var stack = ctx.Inventory.GetSlot(idx);
            // m11 W2-2：耐久附魔按比例减缓磨损——每次消耗以 5/(5+L) 概率真正扣 1 点
            //（非耐久附魔恒 true 直通，行为零变化）。salt 用坐标哈希（同一次挥镐不同
            // 方块不同结果，同方块重放可复现），与碎块掷点同款确定性纪律
            if (ResolveEnchants().TryGet(idx, stack.ItemId, out var ench, out int enchLevel)
                && !EnchantSystem.ShouldWearDurability(ench, enchLevel, x * 31 + y * 7 + z))
            {
                return; // 这次免磨损（上限不动，期望耐用 ×(1+0.2L)）
            }
            var after = stack.WithDurabilityUsed(def.MaxDurability);
            ctx.Inventory.SetSlot(idx, after);
            if (after.IsEmpty)
            {
                _audio?.PlayToolBreak();  // av W3-13：镐碎裂音
                ShowToolBreakHint();
                // m10 B2：碎裂。stack 是磨损前的完整物品栈（碎块颜色取这把镐的贴图均值色）；
                // 场景里没有 PlayerController（旧 fixture / 纯逻辑测试）时没有伤害对象与
                // 出生锚点，跳过——与 SetBlockDrops 缺表的容忍策略一致。
                if (_player != null)
                {
                    PickaxeShard.SpawnScatter(_player, stack, ctx.Items, x, y, z);
                }
            }
        }

        /// <summary>m10 B1：镐坏提示显示时长（秒），与门槛提示同款「不叠不刷」语义。</summary>
        private const float ToolBreakHintDuration = 2f;

        /// <summary>m10 B2：镐碎裂提示文案。B1 只「坏掉了」，B2 起真的有碎块散落扎脚——
        /// 换成「碎」。public const 是给 EditMode 测试锁文案防误改（OnGUI 本身 EditMode 不跑）。</summary>
        public const string ToolBreakHintText = "镐碎了！";

        /// <summary>m10 B1：镐坏提示画在门槛提示上方 30px（两者同时出现时不叠字）。</summary>
        private const float ToolBreakHintBottomOffset = ToolTierHintBottomOffset + 30f;

        /// <summary>m10 B1：镐耐久尽提示的累计触发次数。public 是给 EditMode 测试断言
        /// 「触发一次、显示窗口内不重复」用的（OnGUI 本身 EditMode 不跑）。</summary>
        public int ToolBreakHintCount { get; private set; }

        private float _toolBreakHintUntil = float.NegativeInfinity;

        /// <summary>m10 B1：记录一次「<see cref="ToolBreakHintText"/>」提示。显示窗口（2s）内
        /// 重复损坏（比如接连挖碎两把旧镐）不叠加不重置——与 ShowToolTierHint 同款语义。</summary>
        private void ShowToolBreakHint()
        {
            if (Time.time < _toolBreakHintUntil) return;
            ToolBreakHintCount++;
            _toolBreakHintUntil = Time.time + ToolBreakHintDuration;
        }

        /// <summary>
        /// m10 A3：hotbar 上方的一次性文字提示（m10 B1 起两行：门槛提示 + 镐坏提示；
        /// m11 ②加第三行交互提示——床白天 / 无箭）。简单 GUI.Label，不做 toast 系统；
        /// m11 ②另画弓蓄力条（准星下方）。
        /// 所有提示与蓄力条都不活跃时本方法第一行就 return，平时零开销。
        /// </summary>
        private void OnGUI()
        {
            bool tierActive = ToolTierHintCount > 0 && Time.time < _toolTierHintUntil;
            bool breakActive = ToolBreakHintCount > 0 && Time.time < _toolBreakHintUntil;
            bool interactionActive = InteractionHintCount > 0
                && Time.time < _interactionHintUntil
                && _interactionHintText != null;
            if (!tierActive && !breakActive && !interactionActive && !_bowCharging) return;

            if (_toolTierHintStyle == null)
            {
                // GUI.skin 只能在 OnGUI 里访问，样式首帧构造一次缓存复用（HotbarUI 同款）
                _toolTierHintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    normal = { textColor = new Color(1f, 0.92f, 0.55f) },
                };
            }

            const float width = 220f;
            if (tierActive)
            {
                var rect = new Rect(
                    (Screen.width - width) / 2f, Screen.height - ToolTierHintBottomOffset, width, 26f);
                GUI.Label(rect, "需要更好的镐", _toolTierHintStyle);
            }

            if (breakActive)
            {
                var rect = new Rect(
                    (Screen.width - width) / 2f, Screen.height - ToolBreakHintBottomOffset, width, 26f);
                GUI.Label(rect, ToolBreakHintText, _toolTierHintStyle);
            }

            if (interactionActive)
            {
                var rect = new Rect(
                    (Screen.width - width) / 2f, Screen.height - InteractionHintBottomOffset, width, 26f);
                GUI.Label(rect, _interactionHintText, _toolTierHintStyle);
            }

            if (_bowCharging)
            {
                DrawBowChargeBar();
            }
        }

        /// <summary>
        /// m11 ②：弓蓄力条——准星下方 36px 的纯色矩形（底灰 + 绿色按比例填充）。
        /// 素材 ui-charge-bar.png 已入库 Assets/Art/UI，但 standalone build 读不到
        /// Assets 目录（m6 B3 的教训：必须走 streamingAssetsPath），先按任务卡允许用
        /// 纯色矩形占位，素材挪进 StreamingAssets/ui 后再换贴图。
        /// </summary>
        private void DrawBowChargeBar()
        {
            float ratio = BowChargeRatio01();
            const float barWidth = 160f;
            const float barHeight = 8f;
            var rect = new Rect(
                (Screen.width - barWidth) / 2f, Screen.height / 2f + 36f, barWidth, barHeight);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.9f, 0.4f);
            GUI.DrawTexture(
                new Rect(rect.x, rect.y, barWidth * ratio, barHeight), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
