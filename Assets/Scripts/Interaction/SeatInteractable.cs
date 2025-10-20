using UnityEngine;
using System.Collections;

namespace InteractionSystem
{
    /// <summary>
    /// 座位交互组件
    /// 继承自InteractableObject，专门处理坐下交互
    /// </summary>
    public class SeatInteractable : InteractableObject
    {
        [Header("座位设置")]
        [SerializeField] private Transform seatPosition;
        [SerializeField] private Transform seatLookDirection;
        [SerializeField] private float sitAnimationDuration = 1f;
        [SerializeField] private AnimationCurve sitAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("座位状态")]
        [SerializeField] private bool isOccupied = false;
        [SerializeField] private bool allowMultipleOccupants = false;
        
        // 事件
        public static event System.Action<SeatInteractable, Transform> OnPlayerSitDown;
        public static event System.Action<SeatInteractable, Transform> OnPlayerStandUp;
        
        // 属性
        public bool IsOccupied => isOccupied;
        public Transform SeatPosition => seatPosition;
        public Transform SeatLookDirection => seatLookDirection;
        
        private void Start()
        {
            // 设置默认交互名称
            if (string.IsNullOrEmpty(InteractionName))
            {
                SetInteractionName("坐下");
            }
            
            if (string.IsNullOrEmpty(InteractionDescription))
            {
                SetInteractionDescription("按E键坐下");
            }
            
            // 如果没有设置座位位置，使用当前对象位置
            if (seatPosition == null)
            {
                seatPosition = transform;
            }
        }
        
        /// <summary>
        /// 重写交互触发方法
        /// </summary>
        public override void TriggerInteraction()
        {
            base.TriggerInteraction();
            
            if (isOccupied && !allowMultipleOccupants)
            {
                LogDebug("座位已被占用");
                return;
            }
            
            // 开始坐下动画
            StartCoroutine(SitDownAnimation());
        }
        
        /// <summary>
        /// 坐下动画协程
        /// </summary>
        private IEnumerator SitDownAnimation()
        {
            Transform player = GetPlayerTransform();
            if (player == null) yield break;
            
            // 保存玩家原始位置和旋转
            Vector3 originalPosition = player.position;
            Quaternion originalRotation = player.rotation;
            
            // 计算目标位置和旋转
            Vector3 targetPosition = seatPosition.position;
            Quaternion targetRotation = seatLookDirection != null ? seatLookDirection.rotation : seatPosition.rotation;
            
            float elapsedTime = 0f;
            
            while (elapsedTime < sitAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / sitAnimationDuration;
                float curveValue = sitAnimationCurve.Evaluate(progress);
                
                // 插值位置和旋转
                player.position = Vector3.Lerp(originalPosition, targetPosition, curveValue);
                player.rotation = Quaternion.Lerp(originalRotation, targetRotation, curveValue);
                
                yield return null;
            }
            
            // 确保最终位置正确
            player.position = targetPosition;
            player.rotation = targetRotation;
            
            // 设置座位状态
            isOccupied = true;
            
            // 触发事件
            OnPlayerSitDown?.Invoke(this, player);
            
            LogDebug("玩家已坐下");
            
            // 等待一段时间后自动站起（可选）
            yield return new WaitForSeconds(3f);
            
            // 站起
            StartCoroutine(StandUpAnimation(originalPosition, originalRotation));
        }
        
        /// <summary>
        /// 站起动画协程
        /// </summary>
        private IEnumerator StandUpAnimation(Vector3 originalPosition, Quaternion originalRotation)
        {
            Transform player = GetPlayerTransform();
            if (player == null) yield break;
            
            Vector3 startPosition = player.position;
            Quaternion startRotation = player.rotation;
            
            float elapsedTime = 0f;
            
            while (elapsedTime < sitAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / sitAnimationDuration;
                float curveValue = sitAnimationCurve.Evaluate(progress);
                
                // 插值位置和旋转
                player.position = Vector3.Lerp(startPosition, originalPosition, curveValue);
                player.rotation = Quaternion.Lerp(startRotation, originalRotation, curveValue);
                
                yield return null;
            }
            
            // 确保最终位置正确
            player.position = originalPosition;
            player.rotation = originalRotation;
            
            // 设置座位状态
            isOccupied = false;
            
            // 触发事件
            OnPlayerStandUp?.Invoke(this, player);
            
            LogDebug("玩家已站起");
        }
        
        /// <summary>
        /// 获取玩家Transform
        /// </summary>
        private Transform GetPlayerTransform()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player?.transform;
        }
        
        /// <summary>
        /// 设置座位位置
        /// </summary>
        public void SetSeatPosition(Transform position)
        {
            seatPosition = position;
        }
        
        /// <summary>
        /// 设置座位朝向
        /// </summary>
        public void SetSeatLookDirection(Transform lookDirection)
        {
            seatLookDirection = lookDirection;
        }
        
        /// <summary>
        /// 设置是否允许多个占用者
        /// </summary>
        public void SetAllowMultipleOccupants(bool allow)
        {
            allowMultipleOccupants = allow;
        }
        
        /// <summary>
        /// 强制站起
        /// </summary>
        public void ForceStandUp()
        {
            if (isOccupied)
            {
                StopAllCoroutines();
                isOccupied = false;
                LogDebug("强制站起");
            }
        }
        
        /// <summary>
        /// 检查是否可以坐下
        /// </summary>
        public bool CanSitDown()
        {
            return !isOccupied || allowMultipleOccupants;
        }
        
        private void OnDrawGizmosSelected()
        {
            // 绘制座位位置
            if (seatPosition != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(seatPosition.position, 0.5f);
                
                // 绘制朝向
                if (seatLookDirection != null)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(seatPosition.position, seatLookDirection.forward * 2f);
                }
            }
        }
    }
}
