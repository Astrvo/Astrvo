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
    
    private CharacterController controller;
    private UnityEngine.InputSystem.PlayerInput playerInput;
    private Vector2 currentMovementInput;
    private Vector3 moveDirection;
    private float currentSpeed;
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
        // 计算移动方向（基于相机方向）- 参考ThirdPersonMovement.Move
        if (cameraTarget != null)
        {
            // 参考ThirdPersonMovement：直接使用相机的right和forward向量
            Vector3 right = cameraTarget.right;
            Vector3 forward = cameraTarget.forward;
            
            // 移除Y轴分量，只保留水平方向（确保移动是水平的）
            right.y = 0f;
            forward.y = 0f;
            right.Normalize();
            forward.Normalize();
            
            // 参考ThirdPersonMovement：moveDirection = playerCamera.right * inputX + playerCamera.forward * inputY
            moveDirection = right * currentMovementInput.x + forward * currentMovementInput.y;
        }
        else
        {
            // 如果没有相机，使用世界坐标
            moveDirection = new Vector3(currentMovementInput.x, 0f, currentMovementInput.y);
        }
        
        // 计算速度 - 参考ThirdPersonMovement.Move
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        float moveMagnitude = moveDirection.magnitude;
        
        // 处理重力 - 参考ThirdPersonMovement.JumpAndGravity
        HandleGravity();
        
        // 移动角色 - 参考ThirdPersonMovement
        // moveDirection.normalized * (moveSpeed * Time.deltaTime) + new Vector3(0.0f, verticalVelocity * Time.deltaTime, 0.0f)
        Vector3 finalMove = moveDirection.normalized * (targetSpeed * Time.deltaTime);
        finalMove.y = verticalVelocity * Time.deltaTime;
        controller.Move(finalMove);
        
        // 更新移动速度（供动画使用）- 参考ThirdPersonMovement
        CurrentMoveSpeed = isRunning ? runSpeed * moveMagnitude : walkSpeed * moveMagnitude;
        
        // 参考ThirdPersonMovement：如果有移动输入，旋转人物朝向移动方向
        // w/s/a/d 所有方向都会旋转人物朝向移动方向
        if (moveMagnitude > 0.1f)
        {
            RotateTowardsMoveDirection();
        }
    }
    
    private void RotateTowardsMoveDirection()
    {
        if (moveDirection.magnitude < 0.1f) return;
        
        float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, angle, 0f);
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
        Vector3 position = transform.position;
        Vector3 spherePosition = new Vector3(position.x, position.y + groundedOffset, position.z);
        return Physics.CheckSphere(spherePosition, groundRadius, groundMask);
    }
}