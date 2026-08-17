using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// av W1-9：环境循环音频——洞穴 / 白天鸟 / 夜晚虫。
    /// <para>
    /// 单 AudioSource 循环，轨名由 <see cref="PickTrack"/> 决定（y &lt; 40 优先洞穴，
    /// 地面按昼夜选鸟/虫）。tick 节流 2s 复查（首查立即判定），
    /// clip 缺失 LogWarning 一次 + Stop（不抛）。
    /// </para>
    /// <para>
    /// 音量走 <see cref="MusicVolumeBus.Volume"/>；昼夜判定唯一经
    /// <see cref="MyWorld.Unity.Combat.MobManager.IsNightPhase"/>。
    /// </para>
    /// </summary>
    public sealed class AmbientAudioSystem : MonoBehaviour
    {
        /// <summary>洞穴深度阈值：海平面 62，y &lt; 40（地下 22 格以上）视为洞穴。</summary>
        public const float CaveDepthY = 40f;

        /// <summary>轨名复查节流：2s 一次，避免每帧 Resources.Load。</summary>
        public const float TrackCheckInterval = 2f;

        private AudioSource _source;
        private string _currentTrack;
        private float _timer;

        public string CurrentTrack => _currentTrack;

        /// <summary>
        /// 按玩家 y + 昼夜决定环境轨。y &lt; 40 永远洞穴优先（洞穴里也分昼夜但视觉一致），
        /// 地面按昼夜：白天 amb-birds / 夜晚 amb-crickets。
        /// </summary>
        public static string PickTrack(float playerY, bool isNight)
        {
            if (playerY < CaveDepthY) return "amb-cave";
            return isNight ? "amb-crickets" : "amb-birds";
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = MusicVolumeBus.Volume;
            _timer = 0f;  // 首查立即判定
        }

        private void Update()
        {
            var ctx = MyWorld.Unity.Gameplay.PlayerContext.Instance;
            // 取玩家 transform.position.y（PlayerContext.Instance 是 Unity 侧玩家单例，
            // 这里只取 y 坐标，不感知玩家组件的细节）
            var playerTf = ctx != null ? ctx.transform : null;
            float playerY = playerTf != null ? playerTf.position.y : 70f;
            var time = ctx != null ? ctx.Time : null;
            float dayPhase = time != null ? time.DayPhase01 : 0.5f;
            bool isNight = MyWorld.Unity.Combat.MobManager.IsNightPhase(dayPhase);

            Tick(Time.deltaTime, playerY, isNight);
        }

        /// <summary>时间注入入口（测试用）。首查立即判定，后续每 2s 复查。</summary>
        public void Tick(float dt, float playerY, bool isNight)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = TrackCheckInterval;

            string want = PickTrack(playerY, isNight);
            if (want == _currentTrack) return;

            _currentTrack = want;
            var clip = Resources.Load<AudioClip>("Audio/" + want);
            if (clip == null)
            {
                Debug.LogWarning($"[AmbientAudioSystem] 环境音缺失: Audio/{want}");
                if (_source != null) _source.Stop();
                return;
            }
            _source.clip = clip;
            _source.volume = MusicVolumeBus.Volume;
            _source.Play();
        }
    }
}