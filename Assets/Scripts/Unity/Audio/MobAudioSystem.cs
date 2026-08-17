using UnityEngine;

namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// av W1-10：生物叫声——idle 按确定性哈希间隔 8–20s 随机播一次、
    /// hurt 由 CombatController 命中方调。
    /// <para>
    /// Resources 查表 Audio/Mobs/&lt;kind 小写&gt;-{idle,hurt}；缺文件回退
    /// generic-{small,large}-{idle,hurt}（小=鸡/兔/仓鼠，大=其它），
    /// 再缺静默。挂载模式照 <see cref="MyWorld.Unity.Combat.MobHitFeedback"/>
    /// （同宿主 GameObject），命中方 GetComponent 取用。
    /// </para>
    /// <para>
    /// 距离玩家 &gt; <see cref="MaxHearDistance"/> 不播 idle——叫声按耳听半径算；
    /// hurt 不论距离都该响（命中已发生）。
    /// </para>
    /// </summary>
    public sealed class MobAudioSystem : MonoBehaviour
    {
        /// <summary>idle 叫声最远听距（米）。超过此距离 idle 间隔重置，回声场再数。</summary>
        public const float MaxHearDistance = 16f;
        public const int IntervalMinSeconds = 8;
        public const int IntervalSpanSeconds = 13;  // 区间 8..20

        /// <summary>本 mob 的类型（SpawnMob 时挂上的 Kind）。</summary>
        public MyWorld.Core.Entities.MobKind Kind;

        private AudioSource _source;
        private float _idleIn;
        private int _idleIndex;

        /// <summary>静态挂载助手（照 MobHitFeedback.Attach 模式）。</summary>
        public static MobAudioSystem Attach(GameObject host, MyWorld.Core.Entities.MobKind kind)
        {
            var c = host.AddComponent<MobAudioSystem>();
            c.Kind = kind;
            return c;
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _idleIn = IdleIntervalSeconds((int)Kind, 0);
        }

        /// <summary>确定性间隔（8–20s）：整数哈希（项目铁律，hash_pixel 同思路）。
        /// 不持随机数对象，跨平台同输入同输出。</summary>
        public static int IdleIntervalSeconds(int mobKindValue, int index)
        {
            uint n = ((uint)mobKindValue * 374761393u + (uint)index * 668265263u) & 0xFFFFFFFFu;
            n = ((n ^ (n >> 13)) * 1274126177u) & 0xFFFFFFFFu;
            n = (n ^ (n >> 16)) & 0xFFFFFFFFu;
            return IntervalMinSeconds + (int)(n % (uint)IntervalSpanSeconds);
        }

        /// <summary>专属 Resources 路径：Audio/Mobs/&lt;kind 小写&gt;-{idle,hurt}。</summary>
        public static string ClipPath(MyWorld.Core.Entities.MobKind kind, bool hurt)
        {
            return $"Audio/Mobs/{kind.ToString().ToLowerInvariant()}-{(hurt ? "hurt" : "idle")}";
        }

        /// <summary>小动物三兄弟（鸡/兔/仓鼠）走 generic-small，其它走 generic-large。</summary>
        private static bool IsSmall(MyWorld.Core.Entities.MobKind kind)
        {
            return kind == MyWorld.Core.Entities.MobKind.Chicken
                || kind == MyWorld.Core.Entities.MobKind.Rabbit
                || kind == MyWorld.Core.Entities.MobKind.Hamster;
        }

        /// <summary>回退路径：Audio/Mobs/generic-{small|large}-{idle|hurt}。</summary>
        public static string FallbackPath(MyWorld.Core.Entities.MobKind kind, bool hurt)
        {
            return $"Audio/Mobs/generic-{(IsSmall(kind) ? "small" : "large")}-{(hurt ? "hurt" : "idle")}";
        }

        /// <summary>距离玩家 <paramref name="distanceToPlayer"/> &gt; <see cref="MaxHearDistance"/>
        /// 时重置倒计时（让玩家靠近时才听见首次叫声）；否则按哈希间隔累计到点就播。
        /// hurt 不走本方法——<see cref="PlayHurt"/> 直接播。</summary>
        public void TickIdle(float dt, float distanceToPlayer)
        {
            if (distanceToPlayer > MaxHearDistance)
            {
                _idleIn = 1f;  // 玩家靠近后 1s 内必响一次（不必等满 8s）
                return;
            }
            _idleIn -= dt;
            if (_idleIn > 0f) return;
            _idleIndex++;
            _idleIn = IdleIntervalSeconds((int)Kind, _idleIndex);
            Play(Resources.Load<AudioClip>(ClipPath(Kind, false))
                 ?? Resources.Load<AudioClip>(FallbackPath(Kind, false)));
        }

        /// <summary>受击音——命中方调；不论距离。</summary>
        public void PlayHurt()
        {
            Play(Resources.Load<AudioClip>(ClipPath(Kind, true))
                 ?? Resources.Load<AudioClip>(FallbackPath(Kind, true)));
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && _source != null) _source.PlayOneShot(clip);
        }
    }
}