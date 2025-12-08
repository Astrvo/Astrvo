using UnityEngine;

namespace Astrvo.Space
{
    [RequireComponent(typeof(ThirdPersonMovement),typeof(PlayerInput))]
    public class ThirdPersonController : MonoBehaviour
    {
        private const float FALL_TIMEOUT = 0.15f;
            
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int JumpHash = Animator.StringToHash("JumpTrigger");
        private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        
        private Transform playerCamera;
        private Animator animator;
        private Vector2 inputVector;
        private Vector3 moveVector;
        private GameObject avatar;
        private ThirdPersonMovement thirdPersonMovement;
        private PlayerInput playerInput;
        
        private float fallTimeoutDelta;
        private Vector3 lastPosition;
        private float calculatedMoveSpeed;
        private float moveSpeedSmoothVelocity;
        private const float animationSmoothTime = 0.1f;
        
        [SerializeField][Tooltip("Useful to toggle input detection in editor")]
        private bool inputEnabled = true;
        private bool isInitialized;

        private void Init()
        {
            thirdPersonMovement = GetComponent<ThirdPersonMovement>();
            playerInput = GetComponent<PlayerInput>();
            playerInput.OnJumpPress += OnJump;
            isInitialized = true;
            lastPosition = transform.position;
        }

        public void Setup(GameObject target, RuntimeAnimatorController runtimeAnimatorController)
        {
            if (!isInitialized)
            {
                Init();
            }
            
            if (target == null)
            {
                Debug.LogError("[ThirdPersonController] Setup called with null target!");
                return;
            }
            
            avatar = target;
            
            // 确保avatar是激活的
            if (!avatar.activeSelf)
            {
                Debug.LogWarning("[ThirdPersonController] Avatar is inactive, activating it...");
                avatar.SetActive(true);
            }
            
            thirdPersonMovement.Setup(avatar);
            
            animator = avatar.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[ThirdPersonController] Animator component not found on avatar!");
                return;
            }
            
            if (runtimeAnimatorController != null)
            {
                animator.runtimeAnimatorController = runtimeAnimatorController;
            }
            else
            {
                Debug.LogWarning("[ThirdPersonController] RuntimeAnimatorController is null, avatar may not animate properly");
            }
            
            animator.applyRootMotion = false;
            
            Debug.Log($"[ThirdPersonController] Setup complete. Avatar: {avatar.name}, Animator: {(animator != null ? "Found" : "Missing")}, Controller: {(runtimeAnimatorController != null ? runtimeAnimatorController.name : "None")}");
        }
        
        private void Update()
        {
            if (avatar == null)
            {
                return;
            }
            if (inputEnabled)
            {
                playerInput.CheckInput();
                var xAxisInput = playerInput.AxisHorizontal;
                var yAxisInput = playerInput.AxisVertical;
                thirdPersonMovement.Move(xAxisInput, yAxisInput);
                thirdPersonMovement.SetIsRunning(playerInput.IsHoldingLeftShift);
            }
            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            // Calculate speed from position change (more robust than input-based)
            var currentPosition = transform.position;
            var horizontalDelta = currentPosition - lastPosition;
            horizontalDelta.y = 0f;
            
            var deltaTime = Time.deltaTime;
            if (deltaTime > 0)
            {
                var speed = horizontalDelta.magnitude / deltaTime;
                calculatedMoveSpeed = Mathf.SmoothDamp(calculatedMoveSpeed, speed, ref moveSpeedSmoothVelocity, animationSmoothTime);
            }
            
            // Debug Log to diagnose animation issue
            if (horizontalDelta.magnitude > 0.001f)
            {
                Debug.Log($"[ThirdPersonController] Moving! Delta: {horizontalDelta.magnitude:F4}, Speed: {calculatedMoveSpeed:F2}, IsGrounded: {thirdPersonMovement.IsGrounded()}");
            }

            lastPosition = currentPosition;

            var isGrounded = thirdPersonMovement.IsGrounded();
            animator.SetFloat(MoveSpeedHash, calculatedMoveSpeed);
            animator.SetBool(IsGroundedHash, isGrounded);
            if (isGrounded)
            {
                fallTimeoutDelta = FALL_TIMEOUT;
                animator.SetBool(FreeFallHash, false);
            }
            else
            {
                if (fallTimeoutDelta >= 0.0f)
                {
                    fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    animator.SetBool(FreeFallHash, true);
                }
            }
        }

        private void OnJump()
        {
            if (thirdPersonMovement.TryJump())
            {
                animator.SetTrigger(JumpHash);
            }
        }
    }
}
