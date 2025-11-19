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
    
    private Animator animator;
    private GameObject avatar;
    private float fallTimeoutDelta;
    private bool isInitialized = false;

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
                    Debug.Log($"[NetworkPlayerAnimationController] Avatar loaded and initialized. Avatar: {avatar.name}");
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
        
        Debug.Log($"[NetworkPlayerAnimationController] Setup complete. Avatar: {avatar.name}, Animator: {(animator != null ? "Found" : "Missing")}, Controller: {(runtimeAnimatorController != null ? runtimeAnimatorController.name : "None")}");
    }

    void Update()
    {
        // 只有初始化完成后才更新动画
        if (!isInitialized || animator == null || avatar == null)
            return;
        
        UpdateAnimator();
    }
    
    /// <summary>
    /// 更新动画器 - 参考ThirdPersonController.UpdateAnimator
    /// </summary>
    private void UpdateAnimator()
    {
        // 获取移动速度
        float targetMoveSpeed = 0f;
        if (playerMovement != null)
        {
            targetMoveSpeed = playerMovement.CurrentMoveSpeed;
        }
        
        // 设置移动速度参数（ThirdPersonController直接设置，不进行平滑）
        animator.SetFloat(MoveSpeedHash, targetMoveSpeed);
        
        // 处理地面检测和自由落体
        bool isGrounded = IsGrounded();
        animator.SetBool(IsGroundedHash, isGrounded);
        
        if (isGrounded)
        {
            // 在地面上
            fallTimeoutDelta = FALL_TIMEOUT;
            animator.SetBool(FreeFallHash, false);
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
                animator.SetBool(FreeFallHash, true);
            }
        }
    }
    
    /// <summary>
    /// 检查是否在地面上
    /// </summary>
    private bool IsGrounded()
    {
        if (characterController != null)
        {
            return characterController.isGrounded;
        }
        
        // 如果没有CharacterController，使用简单的射线检测
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f);
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

