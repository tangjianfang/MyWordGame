using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// F5 切换第一人称 / 第三人称相机。
    /// 简化：第三人称相机放在玩家身后 3 格 + 上 1.5 格，看玩家。
    /// </summary>
    public sealed class CameraThirdPerson : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.F5;
        public float Distance = 3f;
        public float Height = 1.5f;

        private Camera _firstPersonCam;
        private Camera _thirdPersonCam;
        private PlayerController _player;
        private bool _thirdPerson;

        private void Awake()
        {
            // 找到玩家身上的 FirstPersonCamera 与新建 ThirdPersonCamera
            _player = GetComponent<PlayerController>();
            foreach (var cam in GetComponentsInChildren<Camera>(true))
            {
                if (cam.gameObject.name.IndexOf("Third", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    _thirdPersonCam = cam;
                else
                    _firstPersonCam = cam;
            }
            if (_thirdPersonCam == null)
            {
                var go = new GameObject("ThirdPersonCamera");
                go.transform.SetParent(transform, false);
                _thirdPersonCam = go.AddComponent<Camera>();
                _thirdPersonCam.fieldOfView = 70f;
                _thirdPersonCam.nearClipPlane = 0.05f;
                _thirdPersonCam.farClipPlane = 500f;
                _thirdPersonCam.enabled = false;
                _thirdPersonCam.tag = "Untagged";   // 避免替代主相机
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                _thirdPerson = !_thirdPerson;
                if (_firstPersonCam != null) _firstPersonCam.enabled = !_thirdPerson;
                if (_thirdPersonCam != null) _thirdPersonCam.enabled = _thirdPerson;
            }

            if (_thirdPerson && _thirdPersonCam != null && _player != null && _player.Eye != null)
            {
                var eye = _player.Eye;
                var pos = eye.position + (-eye.forward * Distance) + (Vector3.up * (Height - eye.localPosition.y));
                _thirdPersonCam.transform.position = pos;
                _thirdPersonCam.transform.LookAt(eye.position + eye.forward * 2f);
            }
        }
    }
}