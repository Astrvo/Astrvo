using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using Astrvo.Space;

/// <summary>
/// 玩家移动控制器 - 使用新输入系统
/// 只有IsOwner才能控制移动
/// 参考ThirdPersonMovement的实现
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float rotationSmoothTime = 0.1f;
    
    [Header("重力设置")]
    [SerializeField] private float gravity = -18f; // 重力值，可调节（设为0可禁用重力）
    
    [Header("地面检测设置")]
    [SerializeField] private float groundedOffset = -0.22f; // 参考GroundCheck
    [SerializeField] private float groundRadius = 0.28f; // 参考GroundCheck
    [SerializeField] private LayerMask groundMask = -1; // 地面层遮罩
    
    [Header("组件引用")]
    public Camera playerCamera;
    [SerializeField] private Transform cameraTarget; // 相机目标（用于计算移动方向）
    [SerializeField] private VariableJoystick variableJoystick; // 虚拟摇杆（移动端输入）
    
    private CharacterController controller;
    private UnityEngine.InputSystem.PlayerInput playerInput;
    private Vector2 currentMovementInput;
    private float targetRotation = 0.0f; // 目标旋转角度
    private float rotationVelocity;
    private bool isRunning;
    private float verticalVelocity = 0f; // 垂直速度
    
    // 移动速度属性，供动画控制器使用
    public float CurrentMoveSpeed { get; private set; }
    public bool IsMoving => currentMovementInput.magnitude > 0.1f;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 只有Owner才能启用输入
        if (IsOwner)
        {
            controller = GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<CharacterController>();
            }
            
            playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = true;
            }
            else
            {
                Debug.LogWarning("[PlayerMovement] PlayerInput component not found!");
            }
            
            // 如果没有设置相机目标，尝试从NetworkCameraController获取
            if (cameraTarget == null)
            {
                NetworkCameraController cameraController = FindObjectOfType<NetworkCameraController>();
                if (cameraController != null && cameraController.IsOwner)
                {
                    cameraTarget = cameraController.transform;
                }
                else
                {
                    // 如果没有找到NetworkCameraController，使用主相机
                    Camera mainCam = Camera.main;
                    if (mainCam != null)
                    {
                        cameraTarget = mainCam.transform;
                    }
                }
            }
            
            // 如果没有设置虚拟摇杆，尝试自动查找
            if (variableJoystick == null)
            {
                variableJoystick = FindObjectOfType<VariableJoystick>();
                if (variableJoystick != null)
                {
                    Debug.Log("[PlayerMovement] Auto-found VariableJoystick: " + variableJoystick.name);
                }
            }
        }
        else
        {
            // 非Owner禁用输入组件
            playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }
        }
    }

    public void OnMove(InputValue value)
    {
        if (!IsOwner) return;
        currentMovementInput = value.Get<Vector2>();
    }
    
    /// <summary>
    /// 获取合并后的移动输入（键盘 + 虚拟摇杆）
    /// </summary>
    private Vector2 GetCombinedMovementInput()
    {
        Vector2 input = currentMovementInput;
        
        // 如果存在虚拟摇杆，优先使用摇杆输入
        if (variableJoystick != null)
        {
            Vector2 joystickInput = new Vector2(variableJoystick.Horizontal, variableJoystick.Vertical);
            
            // 如果摇杆有输入（超过死区），使用摇杆输入
            if (joystickInput.magnitude > 0.01f)
            {
                input = joystickInput;
            }
            // 如果摇杆没有输入但键盘有输入，使用键盘输入
            // 这样键盘和摇杆可以无缝切换
        }
        
        return input;
    }
    
    public void OnSprint(InputValue value)
    {
        if (!IsOwner) return;
        isRunning = value.isPressed;
    }

    void Update()
    {
        // 只有Owner才能控制移动
        if (!IsOwner)
            return;
            
        if (controller == null)
            return;

        HandleMovement();
    }
    
    private void HandleMovement()
    {
        // 获取合并后的输入（键盘 + 虚拟摇杆）
        Vector2 combinedInput = GetCombinedMovementInput();
        
        // 参考Unity官方Starter Assets ThirdPersonController的实现
        // 设置目标速度
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        
        // 如果没有输入，目标速度为0
        if (combinedInput == Vector2.zero) targetSpeed = 0.0f;
        
        // 处理重力
        HandleGravity();
        
        // 归一化输入方向
        Vector3 inputDirection = new Vector3(combinedInput.x, 0.0f, combinedInput.y).normalized;
        
        // 如果有移动输入，旋转角色
        if (combinedInput != Vector2.zero)
        {
            // 计算目标旋转角度：输入方向角度 + 相机Y轴角度
            // 注意：这里直接使用cameraTarget.eulerAngles.y，因为我们已经确保了相机与玩家分离
            float cameraYaw = cameraTarget != null ? cameraTarget.eulerAngles.y : transform.eulerAngles.y;
            
            
            targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  playerCamera.transform.eulerAngles.y;
            // 平滑旋转
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, rotationSmoothTime);
            
            // 旋转角色朝向输入方向（相对于相机）
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }
        
        // 计算移动方向：基于目标旋转角度的前方
        Vector3 targetDirection = Quaternion.Euler(0.0f, targetRotation, 0.0f) * Vector3.forward;
        
        // 移动角色
        controller.Move(targetDirection.normalized * (targetSpeed * Time.deltaTime) +
                       new Vector3(0.0f, verticalVelocity, 0.0f) * Time.deltaTime);
        
        // 更新移动速度（供动画使用）
        float moveMagnitude = combinedInput.magnitude;
        CurrentMoveSpeed = isRunning ? runSpeed * moveMagnitude : walkSpeed * moveMagnitude;
    }
    
    /// <summary>
    /// 处理重力 - 参考ThirdPersonMovement.JumpAndGravity
    /// </summary>
    private void HandleGravity()
    {
        // 使用GroundCheck的方式检测地面（参考GroundCheck.IsGrounded）
        bool isGrounded = IsGrounded();
        
        // 参考ThirdPersonMovement：如果在地面上且垂直速度向下，重置为小的负值
        if (isGrounded && verticalVelocity < 0)
        {
            // 如果gravity为0，则verticalVelocity也设为0
            if (gravity == 0f)
            {
                verticalVelocity = 0f;
            }
            else
            {
                // 参考ThirdPersonMovement，在地面上时设置为-2f
                verticalVelocity = -2f;
            }
        }
        
        // 应用重力（参考ThirdPersonMovement：verticalVelocity += gravity * Time.deltaTime）
        // 如果gravity为0，则不应用重力
        if (gravity != 0f)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }
    
    /// <summary>
    /// 检查是否在地面上 - 参考GroundCheck.IsGrounded
    /// </summary>
    private bool IsGrounded()
    {
        // 如果垂直速度向上，肯定不在地面（参考ThirdPersonMovement.IsGrounded）
        if (verticalVelocity > 0)
        {
            return false;
        }
        
        // 使用GroundCheck的方式：Physics.CheckSphere
        // 但需要排除玩家对象，避免多个玩家挤在一起时误判
        Vector3 position = transform.position;
        Vector3 spherePosition = new Vector3(position.x, position.y + groundedOffset, position.z);
        
        // 使用OverlapSphere然后过滤掉玩家对象
        Collider[] colliders = Physics.OverlapSphere(spherePosition, groundRadius, groundMask, QueryTriggerInteraction.Ignore);
        
        // 过滤掉玩家对象（通过Tag判断）
        foreach (Collider col in colliders)
        {
            // 跳过玩家对象（通过Tag判断）
            if (col.CompareTag("Player"))
            {
                continue;
            }
            
            // 如果检测到非玩家的Collider，说明在地面上
            return true;
        }
        
        return false;
    }
}