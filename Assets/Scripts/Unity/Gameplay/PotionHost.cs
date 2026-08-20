using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 药水视觉宿主（m12 W3）——夜视 buff 的画面提亮：buff 激活期间把
    /// <c>RenderSettings.ambientLight</c> 抬到下限（Linear 色彩空间下的全场景温和提亮），
    /// 过期恢复原值。其余 buff（速度/力量/跳跃/血上限）都是数值消费，不需要宿主——
    /// PlayerController / CombatController / PlayerContext 直接读
    /// <see cref="Core.Buffs.PotionSystem"/>。挂 WorldBootstrap（PlayerContext 之后）。
    /// </summary>
    public sealed class PotionHost : MonoBehaviour
    {
        private Color _originalAmbient;
        private bool _lifted;

        private const float NightVisionLift = 0.55f;

        private void LateUpdate()
        {
            var ctx = PlayerContext.Instance;
            var potions = ctx != null ? ctx.Potions : null;
            bool want = potions != null
                && potions.Active(Core.Buffs.BuffKind.NightVision, Time.time);

            if (want && !_lifted)
            {
                _originalAmbient = RenderSettings.ambientLight;
                var c = _originalAmbient;
                c.r = Mathf.Max(c.r, NightVisionLift);
                c.g = Mathf.Max(c.g, NightVisionLift);
                c.b = Mathf.Max(c.b, NightVisionLift);
                RenderSettings.ambientLight = c;
                _lifted = true;
            }
            else if (!want && _lifted)
            {
                RenderSettings.ambientLight = _originalAmbient;
                _lifted = false;
            }
        }

        private void OnDisable()
        {
            if (_lifted)
            {
                RenderSettings.ambientLight = _originalAmbient;
                _lifted = false;
            }
        }
    }
}
