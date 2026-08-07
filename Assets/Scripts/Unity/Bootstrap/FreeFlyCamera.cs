using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 里程碑 1 的验收工具：WASD 平移、QE 升降、按住右键转视角、Shift 加速。
    /// 玩家控制器是里程碑 2 的内容，这个组件到时候会被替换掉。
    /// </summary>
    public sealed class FreeFlyCamera : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 12f;
        [SerializeField] private float sprintMultiplier = 4f;
        [SerializeField] private float lookSensitivity = 2.5f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            float right = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float up = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            // 升降走世界的上方向而不是相机的上方向，低头飞行时手感才不会打架
            Vector3 direction = transform.right * right + Vector3.up * up + transform.forward * forward;
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

            transform.position += direction * (speed * Time.deltaTime);
        }
    }
}
