using UnityEngine;

namespace Astrvo.Space
{
    public class CameraOrbit : MonoBehaviour
    {
        private const float SMOOTH_TIME = 0.1f;
        
        [SerializeField][Tooltip("PlayerInput component is required to listen for input")]
        private PlayerInput playerInput;
        [SerializeField][Tooltip("Used to set lower limit of camera rotation clamping")]
        private float minRotationX = -60f;
        [SerializeField][Tooltip("Used to set upper limit of camera rotation clamping")]
        private float maxRotationX = 50f;

        [SerializeField][Tooltip("Useful to apply smoothing to mouse input")]
        private bool smoothDamp = false;
        
        private Vector3 rotation;
        private Vector3 currentVelocity;

        private float pitch;
        private float yaw;

        private bool isRotating; // 用于检测是否在旋转状态

        private void Start()
        {
            rotation = transform.transform.eulerAngles;
        }

        private void LateUpdate()
        {
            if (playerInput == null) return;
            // 检查鼠标右键按住或触摸是否在移动
            isRotating = Input.GetMouseButton(1) || Input.touchCount > 0;

            if (isRotating)
            {
                // 只有在旋转状态时才更新 yaw 和 pitch
                yaw += playerInput.MouseAxisX;
                pitch -= playerInput.MouseAxisY;

                if (smoothDamp)
                {
                    rotation = Vector3.SmoothDamp(rotation, new Vector3(pitch, yaw), ref currentVelocity, SMOOTH_TIME);
                }
                else
                {
                    rotation = new Vector3(pitch, yaw, rotation.z);
                }

                // 限制摄像头旋转角度
                rotation.x = ClampAngle(rotation.x, minRotationX, maxRotationX);
                transform.rotation = Quaternion.Euler(rotation);
            }
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360F)
            {
                angle += 360F;
            }
            if (angle > 360F)
            {
                angle -= 360F;
            }
            return Mathf.Clamp(angle, min, max);
        }
    }
}
