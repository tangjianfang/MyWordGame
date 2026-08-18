namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 难度系统全局开关（m13 W2）：仿 PeaceMode 静态开关模式，纯 Core 静态 bool 真源。
    /// <para>
    /// 玩法侧一律只读 <see cref="Enabled"/>；写入只经 <see cref="SetEnabled"/>
    /// （设置面板开关），避免各处直写静态字段造成读法漂移。缓存让
    /// <see cref="MobAI.TakeHit"/> 这类高频路径不必每帧重算。
    /// </para>
    /// <para>
    /// <b>本类刻意不放 PlayerPrefs</b>——Core 层不允许 UnityEngine 引用
    ///（<c>MyWorld.Core.asmdef</c> 设了 <c>noEngineReferences</c>）。
    /// Unity 侧的持久化见 <c>MyWorld.Unity.Gameplay.DifficultyModeBridge</c>：
    /// <c>WorldBootstrap.Awake</c> 一次性从 PlayerPrefs 把「宝宝模式」灌入本类，
    /// <c>SettingsPanelUi</c> 写时同时调 PlayerPrefs 与本类（顺序：先盘后缓存，
    /// 保证崩在任意一步都有合理回退）。
    /// </para>
    /// <para>
    /// 取舍：为什么不在 Core 引用 UnityEngine？
    /// ① Core dotnet 测试不挂 PlayerPrefs（dotnet 链根本不解析 UnityEngine 程序集），
    ///   把开关放 Core 才能让 <see cref="MobAITests"/> 一行调 <c>SetEnabled(true)</c>
    ///   就能验证宝宝一击必杀，不必走 Unity 侧反射；
    /// ② UnityEngine 静态字段随域卸载会清，但 PlayerPrefs 在域之间持久——放 Core
    ///   可避免被 Unity 域卸载搅乱。
    /// </para>
    /// </summary>
    public static class DifficultyMode
    {
        /// <summary>宝宝模式：true = 所有 mob（玩家造成伤害路径统一在 <see cref="MobAI.TakeHit"/>
        /// 入口处一次性把 damage 改成 mob 当前 MaxHealth）一击必杀，false = 现值不变。
        /// 默认 false（普通），与 m11 W3-5 PeaceMode 同款「未存键 = 默认关」的契约一致——
        /// 既有玩家不该被静默切换难度。</summary>
        private static bool _enabled;

        /// <summary>开关当前状态。默认 false（普通），与 PeaceMode 同款语义。</summary>
        public static bool Enabled => _enabled;

        /// <summary>写开关（设置面板调用）：即时生效。
        /// 与 PeaceMode.SetEnabled 同款语义，但此处只更缓存、不落盘——
        /// 持久化由 <see cref="MyWorld.Unity.Gameplay.DifficultyModeBridge.SaveToPrefs"/> 负责。</summary>
        public static void SetEnabled(bool value) => _enabled = value;

        /// <summary>丢弃缓存，强制下次 <see cref="Enabled"/> 默认返回 false（默认普通）。
        /// 仅测试用（SetUp/TearDown 让后续用例不读到上一个用例留下的真值）。</summary>
        public static void ResetCache() => _enabled = false;
    }
}