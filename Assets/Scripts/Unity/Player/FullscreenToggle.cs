using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>F11 切换全屏。启动时读 PlayerPrefs 还原上次设置，默认全屏。</summary>
    public sealed class FullscreenToggle : MonoBehaviour
    {
        private const string Key = "myword.fullscreen";

        private void Awake()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                bool wantFull = PlayerPrefs.GetInt(Key) == 1;
                Screen.fullScreen = wantFull;
            }
            else
            {
                Screen.fullScreen = true;
                PlayerPrefs.SetInt(Key, 1);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                Screen.fullScreen = !Screen.fullScreen;
                PlayerPrefs.SetInt(Key, Screen.fullScreen ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
