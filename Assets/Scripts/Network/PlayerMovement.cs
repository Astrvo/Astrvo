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
        
        // 处理输入：如果只有侧向输入（A/D），自动添加前向分量，实现类似A+W的效果
        Vector2 processedInput = combinedInput;
        if (Mathf.Abs(processedInput.y) < 0.1f && Mathf.Abs(processedInput.x) > 0.1f)
        {
            // 只有侧向输入时，添加前向分量（0.707约为45度角，使移动更自然）
            processedInput = new Vector2(processedInput.x, 0.707f);
        }
        
        // 计算移动方向（基于相机方向）- 直接使用相机相对方向，避免转圈
        Vector3 moveDirection = Vector3.zero;
        if (cameraTarget != null && processedInput != Vector2.zero)
        {
            // 获取相机的水平方向（移除Y轴分量）
            Vector3 cameraForward = cameraTarget.forward;
            Vector3 cameraRight = cameraTarget.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            // 计算移动方向：直接使用相机相对方向，立即移动
            moveDirection = cameraRight * processedInput.x + cameraForward * processedInput.y;
        }
        else if (processedInput != Vector2.zero)
        {
            // 如果没有相机，使用世界坐标
            moveDirection = new Vector3(processedInput.x, 0f, processedInput.y);
        }
        
        // 如果有移动输入，旋转角色朝向移动方向
        // 使用输入方向 + 相机角度来计算目标旋转，确保角度稳定
        if (processedInput != Vector2.zero && cameraTarget != null)
        {
            // 归一化输入方向（世界坐标的x, z）
            Vector3 inputDirection = new Vector3(processedInput.x, 0.0f, processedInput.y).normalized;
            
            // 计算目标旋转角度：输入方向的角度 + 相机的Y轴角度
            // 这样可以让角色朝向相对于相机的输入方向，且角度更稳定
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                cameraTarget.eulerAngles.y;
            
            // 平滑旋转到目标角度
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity,
                rotationSmoothTime);
            
            // 旋转角色朝向输入方向（相对于相机）
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }
        else if (processedInput != Vector2.zero)
        {
            // 如果没有相机，使用世界坐标
            Vector3 inputDirection = new Vector3(processedInput.x, 0.0f, processedInput.y).normalized;
            float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity,
                rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
        }
        
        // 移动角色：直接沿着计算出的移动方向移动（不依赖旋转）
        // 这样可以避免转圈问题，角色会立即移动，同时平滑旋转
        if (moveDirection.magnitude > 0.1f)
        {
            controller.Move(moveDirection.normalized * (targetSpeed * Time.deltaTime) +
                           new Vector3(0.0f, verticalVelocity, 0.0f) * Time.deltaTime);
        }
        else
        {
            // 没有移动输入时，只应用重力
            controller.Move(new Vector3(0.0f, verticalVelocity, 0.0f) * Time.deltaTime);
        }
        
        // 更新移动速度（供动画使用）
        float moveMagnitude = processedInput.magnitude;
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