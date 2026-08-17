using MyWorld.Core.Farming;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Combat;
using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 农业系统的 tick 宿主（m11 W1-6 集成点②）：以 0.5s 累计器批量推进
    /// <see cref="FarmSystem"/>（作物生长）与 <see cref="BreedingSystem"/>（孕期/幼崽计时），
    /// 并把到点的幼崽记录刷成真 mob（scale 0.5）、长大的幼崽恢复 scale 1。
    /// <para>
    /// <b>累计器节奏</b>：两个系统都是「绝对时间阈值」语义（粒度无关、replay 可复现），
    /// 批量推进不影响结果——0.5s 一批只是别把逐帧工作挤进 ChunkStreamer 的 8ms 预算
    /// （照 MobManager 的 tick 模式挂 Unity 侧，但不逐帧跑）。Esc 暂停（timeScale=0）时
    /// deltaTime 恒 0，天然停摆。
    /// </para>
    /// <para>
    /// 农田生长喂的是<b>世界时钟 tick</b>（<c>delta × TimeOfDay.Speed</c>）：FarmSystem 的
    /// 单级基准（小麦 4000 tick）按「一天 24000 tick」校准，直接喂秒会让作物疯长几十倍。
    /// 繁殖计时是真实秒（MC 同款 30s 孕期），喂 delta 原值。
    /// </para>
    /// <para>
    /// 锄/播种/骨粉/喂食的右键路由在 BlockInteraction（B 批接线），本类只管时间推进
    /// 与幼崽实体化；Core 侧跨表引用校验失败（物品表缺种子/产物）时
    /// WorldBootstrap 不挂本类，游戏其余部分照常。
    /// </para>
    /// </summary>
    public sealed class FarmingHost : MonoBehaviour
    {
        /// <summary>批量推进间隔（秒）。0.5s 对肉眼可察的生长节奏绰绰有余。</summary>
        public const float TickIntervalSeconds = 0.5f;

        /// <summary>TimeOfDay 缺席时的时钟速率兜底（= TimeOfDay 默认 60，一天 400 实秒）。</summary>
        public const float FallbackTimeSpeed = 60f;

        private World _world;
        private PlayerContext _context;
        private MobManager _mobManager;
        private float _accumulator;

        /// <summary>注入依赖（WorldBootstrap 挂载后调用）。mobManager 为空时幼崽记录留到下一批。</summary>
        public void Bind(World world, PlayerContext context, MobManager mobManager)
        {
            _world = world;
            _context = context;
            _mobManager = mobManager;
        }

        private void Update()
        {
            if (_context == null) return;
            _accumulator += Time.deltaTime;
            if (_accumulator < TickIntervalSeconds) return;

            float batch = _accumulator;
            _accumulator = 0f;
            TickBatch(batch);
        }

        /// <summary>
        /// 推进一批（公开供 EditMode 测试注入固定步长；Update 自动走同一入口）。
        /// <paramref name="dtSeconds"/> 是本批累计的真实秒。
        /// </summary>
        public void TickBatch(float dtSeconds)
        {
            if (_context == null || dtSeconds <= 0f) return;

            var farm = _context.FarmSystem;
            if (farm != null && _world != null)
            {
                float speed = _context.Time != null ? _context.Time.Speed : FallbackTimeSpeed;
                farm.Tick(_world, dtSeconds * speed);
            }

            var breeding = _context.BreedingSystem;
            if (breeding != null)
            {
                breeding.Tick(dtSeconds);
                SpawnNewborns(breeding);
                GrowBabies(breeding);
            }
        }

        /// <summary>孕期到点的幼崽刷成真 mob：VisualScale = <see cref="BreedingSystem.BabyScale"/>，
        /// 并登记进系统开始 600s 长大计时。mobManager 未挂时<b>先不取</b>——TakeNewborns
        /// 是取走即清，先判宿主才不会把记录取出来又丢掉。</summary>
        private void SpawnNewborns(BreedingSystem breeding)
        {
            if (_mobManager == null) return; // 无宿主先不消费，记录留在队列等下一批
            var newborns = breeding.TakeNewborns();
            if (newborns.Count == 0) return;

            foreach (var record in newborns)
            {
                var mob = _mobManager.SpawnMobAt(record.Kind, record.Position);
                if (mob == null) continue;
                mob.VisualScale = record.Scale;
                breeding.RegisterBaby(mob.EntityId, record.Kind);
            }
        }

        /// <summary>长大的幼崽恢复 scale 1（实体可能已被 despawn——按 id 查不到就跳过）。</summary>
        private void GrowBabies(BreedingSystem breeding)
        {
            var grown = breeding.TakeGrownBabies();
            if (grown.Count == 0 || _mobManager == null) return;

            foreach (int entityId in grown)
            {
                if (_mobManager.TryGetMobById(entityId, out var mob))
                {
                    mob.VisualScale = 1f;
                }
            }
        }
    }
}
