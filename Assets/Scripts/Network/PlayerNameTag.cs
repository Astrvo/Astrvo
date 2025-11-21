using UnityEngine;
using TMPro;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;

/// <summary>
/// 玩家名称标签 - 在玩家头上显示用户名
/// 通过网络同步用户名到所有客户端
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerNameTag : NetworkBehaviour
{
    [Header("UI组件")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Canvas nameCanvas;
    
    [Header("显示设置")]
    [SerializeField] private float nameTagHeight = 2.5f; // 名称标签在玩家头上的高度
    [SerializeField] private bool lookAtCamera = true; // 名称标签是否始终面向相机
    [SerializeField] private Color nameColor = Color.white;
    [SerializeField] private int fontSize = 24;
    
    [Header("调试设置")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // 同步的用户名
    private readonly SyncVar<string> _playerName = new SyncVar<string>();
    
    private Camera _mainCamera;
    private Transform _nameTagTransform;
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 初始化名称标签（所有客户端都需要）
        InitializeNameTag();
        
        // 订阅用户名变化事件（必须在设置值之前订阅）
        _playerName.OnChange += OnPlayerNameChanged;
        
        // 如果是Owner，设置用户名
        if (IsOwner)
        {
            SetPlayerName();
        }
        else
        {
            // 非Owner：检查是否已经有同步的用户名
            // 注意：SyncVar可能在OnStartClient之前就已经同步了
            if (!string.IsNullOrEmpty(_playerName.Value))
            {
                UpdateNameDisplay(_playerName.Value);
                LogDebug($"Non-owner: Displaying existing name: {_playerName.Value}");
            }
            else
            {
                // 如果还没有同步，显示临时文本，等待SyncVar更新
                UpdateNameDisplay("Loading...");
                LogDebug("Non-owner: Waiting for name to sync...");
                
                // 延迟检查一次，因为SyncVar可能稍后同步
                StartCoroutine(CheckNameAfterDelay());
            }
        }
    }
    
    /// <summary>
    /// 延迟检查用户名（用于非Owner客户端）
    /// </summary>
    private IEnumerator CheckNameAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        
        // 再次检查SyncVar是否已经同步
        if (!string.IsNullOrEmpty(_playerName.Value))
        {
            UpdateNameDisplay(_playerName.Value);
            LogDebug($"Non-owner: Name synced after delay: {_playerName.Value}");
        }
        else
        {
            // 如果还是没有，使用默认名称
            string defaultName = $"Player_{NetworkObject.OwnerId}";
            UpdateNameDisplay(defaultName);
            LogDebug($"Non-owner: Using default name: {defaultName}");
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // 取消订阅事件（SyncVar永远不会为null，所以直接取消订阅）
        _playerName.OnChange -= OnPlayerNameChanged;
    }
    
    /// <summary>
    /// 初始化名称标签
    /// </summary>
    private void InitializeNameTag()
    {
        // 如果没有指定Canvas，尝试查找或创建
        if (nameCanvas == null)
        {
            // 尝试在子对象中查找Canvas
            nameCanvas = GetComponentInChildren<Canvas>();
            
            if (nameCanvas == null)
            {
                // 尝试查找名为"Username"的TextMeshProUGUI
                nameText = GetComponentInChildren<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameCanvas = nameText.GetComponentInParent<Canvas>();
                }
            }
        }
        
        // 如果还是没有找到Canvas，创建一个
        if (nameCanvas == null)
        {
            GameObject canvasObj = new GameObject("NameTagCanvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = new Vector3(0, nameTagHeight, 0);
            canvasObj.transform.localRotation = Quaternion.identity;
            
            nameCanvas = canvasObj.AddComponent<Canvas>();
            // 使用WorldSpace模式，这样每个player的name tag会显示在各自头上
            nameCanvas.renderMode = RenderMode.WorldSpace;
            
            // 设置Canvas大小和缩放（WorldSpace模式）
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(1, 1);
                canvasRect.localScale = Vector3.one * 0.01f; // 缩小以适应世界空间
            }
            
            // 添加CanvasScaler以确保文本清晰
            UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            // 创建TextMeshProUGUI
            if (nameText == null)
            {
                GameObject textObj = new GameObject("NameText");
                textObj.transform.SetParent(canvasObj.transform);
                textObj.transform.localPosition = Vector3.zero;
                textObj.transform.localRotation = Quaternion.identity;
                
                nameText = textObj.AddComponent<TextMeshProUGUI>();
                RectTransform textRect = textObj.GetComponent<RectTransform>();
                if (textRect != null)
                {
                    textRect.anchorMin = new Vector2(0.5f, 0.5f);
                    textRect.anchorMax = new Vector2(0.5f, 0.5f);
                    textRect.sizeDelta = new Vector2(500, 50);
                    textRect.anchoredPosition = Vector3.zero;
                }
            }
        }
        
        // 如果Canvas已经存在但使用的是ScreenSpaceCamera，改为WorldSpace
        if (nameCanvas != null && nameCanvas.renderMode != RenderMode.WorldSpace)
        {
            nameCanvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = nameCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one * 0.01f; // 缩小以适应世界空间
            }
        }
        
        // 如果还没有找到nameText，尝试在Canvas中查找
        if (nameText == null && nameCanvas != null)
        {
            nameText = nameCanvas.GetComponentInChildren<TextMeshProUGUI>();
        }
        
        // 如果还是没有，创建一个
        if (nameText == null)
        {
            GameObject textObj = new GameObject("NameText");
            textObj.transform.SetParent(nameCanvas.transform);
            textObj.transform.localPosition = Vector3.zero;
            
            nameText = textObj.AddComponent<TextMeshProUGUI>();
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.sizeDelta = new Vector2(500, 50);
                textRect.anchoredPosition = Vector3.zero;
            }
        }
        
        // 设置名称标签位置（确保在每个玩家头上）
        if (nameCanvas != null)
        {
            _nameTagTransform = nameCanvas.transform;
            // 确保名称标签在玩家头上（相对于玩家transform）
            _nameTagTransform.localPosition = new Vector3(0, nameTagHeight, 0);
            _nameTagTransform.localRotation = Quaternion.identity; // 初始无旋转
            
            LogDebug($"Name tag position set to local position (0, {nameTagHeight}, 0)");
        }
        
        // 配置TextMeshProUGUI
        if (nameText != null)
        {
            nameText.fontSize = fontSize;
            nameText.color = nameColor;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.text = "Loading...";
            
            // 确保TextMeshProUGUI是激活的
            if (!nameText.gameObject.activeSelf)
            {
                nameText.gameObject.SetActive(true);
            }
        }
        
        // 确保Canvas是激活的
        if (nameCanvas != null && !nameCanvas.gameObject.activeSelf)
        {
            nameCanvas.gameObject.SetActive(true);
        }
        
        // 获取主相机
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }
        
        LogDebug($"Name tag initialized (IsOwner: {IsOwner}, NetworkObjectId: {NetworkObject.ObjectId})");
    }
    
    /// <summary>
    /// 设置玩家名称（仅Owner调用）
    /// </summary>
    private void SetPlayerName()
    {
        string username = GetUsername();
        if (!string.IsNullOrEmpty(username))
        {
            // 立即更新本地显示
            UpdateNameDisplay(username);
            
            // 通过ServerRpc发送到服务器
            ServerSetPlayerName(username);
            LogDebug($"Player name set locally and sent to server: {username} (IsOwner: {IsOwner})");
        }
        else
        {
            // 如果用户名还没准备好，等待一下
            StartCoroutine(WaitForUsernameAndSet());
        }
    }
    
    /// <summary>
    /// 服务器端设置玩家名称（通过ServerRpc调用）
    /// </summary>
    [ServerRpc]
    public void ServerSetPlayerName(string name)
    {
        // 服务器端设置SyncVar，会自动同步到所有客户端
        _playerName.Value = name;
        LogDebug($"Server received and set player name: {name}");
    }
    
    /// <summary>
    /// 等待用户名准备好后设置
    /// </summary>
    private IEnumerator WaitForUsernameAndSet()
    {
        float timeout = 10f;
        float elapsed = 0f;
        
        while (string.IsNullOrEmpty(GetUsername()) && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        string username = GetUsername();
        if (!string.IsNullOrEmpty(username))
        {
            // 立即更新本地显示
            UpdateNameDisplay(username);
            
            ServerSetPlayerName(username);
            LogDebug($"Player name set locally and sent to server after wait: {username}");
        }
        else
        {
            // 使用默认名称
            string defaultName = $"Player_{NetworkObject.OwnerId}";
            
            // 立即更新本地显示
            UpdateNameDisplay(defaultName);
            
            ServerSetPlayerName(defaultName);
            LogDebug($"Using default name: {defaultName}");
        }
    }
    
    /// <summary>
    /// 获取用户名
    /// </summary>
    private string GetUsername()
    {
        // 尝试从PlayFabManager获取
        if (PlayFabSystem.PlayFabManager.Instance != null && PlayFabSystem.PlayFabManager.Instance.IsLoggedIn)
        {
            string username = PlayFabSystem.PlayFabManager.Instance.CurrentUsername;
            if (!string.IsNullOrEmpty(username))
            {
                return username;
            }
        }
        
        // 尝试从UsernameManager获取
        if (PlayFabSystem.UsernameManager.Instance != null)
        {
            string username = PlayFabSystem.UsernameManager.Instance.GetCurrentUsername();
            if (!string.IsNullOrEmpty(username))
            {
                return username;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 用户名变化回调
    /// </summary>
    private void OnPlayerNameChanged(string oldName, string newName, bool asServer)
    {
        LogDebug($"Player name changed from '{oldName}' to '{newName}' (asServer: {asServer}, IsOwner: {IsOwner})");
        UpdateNameDisplay(newName);
    }
    
    /// <summary>
    /// 更新名称显示
    /// </summary>
    private void UpdateNameDisplay(string name)
    {
        if (nameText != null)
        {
            nameText.text = name;
            
            // 确保Text和Canvas都是激活的
            if (!nameText.gameObject.activeSelf)
            {
                nameText.gameObject.SetActive(true);
            }
            if (nameCanvas != null && !nameCanvas.gameObject.activeSelf)
            {
                nameCanvas.gameObject.SetActive(true);
            }
            
            LogDebug($"Name display updated: '{name}' (IsOwner: {IsOwner})");
        }
        else
        {
            LogDebug("NameText is null, cannot update display. Re-initializing...");
            // 如果nameText为null，尝试重新初始化
            InitializeNameTag();
            if (nameText != null)
            {
                nameText.text = name;
            }
        }
    }
    
    private void LateUpdate()
    {
        // 确保名称标签始终可见并正确显示
        if (_nameTagTransform != null)
        {
            // 确保名称标签是激活的
            if (!_nameTagTransform.gameObject.activeSelf)
            {
                _nameTagTransform.gameObject.SetActive(true);
            }
            
            // 确保Canvas是激活的
            if (nameCanvas != null && !nameCanvas.gameObject.activeSelf)
            {
                nameCanvas.gameObject.SetActive(true);
            }
            
            // 确保Text是激活的
            if (nameText != null && !nameText.gameObject.activeSelf)
            {
                nameText.gameObject.SetActive(true);
            }
            
            // 更新名称标签位置（确保在玩家头上）
            if (_nameTagTransform.localPosition.y != nameTagHeight)
            {
                _nameTagTransform.localPosition = new Vector3(0, nameTagHeight, 0);
            }
        }
        
        // 更新相机引用（确保使用正确的相机）
        // 所有客户端都应该使用当前观察者的相机（通常是Owner的相机）
        if (_mainCamera == null || !_mainCamera.enabled || !_mainCamera.gameObject.activeInHierarchy)
        {
            // 优先查找NetworkCameraController的相机（Owner的相机）
            // 所有客户端都应该使用Owner的相机来观察name tag
            NetworkCameraController[] cameraControllers = FindObjectsOfType<NetworkCameraController>();
            foreach (NetworkCameraController cameraController in cameraControllers)
            {
                if (cameraController != null && cameraController.IsOwner)
                {
                    Camera cam = cameraController.GetComponent<Camera>();
                    if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                    {
                        _mainCamera = cam;
                        LogDebug($"Found Owner's camera: {cam.name}");
                        break;
                    }
                }
            }
            
            // 如果还没找到，使用主相机
            if (_mainCamera == null || !_mainCamera.enabled || !_mainCamera.gameObject.activeInHierarchy)
            {
                _mainCamera = Camera.main;
                if (_mainCamera != null)
                {
                    LogDebug($"Using Camera.main: {_mainCamera.name}");
                }
            }
            
            // 如果还是没有，查找所有激活的相机（优先选择启用的相机）
            if (_mainCamera == null || !_mainCamera.enabled || !_mainCamera.gameObject.activeInHierarchy)
            {
                Camera[] cameras = FindObjectsOfType<Camera>();
                Camera bestCamera = null;
                foreach (Camera cam in cameras)
                {
                    if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                    {
                        // 优先选择标记为MainCamera的相机
                        if (cam.CompareTag("MainCamera"))
                        {
                            bestCamera = cam;
                            break;
                        }
                        // 否则选择第一个可用的相机
                        if (bestCamera == null)
                        {
                            bestCamera = cam;
                        }
                    }
                }
                if (bestCamera != null)
                {
                    _mainCamera = bestCamera;
                    LogDebug($"Using found camera: {_mainCamera.name}");
                }
            }
        }
        
        // 更新Canvas的worldCamera（确保正确渲染）
        if (nameCanvas != null)
        {
            if (nameCanvas.renderMode == RenderMode.ScreenSpaceCamera || nameCanvas.renderMode == RenderMode.WorldSpace)
            {
                if (nameCanvas.worldCamera != _mainCamera)
                {
                    nameCanvas.worldCamera = _mainCamera;
                }
            }
        }
        
        // 让名称标签始终面向相机（所有客户端都需要，仅WorldSpace模式需要）
        if (lookAtCamera && _nameTagTransform != null && nameCanvas != null && nameCanvas.renderMode == RenderMode.WorldSpace)
        {
            if (_mainCamera != null && _mainCamera.enabled && _mainCamera.gameObject.activeInHierarchy)
            {
                // 计算从名称标签到相机的方向
                Vector3 directionToCamera = _mainCamera.transform.position - _nameTagTransform.position;
                
                // 如果距离太近，不旋转（避免抖动）
                if (directionToCamera.magnitude > 0.01f)
                {
                    // 让名称标签面向相机（Billboard效果）
                    // 注意：LookRotation默认让Z轴指向目标，但我们需要让文字朝向相机
                    // 所以使用反向方向，或者直接使用LookAt然后调整
                    
                    // 方法1：使用LookAt（会让对象朝向目标，但文字可能反）
                    // 方法2：使用LookRotation的反方向（让文字朝向相机）
                    Vector3 lookDirection = -directionToCamera.normalized; // 反转方向，让文字朝向相机
                    
                    // 计算旋转，只保留Y轴旋转（水平旋转）
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                    Vector3 eulerAngles = targetRotation.eulerAngles;
                    
                    // 只保留Y轴旋转，X和Z设为0（保持水平，不上下翻转）
                    _nameTagTransform.rotation = Quaternion.Euler(0f, eulerAngles.y, 0f);
                }
            }
            else
            {
                // 如果相机不可用，记录调试信息
                if (enableDebugLogs && _mainCamera == null)
                {
                    LogDebug("Main camera is null, cannot update name tag rotation");
                }
            }
        }
    }
    
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerNameTag] {message}");
        }
    }
}

