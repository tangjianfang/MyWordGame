using MyWorld.Unity.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyWorld.Unity.Environment
{
    /// <summary>
    /// 每帧推 TimeOfDay，根据 phase 调方向光颜色 / 强度 / ambient。
    /// <para>
    /// m5 B2：ambient 写入改为 <see cref="AmbientMode.Flat"/>——此前场景 ambientMode=Skybox，
    /// <see cref="RenderSettings.ambientLight"/> 是 no-op，白天黑夜画面都不吃这份环境光，整体偏暗。
    /// 参数同步校准（Linear 色彩空间下的 spec 值，实机再微调）：
    /// 白天 sun 1.0→1.3、ambient 0.7 灰；夜晚 sun 0.2→0.35、ambient→(0.22, 0.25, 0.40)。
    /// </para>
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        // —— m5 B2 spec 校准常量（docs/superpowers/specs/2026-08-15-milestone-5-visual-polish-design.md §3）——
        // 公开常量：PreviewSceneBuilder 建场景时直接复用，EditMode 测试断言 spec 值不漂移。

        /// <summary>白天太阳强度。Linear 下 1.0 偏暗，提到 1.3（初值，实机再调）。</summary>
        public const float DaySunIntensity = 1.3f;

        /// <summary>夜晚太阳（月光）强度。0.2 时方块不可辨，提到 0.35。</summary>
        public const float NightSunIntensity = 0.35f;

        /// <summary>白天环境光：0.7 中性灰。</summary>
        public static readonly Color DayAmbient = new Color(0.7f, 0.7f, 0.7f);

        /// <summary>夜晚环境光：带蓝调的月光色，比旧值 (0.15,0.18,0.30) 整体抬高一档。</summary>
        public static readonly Color NightAmbient = new Color(0.22f, 0.25f, 0.40f);

        public Light SunLight;

        private void Awake()
        {
            // 双保险：PreviewSceneBuilder 建场景时已设 Flat，这里启动再设一次——
            // 场景直开 / 手搭场景忘了设也不至于退回 Skybox no-op。
            RenderSettings.ambientMode = AmbientMode.Flat;
        }

        private void Update()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            ctx.Time.Advance(Time.deltaTime);
            if (SunLight == null) return;
            Apply(ctx.Time.Phase);
        }

        /// <summary>
        /// 按 phase 写环境光与方向光。公开给 EditMode 测试直接驱动——
        /// 测试里没有 <see cref="PlayerContext"/> 单例，走不了 <see cref="Update"/> 路径。
        /// </summary>
        public void Apply(int phase)
        {
            // 每次驱动都设一次 Flat：ambientLight 只在 Flat / Trilight / Gradient 下生效，
            // Skybox 模式是 no-op（这正是 B2 之前「画面偏暗」的根因之一）。
            RenderSettings.ambientMode = AmbientMode.Flat;
            Color ambient;
            float lightIntensity;
            Color lightColor;
            switch (phase)
            {
                case 0: // dawn
                    ambient = new Color(0.7f, 0.6f, 0.55f);
                    lightColor = new Color(1f, 0.8f, 0.6f);
                    lightIntensity = 0.7f;
                    break;
                case 1: // day
                    ambient = DayAmbient;
                    lightColor = Color.white;
                    lightIntensity = DaySunIntensity;
                    break;
                case 2: // dusk
                    ambient = new Color(0.7f, 0.55f, 0.5f);
                    lightColor = new Color(1f, 0.7f, 0.5f);
                    lightIntensity = 0.7f;
                    break;
                default: // night
                    ambient = NightAmbient;
                    lightColor = new Color(0.4f, 0.5f, 0.8f);
                    lightIntensity = NightSunIntensity;
                    break;
            }
            RenderSettings.ambientLight = ambient;
            SunLight.color = lightColor;
            SunLight.intensity = lightIntensity;
        }
    }
}
