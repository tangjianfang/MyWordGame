using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 和平模式全局开关（m11 W3-5）：PlayerPrefs 持久化 + 静态缓存。
    /// 开 = 敌对生物不再刷新（<see cref="MyWorld.Unity.Combat.MobManager.TickSpawn"/> 门，
    /// 已在场的不删）+ 死亡不掉落（当前所有模式本就不清背包，见
    /// <see cref="MyWorld.Unity.Player.PlayerController.TakeDamage"/> 死亡分支的契约注释）。
    /// <para>
    /// 玩法侧一律只读 <see cref="Enabled"/>；写入只经 <see cref="SetEnabled"/>
    /// （设置面板开关），避免各处直读 PlayerPrefs 造成读法漂移。缓存让
    /// TickSpawn 这类高频路径不必每帧查 PlayerPrefs。
    /// </para>
    /// </summary>
    public static class PeaceMode
    {
        /// <summary>PlayerPrefs 键名（任务卡 W3-5 指定；键名是全局硬约束，改名旧档读不回）。</summary>
        public const string Key = "PeacefulMode";

        /// <summary>三值缓存：null = 还没读过盘。运行期只有 <see cref="SetEnabled"/> 与
        /// <see cref="ResetCache"/> 会改它。</summary>
        private static bool? _cached;

        /// <summary>开关当前状态。首次访问读 PlayerPrefs（未存键 = 默认关），之后走缓存。</summary>
        public static bool Enabled => _cached ??= Load();

        /// <summary>从 PlayerPrefs 读原始值（未存键 = 默认关，与三滑条的「默认值兜底」同款）。</summary>
        public static bool Load() => PlayerPrefs.GetInt(Key, 0) == 1;

        /// <summary>写开关（设置面板调用）：落 PlayerPrefs 并同步缓存，即时生效。
        /// 与三滑条先例一致不显式 <c>PlayerPrefs.Save()</c>（退出时统一落盘）。</summary>
        public static void SetEnabled(bool value)
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            _cached = value;
        }

        /// <summary>丢弃缓存，强制下次 <see cref="Enabled"/> 重读 PlayerPrefs。
        /// 仅测试用（SetUp/TearDown 清键后让缓存失效）。</summary>
        public static void ResetCache() => _cached = null;
    }
}
