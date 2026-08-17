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

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip);
        }
    }
}