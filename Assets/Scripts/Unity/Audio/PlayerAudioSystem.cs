using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// 玩家音效系统。footstep / place / break / hit 四个基础音效 +
    /// 11 个事件音（av W1-6：eat/hurt/die/pickup/craft/door-open/door-close/
    /// hoe-till/plant/harvest/tool-break）。
    /// <para>
    /// 加载失败时不抛（nice-to-have，不阻断游戏）。clip 为 null 时直接 return，
    /// <see cref="Awake"/> 里 Resources.Load 缺失会 <see cref="Debug.LogWarning"/> 提示一次，
    /// 方便排查而不是沉默失败。
    /// </para>
    /// <para>
    /// m9 B1：hit 打击音按 spec §4「复用既有音频资源，没有则程序生成短促打击声」——
    /// Resources 没有 Audio/hit 时程序生成 80ms 指数衰减噪声闷响（确定性 LCG，
    /// 不持有随机数对象），日后补 Assets/Resources/Audio/hit.ogg 即自动优先真资源。
    /// </para>
    /// <para>
    /// av W1-6：静态 <see cref="Instance"/> 让合成 UI（CraftingPocketUi / CraftingFurnaceUi）
    /// 与门系统（RedstoneSystem）等不在玩家宿主链上的系统取音效 —— 它们没有 _audio 缓存，
    /// 也不该去玩家宿主链上 FindObjectOfType（同一物体可能没有）。Awake 首行赋值，
    /// 测试用反射清回 null 保持夹具隔离。
    /// </para>
    /// </summary>
    public class PlayerAudioSystem : MonoBehaviour
    {
        /// <summary>全局唯一实例——合成 UI / RedstoneSystem 等取音效的入口。
        /// Awake 首行赋值；测试反射清回 null（Instance setter 是 private）。</summary>
        public static PlayerAudioSystem Instance { get; private set; }

        // ─── 基础四音（m9 B1 既有） ──────────────────────────────────────────
        [SerializeField] private AudioClip footstepClip;
        [SerializeField] private AudioClip placeClip;
        [SerializeField] private AudioClip breakClip;
        [SerializeField] private AudioClip hitClip;

        // ─── 11 个事件音（av W1-6）─────────────────────────────────────────
        [SerializeField] private AudioClip eatClip;
        [SerializeField] private AudioClip hurtClip;
        [SerializeField] private AudioClip dieClip;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private AudioClip craftClip;
        [SerializeField] private AudioClip doorOpenClip;
        [SerializeField] private AudioClip doorCloseClip;
        [SerializeField] private AudioClip hoeTillClip;
        [SerializeField] private AudioClip plantClip;
        [SerializeField] private AudioClip harvestClip;
        [SerializeField] private AudioClip toolBreakClip;
        // ─── m13 W3 火枪音（程序生成兜底，真资源优先）─────────────────────
        [SerializeField] private AudioClip fireClip;
        // 咔哒：无弹开火拒绝提示——短促金属干声（手动触发扳机但未击发）。
        [SerializeField] private AudioClip clickClip;

        private AudioSource _source;

        private void Awake()
        {
            // 全局实例：合成 UI / 门系统靠它取音效
            Instance = this;

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;  // 2D 音效

            // 加载既有 4 个
            footstepClip = LoadClipOrNull("Audio/footstep");
            placeClip = LoadClipOrNull("Audio/place");
            breakClip = LoadClipOrNull("Audio/break");
            hitClip = LoadOrCreateHitClip(); // m9 B1：缺失时程序生成，不告警（有兜底不算异常）

            // 加载 11 个事件音（缺失只 warn 一次，不抛）
            eatClip        = LoadClipOrNull("Audio/eat");
            hurtClip       = LoadClipOrNull("Audio/hurt");
            dieClip        = LoadClipOrNull("Audio/die");
            pickupClip     = LoadClipOrNull("Audio/pickup");
            craftClip      = LoadClipOrNull("Audio/craft");
            doorOpenClip   = LoadClipOrNull("Audio/door-open");
            doorCloseClip  = LoadClipOrNull("Audio/door-close");
            hoeTillClip    = LoadClipOrNull("Audio/hoe-till");
            plantClip      = LoadClipOrNull("Audio/plant");
            harvestClip    = LoadClipOrNull("Audio/harvest");
            toolBreakClip  = LoadClipOrNull("Audio/tool-break");
            // m13 W3：火枪音程序生成（缺失不告警——有兜底不算异常），逻辑照 hit 走。
            fireClip       = LoadOrCreateFireClip();
            // 咔哒（无弹开火拒绝）：同样程序生成——无 Assets/Resources/Audio/click 时兜底短促金属干声。
            clickClip      = LoadOrCreateClickClip();
        }

        private static AudioClip LoadClipOrNull(string path)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"[PlayerAudioSystem] AudioClip 缺失: {path}");
            return clip;
        }

        /// <summary>
        /// m9 B1：打击音加载——真资源优先，缺失则程序生成 80ms 单声道「闷响」：
        /// 白噪声 × 平方衰减包络 ×0.6 增益。整数 LCG（种子取黄金比常数）保证
        /// 跨平台结果一致、不依赖随机数对象（项目铁律，与 BlockDrops.RollCount 同思路）。
        /// AudioClip.Create 失败（极端环境）返回 null——PlayClip 静默跳过。
        /// </summary>
        private static AudioClip LoadOrCreateHitClip()
        {
            var clip = Resources.Load<AudioClip>("Audio/hit");
            if (clip != null) return clip;

            const int sampleRate = 44100;
            const int sampleCount = 3528; // 0.08s × 44100
            var generated = AudioClip.Create("hit_procedural", sampleCount, 1, sampleRate, false);
            if (generated == null) return null;
            var data = new float[sampleCount];
            uint h = 0x9E3779B9u; // LCG 种子（Knuth 黄金比），确定性
            for (int i = 0; i < sampleCount; i++)
            {
                h = h * 1664525u + 1013904223u;
                float noise = ((h >> 16) & 0xFFFF) / 65535f * 2f - 1f; // [-1,1)
                float env = 1f - (float)i / sampleCount;               // 线性包络
                data[i] = noise * env * env * 0.6f;                    // 平方收尾 → 短促的「啪」
            }
            generated.SetData(data, 0);
            return generated;
        }

        // ─── 既有四音（m9 B1）─────────────────────────────────────────────
        public void PlayFootstep() { PlayClip(footstepClip); }
        public void PlayPlace() { PlayClip(placeClip); }
        public void PlayBreak() { PlayClip(breakClip); }

        /// <summary>m9 B1：近战命中打击音（CombatController.DoAttack 调）。防御模式同上。</summary>
        public void PlayHit() { PlayClip(hitClip); }

        // ─── 11 个事件音（av W1-6，Task 13 接线批全部消费）──────────────────
        public void PlayEat()       { PlayClip(eatClip); }
        public void PlayHurt()      { PlayClip(hurtClip); }
        public void PlayDie()       { PlayClip(dieClip); }
        public void PlayPickup()    { PlayClip(pickupClip); }
        public void PlayCraft()     { PlayClip(craftClip); }
        public void PlayDoorOpen()  { PlayClip(doorOpenClip); }
        public void PlayDoorClose() { PlayClip(doorCloseClip); }
        public void PlayHoeTill()   { PlayClip(hoeTillClip); }
        public void PlayPlant()     { PlayClip(plantClip); }
        public void PlayHarvest()   { PlayClip(harvestClip); }
        public void PlayToolBreak() { PlayClip(toolBreakClip); }

        // ─── m13 W3：火枪开火 + 咔哒（无弹） ─────────────────────────────
        public void PlayFire()  { PlayClip(fireClip); }
        public void PlayClick() { PlayClip(clickClip); }

        /// <summary>
        /// m13 W3：火枪开火音加载——真资源优先，缺失则程序生成 ~120ms 低频冲击（80Hz 主导 +
        /// 白噪声衰减包络）：「砰！」的一声。LCG 确定性（同 hit 兜底的 Knuth 黄金比种子），
        /// 与 <see cref="LoadOrCreateHitClip"/> 同思路——clip 创建失败静默返回 null，
        /// <see cref="PlayClip"/> 跳过。参数与 hit 区别：时长更长（hit 80ms / fire 120ms）、
        /// 频率更低（hit 高频尖锐 / fire 低频闷响）、振幅峰更高（fire ×0.85 vs hit ×0.6）。
        /// </summary>
        private static AudioClip LoadOrCreateFireClip()
        {
            var clip = Resources.Load<AudioClip>("Audio/fire");
            if (clip != null) return clip;

            const int sampleRate = 44100;
            const int sampleCount = 5292; // 0.12s × 44100
            var generated = AudioClip.Create("fire_procedural", sampleCount, 1, sampleRate, false);
            if (generated == null) return null;
            var data = new float[sampleCount];
            uint h = 0x9E3779B9u; // 与 hit 同种子——不同生成器取同一确定性根
            for (int i = 0; i < sampleCount; i++)
            {
                h = h * 1664525u + 1013904223u;
                float noise = ((h >> 16) & 0xFFFF) / 65535f * 2f - 1f; // [-1,1)
                float t = (float)i / sampleCount;
                float env = 1f - t;             // 线性衰减
                // 低通近似：相邻样本差分（高频被吃掉）+ 振幅平方收尾
                float lp = noise * 0.7f + (i > 0 ? data[i - 1] * 0.3f : 0f);
                data[i] = lp * env * env * 0.85f;
            }
            generated.SetData(data, 0);
            return generated;
        }

        /// <summary>
        /// m13 W3：咔哒音（无弹开火拒绝提示）加载——真资源优先，缺失则程序生成 ~30ms
        /// 高频金属干声：直接开/关短脉冲（接近「咔」的瞬态），与火枪的「砰」拉开对比。
        /// 同确定性种子根（Knuth 黄金比）；clip 创建失败静默跳过。
        /// </summary>
        private static AudioClip LoadOrCreateClickClip()
        {
            var clip = Resources.Load<AudioClip>("Audio/click");
            if (clip != null) return clip;

            const int sampleRate = 44100;
            const int sampleCount = 1324; // 0.03s × 44100
            var generated = AudioClip.Create("click_procedural", sampleCount, 1, sampleRate, false);
            if (generated == null) return null;
            var data = new float[sampleCount];
            uint h = 0x9E3779B9u;
            for (int i = 0; i < sampleCount; i++)
            {
                h = h * 1664525u + 1013904223u;
                float noise = ((h >> 16) & 0xFFFF) / 65535f * 2f - 1f; // [-1,1)
                float t = (float)i / sampleCount;
                // 高频主导：双样本差分（火枪是低通，咔哒反着来） + 短衰减
                float hp = i > 0 ? (noise - (data[i - 1]) * 0.5f) : noise;
                float env = 1f - t;
                data[i] = hp * env * 0.5f;
            }
            generated.SetData(data, 0);
            return generated;
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip);
        }
    }
}