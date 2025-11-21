using FishNet.Object;
using UnityEngine;

/// <summary>
/// 网络玩家动画控制器 - 符合FishNet多人标准
/// 根据移动状态控制动画（idle/walk/run）
/// 参考ThirdPersonController的实现，支持ThirdPersonLoader加载的avatar
/// </summary>
public class NetworkPlayerAnimationController : NetworkBehaviour
{
    private const float FALL_TIMEOUT = 0.15f;
    
    // 动画参数Hash（与ThirdPersonController保持一致）
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int JumpHash = Animator.StringToHash("JumpTrigger");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    
    [Header("组件引用")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private CharacterController characterController;
    
    [Header("动画设置")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    
    [Header("地面检测设置（用于非Owner客户端）")]
    [SerializeField] private float groundedOffset = -0.22f; // 与PlayerMovement保持一致
    [SerializeField] private float groundRadius = 0.28f; // 与PlayerMovement保持一致
    [SerializeField] private LayerMask groundMask = -1; // 与PlayerMovement保持一致
    
    private Animator animator;
    private GameObject avatar;
    private float fallTimeoutDelta;
    private bool isInitialized = false;
    
    // 用于非Owner客户端计算移动速度
    private Vector3 lastPosition;
    private float calculatedMoveSpeed = 0f;
    private float moveSpeedSmoothVelocity = 0f;
    
    // 性能优化: 减少地面检测频率
    private float _groundCheckInterval = 0.15f; // 每0.15秒检测一次地面
    private float _lastGroundCheckTime = 0f;
    private bool _cachedIsGrounded = false;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 初始化组件引用
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
                playerMovement = GetComponentInParent<PlayerMovement>();
            }
        }
        
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = GetComponentInParent<CharacterController>();
            }
        }
        
        // 初始化位置跟踪（用于非Owner客户端计算速度）
        lastPosition = transform.position;
        calculatedMoveSpeed = 0f;
        moveSpeedSmoothVelocity = 0f;
        
        // 尝试监听NetworkThirdPersonLoader的加载完成事件
        TrySubscribeToLoader();
    }
    
    /// <summary>
    /// 尝试订阅NetworkThirdPersonLoader的加载完成事件
    /// </summary>
    private void TrySubscribeToLoader()
    {
        // 优先查找NetworkThirdPersonLoader（用于多人游戏）
        var networkLoader = GetComponent<NetworkThirdPersonLoader>();
        if (networkLoader == null)
        {
            networkLoader = GetComponentInParent<NetworkThirdPersonLoader>();
        }
        
        if (networkLoader != null)
        {
            networkLoader.OnLoadComplete += OnAvatarLoadComplete;
            Debug.Log("[NetworkPlayerAnimationController] Subscribed to NetworkThirdPersonLoader.OnLoadComplete");
            return;
        }
        
        // 如果没有找到NetworkThirdPersonLoader，尝试延迟查找
        StartCoroutine(DelayedLoaderSearch());
    }
    
    private System.Collections.IEnumerator DelayedLoaderSearch()
    {
        // 等待几帧，让其他组件初始化
        yield return new WaitForSeconds(0.1f);
        
        // 优先查找NetworkThirdPersonLoader
        var networkLoader = GetComponent<NetworkThirdPersonLoader>();
        if (networkLoader == null)
        {
            networkLoader = GetComponentInParent<NetworkThirdPersonLoader>();
        }
        
        if (networkLoader != null)
        {
            networkLoader.OnLoadComplete += OnAvatarLoadComplete;
            Debug.Log("[NetworkPlayerAnimationController] Subscribed to NetworkThirdPersonLoader.OnLoadComplete (delayed)");
        }
    }
    
    /// <summary>
    /// Avatar加载完成回调
    /// </summary>
    private void OnAvatarLoadComplete()
    {
        // 查找avatar对象（通常是ThirdPersonLoader的子对象）
        if (avatar == null)
        {
            // 尝试从子对象中找到avatar
            Transform avatarTransform = transform.Find("Avatar");
            if (avatarTransform == null)
            {
                // 查找所有子对象，找到有Animator的那个
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<Animator>() != null)
                    {
                        avatarTransform = child;
                        break;
                    }
                }
            }
            
            if (avatarTransform != null)
            {
                avatar = avatarTransform.gameObject;
                animator = avatar.GetComponent<Animator>();
                
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    isInitialized = true;
                    
                    // 初始化fallTimeoutDelta，避免立即进入掉落状态
                    fallTimeoutDelta = FALL_TIMEOUT;
                    
                    // 初始化位置跟踪（用于非Owner客户端计算速度）
                    lastPosition = transform.position;
                    calculatedMoveSpeed = 0f;
                    moveSpeedSmoothVelocity = 0f;
                    
                    // 立即检查一次地面状态并设置动画参数
                    bool isGrounded = IsGrounded();
                    animator.SetBool(IsGroundedHash, isGrounded);
                    animator.SetBool(FreeFallHash, !isGrounded);
                    
                    Debug.Log($"[NetworkPlayerAnimationController] Avatar loaded and initialized. Avatar: {avatar.name}, IsGrounded: {isGrounded}");
                }
                else
                {
                    Debug.LogWarning("[NetworkPlayerAnimationController] Avatar found but Animator component not found!");
                }
            }
        }
    }

    /// <summary>
    /// Setup方法 - 参考ThirdPersonController.Setup
    /// 由ThirdPersonLoader在加载完成后调用
    /// </summary>
    public void Setup(GameObject target, RuntimeAnimatorController runtimeAnimatorController)
    {
        if (target == null)
        {
            Debug.LogError("[NetworkPlayerAnimationController] Setup called with null target!");
            return;
        }
        
        avatar = target;
        
        // 确保avatar是激活的
        if (!avatar.activeSelf)
        {
            Debug.LogWarning("[NetworkPlayerAnimationController] Avatar is inactive, activating it...");
            avatar.SetActive(true);
        }
        
        animator = avatar.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[NetworkPlayerAnimationController] Animator component not found on avatar!");
            return;
        }
        
        if (runtimeAnimatorController != null)
        {
            animator.runtimeAnimatorController = runtimeAnimatorController;
        }
        else
        {
            Debug.LogWarning("[NetworkPlayerAnimationController] RuntimeAnimatorController is null, avatar may not animate properly");
        }
        
        animator.applyRootMotion = false;
        isInitialized = true;
        
        // 初始化fallTimeoutDelta，避免立即进入掉落状态
        fallTimeoutDelta = FALL_TIMEOUT;
        
        // 初始化位置跟踪（用于非Owner客户端计算速度）
        lastPosition = transform.position;
        calculatedMoveSpeed = 0f;
        moveSpeedSmoothVelocity = 0f;
        
        // 立即检查一次地面状态并设置动画参数
        bool isGrounded = IsGrounded();
        animator.SetBool(IsGroundedHash, isGrounded);
        animator.SetBool(FreeFallHash, !isGrounded);
        
        Debug.Log($"[NetworkPlayerAnimationController] Setup complete. Avatar: {avatar.name}, Animator: {(animator != null ? "Found" : "Missing")}, Controller: {(runtimeAnimatorController != null ? runtimeAnimatorController.name : "None")}, IsGrounded: {isGrounded}");
    }

    void Update()
    {
        // 只有初始化完成后才更新动画
        if (!isInitialized || animator == null || avatar == null)
            return;
        
        // 统一使用位移计算移动速度（不管是不是Owner）
        CalculateMoveSpeedFromPosition();
        
        UpdateAnimator();
    }
    
    /// <summary>
    /// 根据位置变化计算移动速度
    /// </summary>
    private void CalculateMoveSpeedFromPosition()
    {
        Vector3 currentPosition = transform.position;
        
        // 计算水平移动距离（忽略Y轴）
        Vector3 horizontalDelta = currentPosition - lastPosition;
        horizontalDelta.y = 0f; // 只计算水平移动
        
        // 计算速度（米/秒）
        // 防止除以0
        float deltaTime = Time.deltaTime;
        if (deltaTime > 0)
        {
            float speed = horizontalDelta.magnitude / deltaTime;
            
            // 平滑速度变化，避免抖动
            calculatedMoveSpeed = Mathf.SmoothDamp(calculatedMoveSpeed, speed, ref moveSpeedSmoothVelocity, animationSmoothTime);
        }
        
        // 更新上一帧位置
        lastPosition = currentPosition;
    }
    
    /// <summary>
    /// 更新动画器 - 参考ThirdPersonController.UpdateAnimator
    /// </summary>
    private void UpdateAnimator()
    {
        // 使用计算出的速度（基于位移）
        float targetMoveSpeed = calculatedMoveSpeed;
        
        // 设置移动速度参数
        // 优化：只有值变化超过阈值才设置，减少Animator调用开销
        if (Mathf.Abs(animator.GetFloat(MoveSpeedHash) - targetMoveSpeed) > 0.01f)
        {
            animator.SetFloat(MoveSpeedHash, targetMoveSpeed);
        }
        
        // 性能优化: 减少物理检测频率
        bool needGroundCheck = Time.time - _lastGroundCheckTime >= _groundCheckInterval;
        if (needGroundCheck)
        {
            _lastGroundCheckTime = Time.time;
            _cachedIsGrounded = IsGrounded();
        }
        
        // 处理地面检测和自由落体
        bool isGrounded = _cachedIsGrounded;
        
        // 优化：只有状态变化才设置
        if (animator.GetBool(IsGroundedHash) != isGrounded)
        {
            animator.SetBool(IsGroundedHash, isGrounded);
        }
        
        if (isGrounded)
        {
            // 在地面上
            fallTimeoutDelta = FALL_TIMEOUT;
            
            if (animator.GetBool(FreeFallHash))
            {
                animator.SetBool(FreeFallHash, false);
            }
        }
        else
        {
            // 在空中
            if (fallTimeoutDelta >= 0.0f)
            {
                fallTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                // 进入自由落体状态
                if (!animator.GetBool(FreeFallHash))
                {
                    animator.SetBool(FreeFallHash, true);
                }
            }
        }
    }
    
    /// <summary>
    /// 检查是否在地面上
    /// </summary>
    private bool IsGrounded()
    {
        // 对于Owner客户端，优先使用CharacterController.isGrounded（更准确且性能更好）
        if (IsOwner && characterController != null && characterController.enabled)
        {
            return characterController.isGrounded;
        }
        
        // 性能优化: 使用CheckSphere代替OverlapSphere（更高效，不需要分配数组）
        // 非Owner客户端或CharacterController不可用时，使用物理检测
        Vector3 position = transform.position;
        Vector3 spherePosition = new Vector3(position.x, position.y + groundedOffset, position.z);
        
        // 使用CheckSphere进行快速检测（比OverlapSphere快，不需要分配内存）
        // 注意: CheckSphere不能过滤玩家对象，但性能更好
        // 如果多个玩家挤在一起可能会有误判，但通常影响不大
        bool hitGround = Physics.CheckSphere(spherePosition, groundRadius, groundMask, QueryTriggerInteraction.Ignore);
        
        if (hitGround)
        {
            // 如果检测到碰撞，进一步检查是否是玩家对象（使用Raycast进行更精确的检测）
            // 性能优化: 只在检测到碰撞时才进行详细检查
            Collider[] colliders = Physics.OverlapSphere(spherePosition, groundRadius, groundMask, QueryTriggerInteraction.Ignore);
            
            foreach (Collider col in colliders)
            {
                // 跳过玩家对象（通过Tag判断）
                if (col.CompareTag("Player"))
                {
                    continue;
                }
                
                // 快速检查: 只检查直接父对象，不遍历整个层级（性能优化）
                Transform parent = col.transform.parent;
                if (parent != null)
                {
                    // 检查直接父对象是否是玩家
                    if (parent.GetComponent<NetworkObject>() != null || 
                        parent.GetComponent<PlayerMovement>() != null ||
                        parent.CompareTag("Player"))
                    {
                        continue;
                    }
                }
                
                // 如果检测到非玩家的Collider，说明在地面上
                return true;
            }
        }
        
        // 如果没有找到非玩家的Collider，返回false
        return false;
    }
    
    /// <summary>
    /// 触发跳跃动画 - 参考ThirdPersonController.OnJump
    /// </summary>
    public void TriggerJump()
    {
        if (animator != null && IsGrounded())
        {
            animator.SetTrigger(JumpHash);
        }
    }
    
    /// <summary>
    /// 设置玩家移动组件
    /// </summary>
    public void SetPlayerMovement(PlayerMovement movement)
    {
        playerMovement = movement;
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        var networkLoader = GetComponent<NetworkThirdPersonLoader>();
        if (networkLoader == null)
        {
            networkLoader = GetComponentInParent<NetworkThirdPersonLoader>();
        }
        
        if (networkLoader != null)
        {
            networkLoader.OnLoadComplete -= OnAvatarLoadComplete;
        }
    }
}

