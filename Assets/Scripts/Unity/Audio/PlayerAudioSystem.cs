using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// 玩家音效系统。footstep / place / break / hit 四个音效。
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
    /// </summary>
    public class PlayerAudioSystem : MonoBehaviour
    {
        [SerializeField] private AudioClip footstepClip;
        [SerializeField] private AudioClip placeClip;
        [SerializeField] private AudioClip breakClip;
        [SerializeField] private AudioClip hitClip;

        private AudioSource _source;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;  // 2D 音效

            // 加载 clip（缺失不抛）。路径相对于 Assets/Resources/。
            footstepClip = LoadClipOrNull("Audio/footstep");
            placeClip = LoadClipOrNull("Audio/place");
            breakClip = LoadClipOrNull("Audio/break");
            hitClip = LoadOrCreateHitClip(); // m9 B1：缺失时程序生成，不告警（有兜底不算异常）
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

        public void PlayFootstep() { PlayClip(footstepClip); }
        public void PlayPlace() { PlayClip(placeClip); }
        public void PlayBreak() { PlayClip(breakClip); }

        /// <summary>m9 B1：近战命中打击音（CombatController.DoAttack 调）。防御模式同上。</summary>
        public void PlayHit() { PlayClip(hitClip); }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip);
        }
    }
}