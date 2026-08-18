using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 难度系统的 PlayerPrefs 持久化层（m13 W2）。
    /// <para>
    /// 设计取舍：真源是 <see cref="MyWorld.Core.Entities.DifficultyMode"/>（Core 纯静态
    /// bool），本类只负责「盘 ↔ Core 缓存」的单向镜像。原因：Core 层禁引 UnityEngine，
    /// PlayerPrefs 是 Unity 设施，dotnet 测试链根本不存在 PlayerPrefs；把 Core 侧的开关
    /// 与 PlayerPrefs 解耦，让 <c>MobAITests</c> 能直接 <c>DifficultyMode.SetEnabled(true)</c>
    /// 验证宝宝一击必杀，不必反射 Unity 侧类型。
    /// </para>
    /// <para>
    /// 镜像三处调用方：
    /// ① <c>WorldBootstrap.Awake</c> 启动时 <see cref="LoadAndApply"/> 把 PlayerPrefs 值灌进 Core
    /// ② <c>SettingsPanelUi.DrawPanel</c> 用户点 toggle 时 <see cref="Apply"/> 写盘 + 灌 Core
    /// ③ 测试 <see cref="LoadAndApply"/> / <see cref="Apply"/> 守键名与往返
    /// </para>
    /// </summary>
    public static class DifficultyModeBridge
    {
        /// <summary>PlayerPrefs 键名（任务卡 W2 指定；键名是全局硬约束，改名旧档读不回）。
        /// 命名风格沿用 m11 W3-5 PeaceMode.Key = "PeacefulMode"——首字母大写单词，无版本号。
        /// 孩子在宝宝模式下任何存档的难度偏好都要被这一把键吃下，独立于世界存档。</summary>
        public const string Key = "BabyMode";

        /// <summary>从 PlayerPrefs 读原值：未存键 = 默认 0（普通）。
        /// 与 PeaceMode.Load 同款「未存键 = 默认关」的契约——既有玩家不该被静默切难度。</summary>
        public static bool LoadFromPrefs() => PlayerPrefs.GetInt(Key, 0) == 1;

        /// <summary>写 PlayerPrefs + 同步 Core 缓存（顺序：先盘后缓存，保证崩在任意一步都有合理回退）。
        /// 与 PeaceMode.SetEnabled 同款语义：不显式 <c>PlayerPrefs.Save()</c>，退出时统一落盘。</summary>
        public static void Apply(bool value)
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            MyWorld.Core.Entities.DifficultyMode.SetEnabled(value);
        }

        /// <summary>启动一次性灌入：从 PlayerPrefs 读出当前值并灌进 Core。
        /// <c>WorldBootstrap.Awake</c> 调用，保证游戏运行期 Core 静态缓存与盘一致。</summary>
        public static void LoadAndApply() => Apply(LoadFromPrefs());
    }
}