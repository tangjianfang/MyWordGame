namespace MyWorld.Unity.Audio
{
    /// <summary>
    /// 音乐音量总线（av W1-7）：SettingsPanelUi 写、BgmAudioSystem / AmbientAudioSystem
    /// 读。只管 BGM + 环境两类 AudioSource；音效（SFX / 事件音 / 打击 / 走路）仍走
    /// AudioListener 全局音量。
    /// <para>
    /// 静态值省 FindObjectOfType —— BGM/环境 Tick 在 Update 里每帧读，避免拿不到引用
    /// 就静默哑音。设置面板 Awake 时把 PlayerPrefs 的归一值写进来，后续滑动实时刷新。
    /// </para>
    /// </summary>
    public static class MusicVolumeBus
    {
        public static float Volume = 0.8f;
    }
}