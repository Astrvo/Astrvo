using UnityEngine;
using System.Collections.Generic;

namespace InteractionSystem
{
    /// <summary>
    /// 玩家交互控制器
    /// 处理玩家的交互输入和逻辑
    /// </summary>
    public class PlayerInteractionController : MonoBehaviour
    {
        [Header("输入设置")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private bool enableMouseInput = true;
        [SerializeField] private bool enableKeyboardInput = true;
        
        [Header("检测设置")]
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private LayerMask interactionLayerMask = -1;
        [SerializeField] private bool requireLineOfSight = true;
        
        [Header("调试")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        
        // 私有变量
        private Camera playerCamera;
        private List<InteractableObject> nearbyInteractables = new List<InteractableObject>();
        private InteractableObject currentInteractable;
        private bool isInitialized = false;
        private List<GameObject> tempURLObjects = new List<GameObject>(); // 跟踪临时创建的物体
        
        // 属性
        public InteractableObject CurrentInteractable => currentInteractable;
        public List<InteractableObject> NearbyInteractables => nearbyInteractables;
        public bool HasNearbyInteractables => nearbyInteractables.Count > 0;
        
        private void Start()
        {
            Initialize();
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            HandleInput();
            UpdateNearbyInteractables();
        }
        
        private void Initialize()
        {
            // 获取玩家相机
            playerCamera = GetComponent<Camera>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
            
            if (playerCamera == null)
            {
                LogDebug("警告: 未找到玩家相机");
            }
            
            isInitialized = true;
            LogDebug("玩家交互控制器初始化完成");
        }
        
        private void HandleInput()
        {
            // 键盘输入
            if (enableKeyboardInput && Input.GetKeyDown(interactionKey))
            {
                TriggerInteraction();
            }
            
            // 鼠标输入
            if (enableMouseInput && Input.GetMouseButtonDown(0))
            {
                HandleMouseClick();
            }
        }
        
        private void HandleMouseClick()
        {
            if (playerCamera == null) return;
            
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit, detectionRange, interactionLayerMask))
            {
                // 首先检查是否有InteractableObject组件
                var interactable = hit.collider.GetComponent<InteractableObject>();
                if (interactable != null)
                {
                    // 检查距离
                    float distance = Vector3.Distance(transform.position, interactable.transform.position);
                    if (distance <= interactable.InteractionRange)
                    {
                        // 检查视线
                        if (!requireLineOfSight || HasLineOfSight(interactable))
                        {
                            interactable.TriggerInteraction();
                            LogDebug($"鼠标点击触发交互: {interactable.InteractionName}");
                        }
                    }
                }
                else
                {
                    // 如果没有InteractableObject组件，检查物体名称是否以http开头
                    if (hit.collider.gameObject.name.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
                    {
                        HandleURLObjectClick(hit.collider.gameObject);
                    }
                }
            }
        }
        
        /// <summary>
        /// 处理URL物体的点击
        /// </summary>
        private void HandleURLObjectClick(GameObject urlObject)
        {
            // 检查距离
            float distance = Vector3.Distance(transform.position, urlObject.transform.position);
            if (distance <= detectionRange)
            {
                // 检查视线
                if (!requireLineOfSight || HasLineOfSightToObject(urlObject))
                {
                    // 直接打开URL
                    string url = urlObject.name;
                    LogDebug($"点击URL物体，打开链接: {url}");
                    
                    try
                    {
                        Application.OpenURL(url);
                        LogDebug($"成功打开URL: {url}");
                    }
                    catch (System.Exception e)
                    {
                        LogDebug($"打开URL失败: {e.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 检查到指定物体的视线
        /// </summary>
        private bool HasLineOfSightToObject(GameObject targetObject)
        {
            Vector3 direction = (targetObject.transform.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, targetObject.transform.position);
            
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, distance))
            {
                return hit.collider.gameObject == targetObject;
            }
            
            return true;
        }
        
        private void TriggerInteraction()
        {
            if (currentInteractable != null)
            {
                currentInteractable.TriggerInteraction();
                LogDebug($"键盘触发交互: {currentInteractable.InteractionName}");
            }
            else if (nearbyInteractables.Count > 0)
            {
                // 选择最近的交互对象
                var nearest = GetNearestInteractable();
                if (nearest != null)
                {
                    nearest.TriggerInteraction();
                    LogDebug($"键盘触发最近交互: {nearest.InteractionName}");
                }
            }
        }
        
        private void UpdateNearbyInteractables()
        {
            // 清理之前的临时物体
            CleanupTempURLObjects();
            nearbyInteractables.Clear();
            
            // 查找附近的交互对象
            var allInteractables = FindObjectsOfType<InteractableObject>();
            
            foreach (var interactable in allInteractables)
            {
                if (interactable == null) continue;
                
                float distance = Vector3.Distance(transform.position, interactable.transform.position);
                
                if (distance <= interactable.InteractionRange)
                {
                    // 检查视线
                    if (!requireLineOfSight || HasLineOfSight(interactable))
                    {
                        nearbyInteractables.Add(interactable);
                    }
                }
            }
            
            // 检测URL物体
            DetectURLObjects();
            
            // 更新当前交互对象
            UpdateCurrentInteractable();
        }
        
        /// <summary>
        /// 清理临时URL物体
        /// </summary>
        private void CleanupTempURLObjects()
        {
            foreach (var tempObj in tempURLObjects)
            {
                if (tempObj != null)
                {
                    DestroyImmediate(tempObj);
                }
            }
            tempURLObjects.Clear();
        }
        
        /// <summary>
        /// 检测附近的URL物体
        /// </summary>
        private void DetectURLObjects()
        {
            // 查找所有GameObject
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            
            foreach (GameObject obj in allObjects)
            {
                // 检查物体名称是否以http开头
                if (obj.name.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
                {
                    float distance = Vector3.Distance(transform.position, obj.transform.position);
                    
                    if (distance <= detectionRange)
                    {
                        // 检查视线
                        if (!requireLineOfSight || HasLineOfSightToObject(obj))
                        {
                            // 检查是否已经为这个物体创建了临时交互对象
                            bool alreadyExists = false;
                            foreach (var existing in nearbyInteractables)
                            {
                                if (existing != null && existing.name.Contains(obj.name))
                                {
                                    alreadyExists = true;
                                    break;
                                }
                            }
                            
                            if (!alreadyExists)
                            {
                                // 直接创建临时的InteractableObject，不挂载到原物体上
                                var tempInteractable = CreateTempURLInteractable(obj);
                                if (tempInteractable != null)
                                {
                                    nearbyInteractables.Add(tempInteractable);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 为URL物体创建临时的InteractableObject
        /// </summary>
        private InteractableObject CreateTempURLInteractable(GameObject urlObject)
        {
            // 创建一个临时的InteractableObject来处理URL交互
            var tempGO = new GameObject($"TempURLInteractable_{urlObject.name}");
            tempGO.transform.position = urlObject.transform.position;
            
            var tempInteractable = tempGO.AddComponent<URLInteractable>();
            tempInteractable.SetTargetURL(urlObject.name);
            tempInteractable.SetInteractionName("打开链接");
            tempInteractable.SetInteractionDescription($"点击打开: {urlObject.name}");
            tempInteractable.SetInteractionRange(detectionRange);
            
            // 添加到跟踪列表
            tempURLObjects.Add(tempGO);
            
            return tempInteractable;
        }
        
        private void UpdateCurrentInteractable()
        {
            if (nearbyInteractables.Count == 0)
            {
                currentInteractable = null;
                return;
            }
            
            // 选择最近的交互对象
            currentInteractable = GetNearestInteractable();
        }
        
        private InteractableObject GetNearestInteractable()
        {
            if (nearbyInteractables.Count == 0) return null;
            
            InteractableObject nearest = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var interactable in nearbyInteractables)
            {
                float distance = Vector3.Distance(transform.position, interactable.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = interactable;
                }
            }
            
            return nearest;
        }
        
        private bool HasLineOfSight(InteractableObject interactable)
        {
            Vector3 direction = (interactable.transform.position - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, interactable.transform.position);
            
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, distance))
            {
                return hit.collider.gameObject == interactable.gameObject;
            }
            
            return true;
        }
        
        /// <summary>
        /// 设置交互键
        /// </summary>
        public void SetInteractionKey(KeyCode key)
        {
            interactionKey = key;
        }
        
        /// <summary>
        /// 设置检测范围
        /// </summary>
        public void SetDetectionRange(float range)
        {
            detectionRange = range;
        }
        
        /// <summary>
        /// 设置是否启用鼠标输入
        /// </summary>
        public void SetMouseInputEnabled(bool enabled)
        {
            enableMouseInput = enabled;
        }
        
        /// <summary>
        /// 设置是否启用键盘输入
        /// </summary>
        public void SetKeyboardInputEnabled(bool enabled)
        {
            enableKeyboardInput = enabled;
        }
        
        /// <summary>
        /// 强制更新附近的交互对象
        /// </summary>
        public void ForceUpdateNearbyInteractables()
        {
            UpdateNearbyInteractables();
        }
        
        private void OnDestroy()
        {
            // 清理临时物体
            CleanupTempURLObjects();
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerInteractionController] {message}");
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            // 绘制检测范围
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            // 绘制附近的交互对象
            Gizmos.color = Color.green;
            foreach (var interactable in nearbyInteractables)
            {
                if (interactable != null)
                {
                    Gizmos.DrawLine(transform.position, interactable.transform.position);
                }
            }
            
            // 绘制当前交互对象
            if (currentInteractable != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, currentInteractable.transform.position);
            }
        }
    }
}
