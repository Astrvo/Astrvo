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
    private bool _hasTriedToSetName = false; // 标记是否已经尝试过设置用户名
    
    // 性能优化: 减少相机查找频率
    private float _cameraCheckInterval = 0.5f; // 每0.5秒检查一次相机
    private float _lastCameraCheckTime = 0f;
    
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        
        // 订阅用户名变化事件（必须在设置值之前订阅）
        _playerName.OnChange += OnPlayerNameChanged;
        
        // 注意：在 OnStartNetwork 中不能使用 IsOwner，只能使用 base.Owner.IsLocalClient
        bool isLocalClient = base.Owner != null && base.Owner.IsLocalClient;
        LogDebug($"OnStartNetwork called (IsLocalClient: {isLocalClient}, IsServer: {IsServer})");
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 初始化名称标签（所有客户端都需要）
        InitializeNameTag();
        
        // 如果是Owner，设置用户名
        if (IsOwner)
        {
            // 延迟一小段时间，确保网络对象完全初始化
            StartCoroutine(DelayedSetPlayerName());
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
    /// 延迟设置玩家名称（确保网络对象完全初始化）
    /// </summary>
    private IEnumerator DelayedSetPlayerName()
    {
        // 等待一帧，确保网络对象完全初始化
        yield return null;
        
        // 再等待一小段时间，确保 PlayFab 可能已经登录
        yield return new WaitForSeconds(0.1f);
        
        SetPlayerName();
    }
    
    /// <summary>
    /// 延迟检查用户名（用于非Owner客户端）
    /// </summary>
    private IEnumerator CheckNameAfterDelay()
    {
        // 多次检查，给更多时间让SyncVar同步
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForSeconds(0.2f);
            
            // 检查SyncVar是否已经同步
            if (!string.IsNullOrEmpty(_playerName.Value))
            {
                UpdateNameDisplay(_playerName.Value);
                LogDebug($"Non-owner: Name synced after delay: {_playerName.Value}");
                yield break; // 找到了，退出循环
            }
        }
        
        // 如果还是没有，使用默认名称
        string defaultName = $"Player_{NetworkObject.OwnerId}";
        UpdateNameDisplay(defaultName);
        LogDebug($"Non-owner: Using default name after timeout: {defaultName}");
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // 取消订阅事件（SyncVar永远不会为null，所以直接取消订阅）
        _playerName.OnChange -= OnPlayerNameChanged;
        
        LogDebug("OnStopClient called, unsubscribed from name change event");
    }
    
    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        
        // 取消订阅事件
        _playerName.OnChange -= OnPlayerNameChanged;
        
        LogDebug("OnStopNetwork called, unsubscribed from name change event");
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
        if (_hasTriedToSetName)
        {
            LogDebug("Already tried to set player name, skipping...");
            return;
        }
        
        _hasTriedToSetName = true;
        
        string username = GetUsername();
        LogDebug($"GetUsername() returned: {(string.IsNullOrEmpty(username) ? "null/empty" : username)}");
        
        if (!string.IsNullOrEmpty(username))
        {
            // 立即更新本地显示
            UpdateNameDisplay(username);
            
            // 尝试多种方法同步用户名：
            // 1. 先尝试直接设置 SyncVar（如果服务器有网络对象，可能会自动同步）
            TrySetSyncVarDirectly(username);
            
            // 2. 使用 ObserversRpc 广播（如果服务器端有组件，会转发给其他客户端）
            BroadcastPlayerName(username);
            
            LogDebug($"Player name set locally, attempted sync via multiple methods: {username} (IsOwner: {IsOwner})");
        }
        else
        {
            // 如果用户名还没准备好，等待一下
            LogDebug("Username not ready, starting WaitForUsernameAndSet coroutine...");
            StartCoroutine(WaitForUsernameAndSet());
        }
    }
    
    /// <summary>
    /// 广播玩家名称给所有观察者客户端
    /// 注意：如果服务器端没有这个组件，ObserversRpc 可能无法工作
    /// 作为备用方案，我们尝试直接设置 SyncVar（如果网络对象存在，可能会自动同步）
    /// </summary>
    [ObserversRpc(ExcludeOwner = false, ExcludeServer = true)]
    private void BroadcastPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            LogDebug("BroadcastPlayerName received empty name, ignoring...");
            return;
        }
        
        // 更新显示
        UpdateNameDisplay(name);
        LogDebug($"Received broadcasted player name via RPC: {name} (IsOwner: {IsOwner})");
    }
    
    /// <summary>
    /// 尝试直接设置 SyncVar（作为备用方案，如果服务器端没有代码）
    /// 注意：在 FishNet 中，客户端设置 SyncVar 通常不会同步，但我们可以尝试
    /// </summary>
    private void TrySetSyncVarDirectly(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
        
        try
        {
            // 尝试直接设置 SyncVar
            // 如果服务器端有网络对象，这可能会自动同步
            _playerName.Value = name;
            LogDebug($"Attempted to set SyncVar directly: {name} (IsOwner: {IsOwner}, IsServer: {IsServer})");
        }
        catch (System.Exception e)
        {
            LogDebug($"Failed to set SyncVar directly: {e.Message}");
        }
    }
    
    /// <summary>
    /// 等待用户名准备好后设置
    /// </summary>
    private IEnumerator WaitForUsernameAndSet()
    {
        float timeout = 15f; // 增加超时时间
        float elapsed = 0f;
        float checkInterval = 0.2f;
        
        LogDebug($"Waiting for username to be ready (timeout: {timeout}s)...");
        
        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;
            
            string username = GetUsername();
            if (!string.IsNullOrEmpty(username))
            {
                // 立即更新本地显示
                UpdateNameDisplay(username);
                
                // 尝试多种方法同步
                TrySetSyncVarDirectly(username);
                BroadcastPlayerName(username);
                LogDebug($"Player name set locally and broadcasted after wait: {username} (elapsed: {elapsed:F1}s)");
                yield break; // 成功设置，退出协程
            }
            
            // 每2秒记录一次日志
            if (Mathf.FloorToInt(elapsed) % 2 == 0 && Mathf.Approximately(elapsed % 1f, 0f))
            {
                LogDebug($"Still waiting for username... (elapsed: {elapsed:F1}s)");
            }
        }
        
        // 超时后，尝试最后一次获取
        string finalUsername = GetUsername();
        if (!string.IsNullOrEmpty(finalUsername))
        {
            UpdateNameDisplay(finalUsername);
            TrySetSyncVarDirectly(finalUsername);
            BroadcastPlayerName(finalUsername);
            LogDebug($"Player name set after timeout: {finalUsername}");
        }
        else
        {
            // 使用默认名称
            string defaultName = $"Player_{NetworkObject.OwnerId}";
            
            // 立即更新本地显示
            UpdateNameDisplay(defaultName);
            
            TrySetSyncVarDirectly(defaultName);
            BroadcastPlayerName(defaultName);
            LogDebug($"Using default name after timeout: {defaultName}");
        }
    }
    
    /// <summary>
    /// 获取用户名
    /// </summary>
    private string GetUsername()
    {
        // 尝试从PlayFabManager获取
        if (PlayFabSystem.PlayFabManager.Instance != null)
        {
            bool isLoggedIn = PlayFabSystem.PlayFabManager.Instance.IsLoggedIn;
            string username = PlayFabSystem.PlayFabManager.Instance.CurrentUsername;
            
            LogDebug($"PlayFabManager check - IsLoggedIn: {isLoggedIn}, Username: {(string.IsNullOrEmpty(username) ? "null/empty" : username)}");
            
            if (isLoggedIn && !string.IsNullOrEmpty(username))
            {
                return username;
            }
        }
        else
        {
            LogDebug("PlayFabManager.Instance is null");
        }
        
        // 尝试从UsernameManager获取
        if (PlayFabSystem.UsernameManager.Instance != null)
        {
            string username = PlayFabSystem.UsernameManager.Instance.GetCurrentUsername();
            LogDebug($"UsernameManager check - Username: {(string.IsNullOrEmpty(username) ? "null/empty" : username)}");
            
            if (!string.IsNullOrEmpty(username))
            {
                return username;
            }
        }
        else
        {
            LogDebug("UsernameManager.Instance is null");
        }
        
        // 尝试从PlayerPrefs获取（作为后备方案）
        string savedUsername = PlayerPrefs.GetString("SavedUsername", "");
        if (!string.IsNullOrEmpty(savedUsername))
        {
            LogDebug($"Found saved username in PlayerPrefs: {savedUsername}");
            return savedUsername;
        }
        
        LogDebug("No username found from any source");
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
        // 性能优化: 只在名称标签存在时才执行
        if (_nameTagTransform == null)
            return;
            
        // 性能优化: 减少不必要的激活检查（只在需要时检查）
        // 这些检查通常只需要在初始化时执行一次
        // 如果对象被意外禁用，会在下次相机检查时重新激活
        
        // 更新名称标签位置（确保在玩家头上）
        // 性能优化: 使用Vector3.Distance或直接比较，避免创建新Vector3
        float currentY = _nameTagTransform.localPosition.y;
        if (Mathf.Abs(currentY - nameTagHeight) > 0.01f)
        {
            _nameTagTransform.localPosition = new Vector3(0, nameTagHeight, 0);
        }
        
        // 性能优化: 减少相机查找频率 - 只在需要时查找
        bool needCameraCheck = _mainCamera == null || !_mainCamera.enabled || !_mainCamera.gameObject.activeInHierarchy;
        bool timeToCheck = Time.time - _lastCameraCheckTime >= _cameraCheckInterval;
        
        if (needCameraCheck && timeToCheck)
        {
            _lastCameraCheckTime = Time.time;
            UpdateCameraReference();
        }
        
        // 性能优化: 只在相机有效时才更新
        if (_mainCamera == null || !_mainCamera.enabled || !_mainCamera.gameObject.activeInHierarchy)
            return;
            
        // 更新Canvas的worldCamera（确保正确渲染）
        if (nameCanvas != null)
        {
            if ((nameCanvas.renderMode == RenderMode.ScreenSpaceCamera || nameCanvas.renderMode == RenderMode.WorldSpace) 
                && nameCanvas.worldCamera != _mainCamera)
            {
                nameCanvas.worldCamera = _mainCamera;
            }
        }
        
        // 让名称标签始终面向相机（所有客户端都需要，仅WorldSpace模式需要）
        if (lookAtCamera && nameCanvas != null && nameCanvas.renderMode == RenderMode.WorldSpace)
        {
            // 计算从名称标签到相机的方向
            Vector3 directionToCamera = _mainCamera.transform.position - _nameTagTransform.position;
            float distance = directionToCamera.sqrMagnitude; // 使用sqrMagnitude避免开方运算
            
            // 如果距离太近，不旋转（避免抖动）
            if (distance > 0.0001f) // 0.01^2 = 0.0001
            {
                // 使用反向方向，让文字朝向相机（Billboard效果）
                Vector3 lookDirection = -directionToCamera.normalized; // 反转方向，让文字朝向相机
                
                // 计算旋转，只保留Y轴旋转（水平旋转）
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                Vector3 eulerAngles = targetRotation.eulerAngles;
                
                // 只保留Y轴旋转，X和Z设为0（保持水平，不上下翻转）
                _nameTagTransform.rotation = Quaternion.Euler(0f, eulerAngles.y, 0f);
            }
        }
    }
    
    /// <summary>
    /// 更新相机引用（性能优化: 只在需要时调用，优先使用Camera.main避免FindObjectsOfType）
    /// </summary>
    private void UpdateCameraReference()
    {
        // 性能优化: 优先使用Camera.main（Owner的相机应该被标记为MainCamera）
        // 这样可以避免使用FindObjectsOfType，大大提高性能
        if (Camera.main != null && Camera.main.enabled && Camera.main.gameObject.activeInHierarchy)
        {
            _mainCamera = Camera.main;
            #if UNITY_EDITOR
            LogDebug($"Using Camera.main: {_mainCamera.name}");
            #endif
            return;
        }
        
        // 如果Camera.main不可用，尝试通过NetworkCameraController查找（但使用更高效的方式）
        // 性能优化: 使用FindFirstObjectByType（Unity 2023.1+）或FindObjectsOfType（旧版本）
        // 注意：由于我们已经将Owner的相机设置为MainCamera，这个分支应该很少执行
        NetworkCameraController cameraController = null;
        #if UNITY_2023_1_OR_NEWER
        cameraController = FindFirstObjectByType<NetworkCameraController>();
        #else
        NetworkCameraController[] controllers = FindObjectsOfType<NetworkCameraController>();
        if (controllers != null && controllers.Length > 0)
        {
            cameraController = controllers[0];
        }
        #endif
        
        if (cameraController != null && cameraController.IsOwner)
        {
            Camera cam = cameraController.GetComponent<Camera>();
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                _mainCamera = cam;
                #if UNITY_EDITOR
                LogDebug($"Found Owner's camera: {cam.name}");
                #endif
                return;
            }
        }
        
        // 最后的后备方案：查找所有激活的相机（性能开销较大，应尽量避免）
        // 性能优化: 使用FindFirstObjectByType而不是FindObjectsOfType（Unity 2023.1+）
        #if UNITY_2023_1_OR_NEWER
        Camera foundCamera = FindFirstObjectByType<Camera>();
        if (foundCamera != null && foundCamera.enabled && foundCamera.gameObject.activeInHierarchy)
        {
            _mainCamera = foundCamera;
            #if UNITY_EDITOR
            LogDebug($"Using found camera: {_mainCamera.name}");
            #endif
        }
        #else
        // Unity旧版本的回退方案（性能较差，应尽量避免）
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            {
                _mainCamera = cam;
                #if UNITY_EDITOR
                LogDebug($"Using found camera: {_mainCamera.name}");
                #endif
                break;
            }
        }
        #endif
    }
    
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerNameTag] {message}");
        }
    }
}

