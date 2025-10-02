using System;
using UnityEngine;

namespace Astrvo.Space
{
    public class PlayerInput : MonoBehaviour
    {
        private const string HORIZONTAL_AXIS = "Horizontal";
        private const string VERTICAL_AXIS = "Vertical";
        private const string MOUSE_AXIS_X = "Mouse X";
        private const string MOUSE_AXIS_Y = "Mouse Y";
        private const string JUMP_BUTTON = "Jump";

        public Action OnJumpPress;
        public float AxisHorizontal { get; private set; }
        public float AxisVertical { get; private set; }
        public float MouseAxisX { get; private set; }
        public float MouseAxisY { get; private set; }

        [SerializeField][Tooltip("Defines the mouse sensitivity on the X axis (left and right)")]
        private float mouseSensitivityX = 1;
        [SerializeField][Tooltip("Defines the mouse sensitivity on the Y axis (up and down)")]
        private float mouseSensitivityY = 2;

        [SerializeField] private VariableJoystick variableJoystick; // 引用 VariableJoystick

        public bool IsHoldingLeftShift => Input.GetKey(KeyCode.LeftShift);

        public void CheckInput()
        {
            // 获取键盘输入
            AxisHorizontal = Input.GetAxis(HORIZONTAL_AXIS);
            AxisVertical = Input.GetAxis(VERTICAL_AXIS);
            MouseAxisX = Input.GetAxis(MOUSE_AXIS_X) * mouseSensitivityX;
            MouseAxisY = Input.GetAxis(MOUSE_AXIS_Y) * mouseSensitivityY;

            // 获取 Joystick 输入
            if (variableJoystick != null)
            {
                AxisHorizontal += variableJoystick.Horizontal;
                AxisVertical += variableJoystick.Vertical;
            }

            if (Input.GetButtonDown(JUMP_BUTTON))
            {
                OnJumpPress?.Invoke();
            }
        }
    }
}
