using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// 玩家音效系统。footstep / place / break 三个音效。
    /// <para>
    /// 加载失败时不抛（nice-to-have，不阻断游戏）。clip 为 null 时直接 return，
    /// <see cref="Awake"/> 里 Resources.Load 缺失会 <see cref="Debug.LogWarning"/> 提示一次，
    /// 方便排查而不是沉默失败。
    /// </para>
    /// </summary>
    public class PlayerAudioSystem : MonoBehaviour
    {
        [SerializeField] private AudioClip footstepClip;
        [SerializeField] private AudioClip placeClip;
        [SerializeField] private AudioClip breakClip;

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
        }

        private static AudioClip LoadClipOrNull(string path)
        {
            var clip = Resources.Load<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"[PlayerAudioSystem] AudioClip 缺失: {path}");
            return clip;
        }

        public void PlayFootstep() { PlayClip(footstepClip); }
        public void PlayPlace() { PlayClip(placeClip); }
        public void PlayBreak() { PlayClip(breakClip); }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip);
        }
    }
}