using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace InteractionSystem
{
    /// <summary>
    /// 交互系统管理器
    /// 负责管理所有可交互对象和UI显示
    /// </summary>
    public class InteractionManager : MonoBehaviour
    {
        [Header("UI设置")]
        [SerializeField] private GameObject interactionUIPrefab;
        [SerializeField] private Canvas interactionCanvas;
        [SerializeField] private Transform uiParent;
        
        [Header("检测设置")]
        [SerializeField] private float detectionUpdateInterval = 0.1f;
        [SerializeField] private bool enableAutoDetection = true;
        
        [Header("UI定位设置")]
        [SerializeField] private float uiVerticalOffset = 0.5f; // UI垂直偏移量
        [SerializeField] private bool enableUIOffset = true; // 是否启用UI偏移
        
        [Header("调试")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;
        
        // 单例
        public static InteractionManager Instance { get; private set; }
        
        // 私有变量
        private List<InteractableObject> allInteractables = new List<InteractableObject>();
        private List<InteractableObject> nearbyInteractables = new List<InteractableObject>();
        private InteractableObject currentInteractable;
        private GameObject currentInteractionUI;
        private float lastDetectionTime;
        
        // 属性
        public List<InteractableObject> AllInteractables => allInteractables;
        public List<InteractableObject> NearbyInteractables => nearbyInteractables;
        public InteractableObject CurrentInteractable => currentInteractable;
        public bool HasNearbyInteractables => nearbyInteractables.Count > 0;
        
        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            Initialize();
            SubscribeToEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }
        
        private void Update()
        {
            if (enableAutoDetection && Time.time - lastDetectionTime >= detectionUpdateInterval)
            {
                UpdateNearbyInteractables();
                lastDetectionTime = Time.time;
            }
            
            // 持续更新UI位置，确保UI跟随交互对象
            if (currentInteractionUI != null && nearbyInteractables.Count > 0)
            {
                PositionUIAtInteractable();
            }
        }
        
        private void Initialize()
        {
            // 查找所有可交互对象
            FindAllInteractables();
            
            // 设置UI画布
            SetupInteractionCanvas();
            
            LogDebug("交互管理器初始化完成");
        }
        
        private void FindAllInteractables()
        {
            allInteractables.Clear();
            var interactables = FindObjectsOfType<InteractableObject>();
            allInteractables.AddRange(interactables);
            
            LogDebug($"找到 {allInteractables.Count} 个可交互对象");
        }
        
        private void SetupInteractionCanvas()
        {
            if (interactionCanvas == null)
            {
                // 创建交互UI画布 - 使用Screen Space Overlay模式
                GameObject canvasGO = new GameObject("InteractionCanvas");
                interactionCanvas = canvasGO.AddComponent<Canvas>();
                interactionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                interactionCanvas.sortingOrder = 100; // 确保在最上层
                
                // 添加CanvasScaler
                var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                // 添加GraphicRaycaster
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                
                LogDebug("创建Screen Space Overlay Canvas完成");
            }
            
            uiParent = interactionCanvas.transform;
        }
        
        private void SubscribeToEvents()
        {
            InteractableObject.OnPlayerEnterRange += OnPlayerEnterRange;
            InteractableObject.OnPlayerExitRange += OnPlayerExitRange;
            InteractableObject.OnInteractionTriggered += OnInteractionTriggered;
        }
        
        private void UnsubscribeFromEvents()
        {
            InteractableObject.OnPlayerEnterRange -= OnPlayerEnterRange;
            InteractableObject.OnPlayerExitRange -= OnPlayerExitRange;
            InteractableObject.OnInteractionTriggered -= OnInteractionTriggered;
        }
        
        private void OnPlayerEnterRange(InteractableObject interactable)
        {
            if (!nearbyInteractables.Contains(interactable))
            {
                nearbyInteractables.Add(interactable);
                LogDebug($"添加附近交互对象: {interactable.InteractionName}");
            }
            
            UpdateInteractionUI();
        }
        
        private void OnPlayerExitRange(InteractableObject interactable)
        {
            if (nearbyInteractables.Contains(interactable))
            {
                nearbyInteractables.Remove(interactable);
                LogDebug($"移除附近交互对象: {interactable.InteractionName}");
            }
            
            UpdateInteractionUI();
        }
        
        private void OnInteractionTriggered(InteractableObject interactable)
        {
            LogDebug($"交互被触发: {interactable.InteractionName}");
            
            // 可以在这里添加交互后的处理逻辑
            // 比如播放音效、动画等
        }
        
        private void UpdateNearbyInteractables()
        {
            // 手动检测附近的交互对象
            var allInteractables = FindObjectsOfType<InteractableObject>();
            var newNearbyInteractables = new List<InteractableObject>();
            
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            
            foreach (var interactable in allInteractables)
            {
                if (interactable == null) continue;
                
                float distance = Vector3.Distance(player.transform.position, interactable.transform.position);
                if (distance <= interactable.InteractionRange)
                {
                    newNearbyInteractables.Add(interactable);
                }
            }
            
            // 更新附近交互对象列表
            bool hasChanges = false;
            
            // 检查新增的交互对象
            foreach (var interactable in newNearbyInteractables)
            {
                if (!nearbyInteractables.Contains(interactable))
                {
                    nearbyInteractables.Add(interactable);
                    OnPlayerEnterRange(interactable);
                    hasChanges = true;
                    LogDebug($"手动添加附近交互对象: {interactable.InteractionName}");
                }
            }
            
            // 检查移除的交互对象
            for (int i = nearbyInteractables.Count - 1; i >= 0; i--)
            {
                var interactable = nearbyInteractables[i];
                if (interactable == null || !newNearbyInteractables.Contains(interactable))
                {
                    nearbyInteractables.RemoveAt(i);
                    OnPlayerExitRange(interactable);
                    hasChanges = true;
                    LogDebug($"手动移除附近交互对象: {interactable?.InteractionName}");
                }
            }
            
            // 如果有变化，更新UI
            if (hasChanges)
            {
                UpdateInteractionUI();
            }
        }
        
        private void UpdateInteractionUI()
        {
            // 移除当前UI
            if (currentInteractionUI != null)
            {
                Destroy(currentInteractionUI);
                currentInteractionUI = null;
            }
            
            // 如果有附近的交互对象，显示UI
            if (nearbyInteractables.Count > 0)
            {
                ShowInteractionUI();
            }
        }
        
        private void ShowInteractionUI()
        {
            if (interactionUIPrefab == null)
            {
                CreateDefaultInteractionUI();
            }
            else
            {
                currentInteractionUI = Instantiate(interactionUIPrefab, uiParent);
            }
            
            // 设置UI内容
            if (currentInteractionUI != null)
            {
                var uiScript = currentInteractionUI.GetComponent<InteractionUI>();
                if (uiScript != null)
                {
                    uiScript.SetupUI(nearbyInteractables);
                }
                
                // 定位UI到交互对象附近
                PositionUIAtInteractable();
            }
        }
        
        /// <summary>
        /// 将UI定位到交互对象附近
        /// </summary>
        private void PositionUIAtInteractable()
        {
            if (currentInteractionUI == null || nearbyInteractables.Count == 0) return;
            
            // 选择最近的交互对象
            var nearestInteractable = GetNearestInteractable();
            if (nearestInteractable == null) return;
            
            // 获取交互对象的世界位置 - 使用可配置的偏移量
            Vector3 interactableWorldPosition = nearestInteractable.transform.position;
            if (enableUIOffset)
            {
                interactableWorldPosition += Vector3.up * uiVerticalOffset;
            }
            
            // 将世界坐标转换为屏幕坐标
            Camera playerCamera = Camera.main;
            if (playerCamera == null)
            {
                playerCamera = FindObjectOfType<Camera>();
            }
            
            if (playerCamera != null)
            {
                Vector3 screenPosition = playerCamera.WorldToScreenPoint(interactableWorldPosition);
                
                // 检查对象是否在屏幕内
                if (screenPosition.z > 0 && screenPosition.x >= 0 && screenPosition.x <= Screen.width && 
                    screenPosition.y >= 0 && screenPosition.y <= Screen.height)
                {
                    // 设置UI的屏幕位置
                    var rectTransform = currentInteractionUI.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        // 将屏幕坐标转换为Canvas坐标
                        Vector2 canvasPosition;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            interactionCanvas.GetComponent<RectTransform>(),
                            screenPosition,
                            null,
                            out canvasPosition);
                        
                        rectTransform.anchoredPosition = canvasPosition;
                        LogDebug($"UI定位到屏幕坐标: {screenPosition}, Canvas坐标: {canvasPosition}");
                    }
                }
                else
                {
                    // 如果对象不在屏幕内，隐藏UI
                    currentInteractionUI.SetActive(false);
                    LogDebug("交互对象不在屏幕内，隐藏UI");
                }
            }
        }
        
        private void CreateDefaultInteractionUI()
        {
            // 创建默认的交互UI
            GameObject uiGO = new GameObject("InteractionUI");
            uiGO.transform.SetParent(uiParent);
            
            // 添加RectTransform - 适应Screen Space Overlay
            var rectTransform = uiGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(300, 100);
            
            // 添加背景
            var image = uiGO.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, 0.8f);
            
            // 添加文本
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(uiGO.transform);
            
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            var text = textGO.AddComponent<UnityEngine.UI.Text>();
            text.text = "按E键交互";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            // 添加交互UI脚本
            var interactionUI = uiGO.AddComponent<InteractionUI>();
            currentInteractionUI = uiGO;
            
            LogDebug("创建默认交互UI完成");
        }
        
        /// <summary>
        /// 手动触发当前交互
        /// </summary>
        public void TriggerCurrentInteraction()
        {
            if (currentInteractable != null)
            {
                currentInteractable.TriggerInteraction();
            }
        }
        
        /// <summary>
        /// 获取最近的交互对象
        /// </summary>
        public InteractableObject GetNearestInteractable()
        {
            if (nearbyInteractables.Count == 0) return null;
            
            Transform player = GetPlayerTransform();
            if (player == null) return null;
            
            return nearbyInteractables
                .OrderBy(x => Vector3.Distance(x.transform.position, player.position))
                .FirstOrDefault();
        }
        
        /// <summary>
        /// 设置UI垂直偏移量
        /// </summary>
        public void SetUIVerticalOffset(float offset)
        {
            uiVerticalOffset = offset;
            LogDebug($"UI垂直偏移量设置为: {offset}");
        }
        
        /// <summary>
        /// 设置是否启用UI偏移
        /// </summary>
        public void SetUIOffsetEnabled(bool enabled)
        {
            enableUIOffset = enabled;
            LogDebug($"UI偏移启用状态: {enabled}");
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
        /// 注册新的可交互对象
        /// </summary>
        public void RegisterInteractable(InteractableObject interactable)
        {
            if (!allInteractables.Contains(interactable))
            {
                allInteractables.Add(interactable);
                LogDebug($"注册新的可交互对象: {interactable.InteractionName}");
            }
        }
        
        /// <summary>
        /// 注销可交互对象
        /// </summary>
        public void UnregisterInteractable(InteractableObject interactable)
        {
            if (allInteractables.Contains(interactable))
            {
                allInteractables.Remove(interactable);
                nearbyInteractables.Remove(interactable);
                LogDebug($"注销可交互对象: {interactable.InteractionName}");
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[InteractionManager] {message}");
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            
            // 绘制所有可交互对象的范围
            Gizmos.color = Color.green;
            foreach (var interactable in allInteractables)
            {
                if (interactable != null)
                {
                    Gizmos.DrawWireSphere(interactable.transform.position, interactable.InteractionRange);
                }
            }
            
            // 绘制附近交互对象的范围
            Gizmos.color = Color.yellow;
            foreach (var interactable in nearbyInteractables)
            {
                if (interactable != null)
                {
                    Gizmos.DrawWireSphere(interactable.transform.position, interactable.InteractionRange);
                }
            }
        }
    }
}
