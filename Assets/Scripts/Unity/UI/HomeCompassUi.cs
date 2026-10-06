using MyWorld.Core.Math;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>回家罗盘 HUD（评审 08 F4）：屏幕上方居中一行「家 128m ↖」——床的重生点
    /// 方向与距离；无床显示「无家」提示。孩子 30-60 分钟一局、LoadRadius=6 的小世界里
    /// 迷路 = 挫败 = 提前退出（用户存档画像：床 0 张、死亡瞬间退出）。v1 不做罗盘物品，
    /// HUD 常驻——「有家可回」这件事本身就该一直看得见。
    /// <para>方位约定与世界一致：+Z 为北（模型「面朝 +Z」同款），北 = ↑。</para>
    /// <para>绘制在屏幕上方居中（QuestHudUi 占右上、血条/经验条在底部，互不遮挡；
    /// 主菜单遮罩可见时不画——标题画面没有「回家」语境）。</para>
    /// </summary>
    public sealed class HomeCompassUi : MonoBehaviour
    {
        private void OnGUI()
        {
            if (TitleScreenUi.OverlayVisible) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.BedSystem == null) return;

            var player = new Vector2(transform.position.x, transform.position.z);
            string line = FormatLine(ctx.BedSystem.RespawnPoint, player);
            // 顶中一行：420 宽足够放下「家 999m ↖」，白字带描边样式与其它 HUD 一致
            GUI.Label(new Rect((Screen.width - 420f) / 2f, 8f, 420f, 22f),
                line, ItemSlotDrawer.WhiteStyle());
        }

        /// <summary>格式化一行（internal 供 EditMode 断言）：无床 / 到家 / 有床三态。</summary>
        internal static string FormatLine(Float3? bed, Vector2 playerPos)
        {
            if (bed == null)
            {
                return "无家——放一张床睡一觉，这里就会指向床";
            }

            float dx = bed.Value.X - playerPos.x;
            float dz = bed.Value.Z - playerPos.y;
            int distance = Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dz * dz));
            if (distance <= 2) return "到家了";

            return $"家 {distance}m {HeadingArrow(dx, dz)}";
        }

        /// <summary>8 向箭头：Atan2(dx, dz) 以北为 0° 顺时针（东 90°），45° 一档。
        /// 数组序 = (Round(angle/45) mod 8 + 8) mod 8：0↑ 1↗ 2→ 3↘ 4↓ 5↙ 6← 7↖。</summary>
        internal static string HeadingArrow(float dx, float dz)
        {
            if (Mathf.Abs(dx) < 0.0001f && Mathf.Abs(dz) < 0.0001f) return "·";
            float angle = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg; // 0=北, 90=东, ±180=南
            int index = ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
            string[] arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };
            return arrows[index];
        }
    }
}
