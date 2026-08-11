using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.World
{
    /// <summary>
    /// 每帧推 TimeOfDay，根据 phase 调方向光颜色 / 强度 / ambient。
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        public Light SunLight;
        public float DayAmbientIntensity = 0.7f;
        public float NightAmbientIntensity = 0.1f;

        private void Update()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            ctx.Time.Advance(Time.deltaTime);
            if (SunLight == null) return;
            int phase = ctx.Time.Phase;
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
                    ambient = new Color(0.7f, 0.7f, 0.7f);
                    lightColor = Color.white;
                    lightIntensity = 1f;
                    break;
                case 2: // dusk
                    ambient = new Color(0.7f, 0.55f, 0.5f);
                    lightColor = new Color(1f, 0.7f, 0.5f);
                    lightIntensity = 0.7f;
                    break;
                default: // night
                    ambient = new Color(0.15f, 0.18f, 0.3f);
                    lightColor = new Color(0.4f, 0.5f, 0.8f);
                    lightIntensity = 0.2f;
                    break;
            }
            RenderSettings.ambientLight = ambient;
            SunLight.color = lightColor;
            SunLight.intensity = lightIntensity;
        }
    }
}
