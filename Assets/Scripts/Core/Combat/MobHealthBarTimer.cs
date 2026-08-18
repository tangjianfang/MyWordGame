using MyWorld.Core.Entities;

namespace MyWorld.Core.Combat
{
    /// <summary>
    /// 怪物头顶血条纯逻辑状态机（m13 W2）：受击显示 + 3s 淡出 + Boss 常显。
    /// <para>
    /// 抽出目的是 EditMode 可单测——<see cref="UnityEngine.MonoBehaviour"/> 的 OnGUI 在
    /// EditMode 不跑，只测纯数学/状态机；绘制（IMGUI / billboard quad 取舍注释见
    /// <see cref="MobView"/>.DrawHealthBar）只在 MonoBehaviour 路径走。
    /// </para>
    /// <para>
    /// 设计取舍：
    /// ① 取舍注释——血条画法：选 IMGUI（<see cref="UnityEngine.GUI.Box"/> + 前景矩形），
    ///   理由：本批血条只有「宽 0.6-1.2m + 红底绿前景」一种状态、3s 后消失，没有动效、
    ///   不跟随玩家旋转（billboard quad 优势）、不需要光照；billboard quad 需要新建材质
    ///   + Sprite atlas + shader，处理「绿条颜色 + 边框」需要多套纹理，与既有单条
    ///   <see cref="UnityEngine.GUIStyle"/> 路径（<see cref="MobHitFeedback"/> 同款）相比
    ///   工作量大、收益小。IMGUI 的代价是 backbuffer 后绘制、不能被 <c>Camera.Render</c>
    ///   抓到——但 mob 血条只在玩家视野内可见，孩子验收用 UI shot 路径直接可见。
    /// ② Boss（<see cref="MobKind.MachineGuardian"/>）常显——不受 3s 淡出限制；
    ///   <see cref="VisibleKind"/> 在构造期快照一次，避免 mob kind 在受击期间被改
    ///   （m11 W3-3 BossSummon 不会改 kind，但防御式快照更稳）。
    /// </para>
    /// </summary>
    public sealed class MobHealthBarTimer
    {
        /// <summary>受击后血条持续显示时间（秒）：3s 后开始淡出（最后 0.5s alpha 渐变）。</summary>
        public const float VisibleDuration = 3f;

        /// <summary>淡出窗口（秒）：最后 0.5s alpha 从 1 线性降到 0。</summary>
        public const float FadeOutDuration = 0.5f;

        /// <summary>血条可见条件之一：构造时快照的 mob 类型，Boss（27）= true，普通 = false。
        /// 字段名 <see cref="VisibleKind"/> 命名保留——既是 kind 也是「是否常显」的双关，
        /// 实测构造期从传入的 <see cref="MobKind"/> 取值后该字段永远只读。</summary>
        public readonly bool VisibleKind;

        private float _remaining = 0f;

        public MobHealthBarTimer(MobKind kind)
        {
            VisibleKind = kind == MobKind.MachineGuardian;
        }

        /// <summary>受击重置计时到 <see cref="VisibleDuration"/>。
        /// 对 Boss（<see cref="VisibleKind"/> = true）无影响——Boss 常显，本字段不递减。</summary>
        public void OnHit()
        {
            if (VisibleKind) return;
            _remaining = VisibleDuration;
        }

        /// <summary>每帧递减。返回当前是否仍可见（常显 Boss 永远 true）。</summary>
        public bool Tick(float dt)
        {
            if (VisibleKind) return true;
            _remaining -= dt;
            if (_remaining < 0f) _remaining = 0f;
            return _remaining > 0f;
        }

        /// <summary>当前 alpha（0..1）：可见且未到淡出窗口 = 1，淡出窗口内线性降到 0，
        /// 不可见 = 0。Boss 永远 1。</summary>
        public float Alpha
        {
            get
            {
                if (VisibleKind) return 1f;
                if (_remaining <= 0f) return 0f;
                if (_remaining >= FadeOutDuration) return 1f;
                return _remaining / FadeOutDuration;
            }
        }

        /// <summary>当前血量填充比（0..1），由调用方传入——timer 不知道 mob 的血，只负责显隐。</summary>
        public static float FillRatio(float currentHealth, float maxHealth)
            => maxHealth <= 0f ? 0f : (currentHealth / maxHealth);
    }
}