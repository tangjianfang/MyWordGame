using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// m9 B1：战斗手感三件套的宿主薄组件（闪红 / 击退冲量 / 死亡缩小，命中音在
    /// <see cref="MyWorld.Unity.Audio.PlayerAudioSystem.PlayHit"/>）。与
    /// <see cref="MobView"/> 挂同一宿主 GameObject，由 <see cref="MobManager"/> 刷怪时
    /// 挂上；<see cref="CombatController"/> 命中时 GetComponent 取用，
    /// 旧测试宿主没挂它则整体 null 安全跳过。
    /// <para>
    /// <b>闪红</b>：MPB 逐渲染器临时 tint（m8 终审核准的职业染色同款手法，不改
    /// sharedMaterial），过期 <see cref="MaterialPropertyBlock.Clear"/> 清 override、
    /// 材质底色透出来。时长 0.15s（spec §4），与 Core <see cref="MobAI.HitFlashDuration"/>
    /// 同值——MobView.LateUpdate 每帧用部位底色重写实例色，红闪的<b>每帧渲染</b>实际由
    /// Core 计时器驱动，所以 <see cref="FlashRed"/> 会同步刷 <see cref="Mob.HitFlashTimer"/>：
    /// 两条通道同窗同色，互不打架（本组件自己的 tint 写在 Update，早于 MobView 的
    /// LateUpdate，同帧双双落红）。
    /// </para>
    /// <para>
    /// <b>击退</b>：写 Core <see cref="Mob.Position"/>，不写 Unity transform——
    /// MobView.LateUpdate 每帧用 Mob.Position 覆写 transform.position，写 transform
    /// 必然被冲掉。MobAI.Tick 对活着的 mob 也是「Position += Velocity×dt」的加法积分，
    /// 本组件的击退位移同为加法，两侧无论 Update 执行顺序如何都可交换、自然叠加
    /// （受击逃跑 4m/s 与击退冲量同向合成）。冲量非瞬移：初速 10m/s 指数衰减，
    /// 累计位移恰好 1.5m。
    /// </para>
    /// <para>
    /// <b>缩小</b>：Dying 时 scale 等比插值 0.3s 到 0。0.3s &lt; <see cref="MobAI.DefaultDeathTimer"/>
    /// （0.5s），scale 到 0 之后 MobManager 的 Dying 倒计时才走既有移除（RemoveMobAt），
    /// 动画不会被腰斩。
    /// </para>
    /// <para>
    /// 时间统一走 <see cref="TickFeedback"/>（Update 喂 Time.deltaTime）——EditMode 测试
    /// 直调本方法注入步长（编辑器下 Update 不回调、Time.deltaTime 冻结）。
    /// </para>
    /// </summary>
    public sealed class MobHitFeedback : MonoBehaviour
    {
        /// <summary>受击闪红时长（秒）——spec §4「闪红 0.15s」。必须与
        /// <see cref="MobAI.HitFlashDuration"/> 同值（两条渲染通道同窗，测试守此不变量）。</summary>
        public const float FlashDuration = 0.15f;

        /// <summary>击退总冲量（米）——spec §4「冲量 1.5m」。首帧快、指数衰减、总量恰好耗完。</summary>
        public const float KnockbackDistance = 1.5f;

        /// <summary>击退速度指数衰减时间常数（秒）：v(t)=v0·e^(−t/τ)，v0·τ=总冲量 1.5m
        /// （v0 = 1.5/0.15 = 10m/s——猛推一下，约 0.35s 烧掉九成）。</summary>
        public const float KnockbackDecayTau = 0.15f;

        /// <summary>死亡缩小时长（秒）——spec §4「0.3s 缩小消失」。必须 &lt;
        /// <see cref="MobAI.DefaultDeathTimer"/>（Dying 0.5s）：scale 到 0 后才走既有移除。</summary>
        public const float ShrinkDuration = 0.3f;

        /// <summary>击退写入的 Core 实体（Attach 注入）。null 时击退跳过，闪红/缩小照常。</summary>
        public Mob Mob;

        /// <summary>剩余击退距离（米）——测试观测「衰减到 0」用，生产只读。</summary>
        public float KnockbackRemaining => _kbRemaining;

        /// <summary>缩小是否已走完（scale 到 0，或从未播放——后者不阻塞尸体移除）。</summary>
        public bool IsShrinkComplete => !_shrinkPlayed || _shrinkElapsed >= ShrinkDuration;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _block;
        private Renderer[] _renderers;
        private float _flashTimer;

        private Vector3 _kbDir = Vector3.right;
        private float _kbRemaining;
        private float _kbSpeed;

        private bool _shrinkPlayed;
        private float _shrinkElapsed;
        private Vector3 _shrinkStartScale = Vector3.one;

        /// <summary>刷怪挂载入口（与 <see cref="MobView.Attach"/> 同款）。</summary>
        public static MobHitFeedback Attach(GameObject host, Mob mob)
        {
            var fb = host.AddComponent<MobHitFeedback>();
            fb.Mob = mob;
            return fb;
        }

        /// <summary>
        /// 受击闪红：MPB tint 立即涂红 + 同步刷 Core 计时器（MobView 每帧红闪渲染
        /// 由 Core 计时器驱动，见类注释「两条通道」）。
        /// </summary>
        public void FlashRed()
        {
            _flashTimer = FlashDuration;
            if (Mob != null) Mob.HitFlashTimer = FlashDuration;
            ApplyTint();
        }

        /// <summary>
        /// 击退冲量：方向只取水平分量并归一（纵向垃圾分量丢弃——体素世界里 mob 不离地），
        /// 零向量兜底 +X（与 MobAI 受击逃跑的重合兜底同向，不除零）。总位移
        /// <see cref="KnockbackDistance"/>，由 <see cref="TickFeedback"/> 按指数衰减的
        /// 速度分帧烧完——非瞬移。
        /// </summary>
        public void ApplyKnockback(Vector3 direction)
        {
            var d = new Vector3(direction.x, 0f, direction.z);
            float len = d.magnitude;
            _kbDir = len > 1e-5f ? d / len : Vector3.right;
            _kbRemaining = KnockbackDistance;
            _kbSpeed = KnockbackDistance / KnockbackDecayTau; // v0·τ = 1.5m
        }

        /// <summary>死亡缩小：scale 从当前值等比插值到 0（0.3s）。幂等——重复调用不重置进度。</summary>
        public void PlayDeathShrink()
        {
            if (_shrinkPlayed) return;
            _shrinkPlayed = true;
            _shrinkElapsed = 0f;
            _shrinkStartScale = transform.localScale; // 捕获原体型：拼装/旧单 cube 都不被拉直
        }

        private void Update() => TickFeedback(Time.deltaTime);

        /// <summary>
        /// 四件套的唯一推进入口（时间注入点）：闪红倒计时 / 击退冲量积分 / 缩小插值。
        /// EditMode 测试直调本方法注入 dt；运行时由 Update 喂 Time.deltaTime。
        /// </summary>
        public void TickFeedback(float dt)
        {
            if (dt <= 0f) return;

            // 1) 闪红：激活期间持续压红 tint（防其它系统中途改写实例色），过期清 override
            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                if (_flashTimer > 0f) ApplyTint();
                else ClearTint();
            }

            // 2) 击退：速度衰减的位移积分，加法写入 Core Position（与 MobAI.Tick 可交换，
            //    无论两侧 Update 顺序如何都自然叠加）；末段按剩余量钳制，总量恰好 1.5m
            if (_kbRemaining > 0f && Mob != null)
            {
                float step = Mathf.Min(_kbSpeed * dt, _kbRemaining);
                Mob.Position = new Float3(
                    Mob.Position.X + _kbDir.x * step,
                    Mob.Position.Y,
                    Mob.Position.Z + _kbDir.z * step);
                _kbRemaining -= step;
                _kbSpeed *= Mathf.Exp(-dt / KnockbackDecayTau);
            }

            // 3) 缩小：从捕获的原体型等比插值到 0（k 钳在 [0,1]，走完精确归零后不再动）
            if (_shrinkPlayed && _shrinkElapsed < ShrinkDuration)
            {
                _shrinkElapsed = Mathf.Min(_shrinkElapsed + dt, ShrinkDuration);
                float k = 1f - _shrinkElapsed / ShrinkDuration;
                transform.localScale = _shrinkStartScale * k;
            }
        }

        /// <summary>宿主全部渲染器（拼装部位 + host 本体），首次用到时缓存。</summary>
        private Renderer[] Renderers => _renderers ??= GetComponentsInChildren<Renderer>();

        private void ApplyTint()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            foreach (var r in Renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(ColorId, Color.red);
                r.SetPropertyBlock(_block);
            }
        }

        /// <summary>过期复原：清空 MPB 里的实例色 override，材质（职业染色）底色透出来。</summary>
        private void ClearTint()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            foreach (var r in Renderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(_block);
            }
        }
    }
}
