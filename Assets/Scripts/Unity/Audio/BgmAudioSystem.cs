using System.Collections.Generic;
using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// av W1-8：BGM 三态（Menu / Day / Night）双 AudioSource 交叉淡化，切换 3s 线性渐变。
    /// <para>
    /// 昼夜判定唯一走 <see cref="MyWorld.Unity.Combat.MobManager.IsNightPhase"/>（CLAUDE.md 铁律），
    /// menuMode 下不被昼夜拉动——Menu 只能由 <see cref="EnterMenu"/> 显式进。
    /// </para>
    /// <para>
    /// clip 缺失 LogWarning 一次并静默——音频是 nice-to-have，不阻断游戏。
    /// Resources/Audio/bgm-{menu,day,night}.ogg 入库后即自动加载。
    /// </para>
    /// </summary>
    public sealed class BgmAudioSystem : MonoBehaviour
    {
        public enum BgmState { Menu, Day, Night }

        /// <summary>切换 3s 线性渐变（spec §5.1）。</summary>
        public const float CrossfadeSeconds = 3f;

        private readonly Dictionary<BgmState, AudioClip> _clips = new Dictionary<BgmState, AudioClip>();
        private readonly HashSet<BgmState> _warned = new HashSet<BgmState>();
        private AudioSource _current;
        private AudioSource _incoming;
        private BgmState _target = BgmState.Menu;
        private float _fade = 1f;

        public BgmState TargetState => _target;

        private void Awake()
        {
            // 两个 AudioSource 共享同一 GameObject：交替承载 current/incoming，
            // 切曲时把 incoming 的音量从 0 渐升到 target，同时把 current 渐降到 0。
            var a = gameObject.AddComponent<AudioSource>();
            var b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b })
            {
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.volume = 0f;
            }
            _current = a;

            // 加载三段 clip（缺失 LogWarning 一次，之后静默）
            foreach (BgmState st in System.Enum.GetValues(typeof(BgmState)))
            {
                LoadClip(st);
            }
            PlayOn(_current, BgmState.Menu);
        }

        /// <summary>Resources 路径生成器——bgm-{state 小写}。</summary>
        public static string ClipPath(BgmState st)
        {
            return "Audio/bgm-" + st.ToString().ToLowerInvariant();
        }

        private void LoadClip(BgmState st)
        {
            _clips[st] = Resources.Load<AudioClip>(ClipPath(st));
            if (_clips[st] == null && _warned.Add(st))
            {
                Debug.LogWarning($"[BgmAudioSystem] AudioClip 缺失: {ClipPath(st)}");
            }
        }

        private void PlayOn(AudioSource src, BgmState st)
        {
            var clip = _clips[st];
            if (clip == null) return;
            src.clip = clip;
            src.volume = MusicVolumeBus.Volume;
            src.Play();
        }

        /// <summary>主菜单打开时调：切到 menu 曲（仅当当前不在 menu）。</summary>
        public void EnterMenu()
        {
            if (_target != BgmState.Menu) BeginTransition(BgmState.Menu);
        }

        /// <summary>开始游戏时调：menu → day。已在 day 时 no-op。</summary>
        public void StartGame()
        {
            if (_target == BgmState.Menu) BeginTransition(BgmState.Day);
        }

        private void BeginTransition(BgmState target)
        {
            _target = target;
            _fade = 0f;
            if (_incoming != null)
            {
                // 上一次淡化未完：把新目标 clip 换到 _incoming 上，避免重叠出声
                var clip = _clips[target];
                if (clip != null && _incoming.clip != clip) _incoming.clip = clip;
                return;
            }
            _incoming = GetOther(_current);
            if (_incoming == null) return;
            var newClip = _clips[target];
            if (newClip == null)
            {
                _incoming = null;
                return;
            }
            _incoming.clip = newClip;
            _incoming.volume = 0f;
            _incoming.Play();
        }

        /// <summary>取 _current 之外的另一个 AudioSource（EditMode + 实机共用）。</summary>
        private AudioSource GetOther(AudioSource x)
        {
            var sources = GetComponents<AudioSource>();
            if (sources.Length < 2) return null;
            return sources[0] == x ? sources[1] : sources[0];
        }

        private void Update()
        {
            // 实机驱动：从 PlayerContext.Time 取昼夜相位（CLAUDE.md：昼夜判定不走 GameObject 名字）
            var ctx = MyWorld.Unity.Gameplay.PlayerContext.Instance;
            var time = ctx != null ? ctx.Time : null;
            float dayPhase = time != null ? time.DayPhase01 : 0.5f;
            bool isNight = MyWorld.Unity.Combat.MobManager.IsNightPhase(dayPhase);
            Tick(Time.deltaTime, isNight, _target == BgmState.Menu);
        }

        /// <summary>时间注入入口（测试用）。menuMode=true 时不随昼夜换曲。</summary>
        public void Tick(float dt, bool isNight, bool menuMode)
        {
            if (!menuMode)
            {
                var want = isNight ? BgmState.Night : BgmState.Day;
                if (_target != want) BeginTransition(want);
            }
            float vol = MusicVolumeBus.Volume;
            if (_incoming != null)
            {
                _fade = Mathf.Min(1f, _fade + dt / CrossfadeSeconds);
                _current.volume = vol * (1f - _fade);
                _incoming.volume = vol * _fade;
                if (_fade >= 1f)
                {
                    _current.Stop();
                    _current.clip = null;
                    _current = _incoming;
                    _incoming = null;
                }
            }
            else if (_current != null)
            {
                _current.volume = vol;
            }
        }
    }
}