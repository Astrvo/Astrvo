using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        public bool IsHoldingLeftShift { get; private set; }

#if ENABLE_INPUT_SYSTEM
        private UnityEngine.InputSystem.PlayerInput _playerInputComponent;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
#endif

        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM
            _playerInputComponent = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (_playerInputComponent != null)
            {
                _moveAction = _playerInputComponent.actions["Move"];
                _lookAction = _playerInputComponent.actions["Look"];
                _jumpAction = _playerInputComponent.actions["Jump"];
                _sprintAction = _playerInputComponent.actions["Sprint"];
            }
#endif
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null)
            {
                _jumpAction.performed += OnJumpPerformed;
            }
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            if (_jumpAction != null)
            {
                _jumpAction.performed -= OnJumpPerformed;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnJumpPerformed(InputAction.CallbackContext ctx)
        {
            OnJumpPress?.Invoke();
        }
#endif

// ... (previous code)

#if ENABLE_INPUT_SYSTEM
        // Support for "Send Messages" behavior (like Network/PlayerMovement.cs)
        public void OnMove(InputValue value)
        {
            var moveInput = value.Get<Vector2>();
            Debug.Log($"[PlayerInput] OnMove called! Input: {moveInput}");
            AxisHorizontal = moveInput.x;
            AxisVertical = moveInput.y;
        }

        public void OnLook(InputValue value)
        {
            var lookInput = value.Get<Vector2>();
            MouseAxisX = lookInput.x * mouseSensitivityX;
            MouseAxisY = lookInput.y * mouseSensitivityY;
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                OnJumpPress?.Invoke();
            }
        }

        public void OnSprint(InputValue value)
        {
            IsHoldingLeftShift = value.isPressed;
        }
#endif

        public void CheckInput()
        {
            bool inputFound = false;

#if ENABLE_INPUT_SYSTEM
            if (_playerInputComponent != null)
            {
                inputFound = true;
                // If using polling (Actions)
                if (_moveAction != null)
                {
                    var moveInput = _moveAction.ReadValue<Vector2>();
                    AxisHorizontal = moveInput.x;
                    AxisVertical = moveInput.y;
                }
                
                // Note: If "Send Messages" is used, the OnMove etc methods above 
                // will have already populated value, so we don't strictly need to do anything here
                // if the actions are null. But we keep this for hybrid support.
            }
#endif
// ... (rest of method)

            if (!inputFound)
            {
                // 获取键盘输入 (Legacy Fallback)
                AxisHorizontal = Input.GetAxis(HORIZONTAL_AXIS);
                AxisVertical = Input.GetAxis(VERTICAL_AXIS);
                MouseAxisX = Input.GetAxis(MOUSE_AXIS_X) * mouseSensitivityX;
                MouseAxisY = Input.GetAxis(MOUSE_AXIS_Y) * mouseSensitivityY;
                IsHoldingLeftShift = Input.GetKey(KeyCode.LeftShift);

                if (Input.GetButtonDown(JUMP_BUTTON))
                {
                    OnJumpPress?.Invoke();
                }
            }

            // 获取 Joystick 输入
            if (variableJoystick != null)
            {
                AxisHorizontal += variableJoystick.Horizontal;
                AxisVertical += variableJoystick.Vertical;
            }
        }
    }
}
