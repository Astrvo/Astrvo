using UnityEngine;
using TMPro;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;

/// <summary>
/// 玩家名称标签 - 在玩家头上显示用户名
/// 客户端组件：监听PlayFab UsernameManager的用户名变化，通过服务器同步到所有客户端
/// 在Dedicated Server模式下，只运行在客户端
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
    
    // 当前显示的用户名
    private string _currentUsername = "";
    
    private Camera _mainCamera;
    private Transform _nameTagTransform;
    private bool _hasTriedToSetName = false; // 标记是否已经尝试过设置用户名
    
    // 性能优化: 减少相机查找频率
    private float _cameraCheckInterval = 0.5f; // 每0.5秒检查一次相机
    private float _lastCameraCheckTime = 0f;
    
    // 可见性控制
    private bool _isVisible = true; // 是否可见
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 只在客户端运行（Dedicated Server模式下服务器端不会有这个组件）
        if (IsServer && !IsClient)
        {
            LogDebug("Server-only mode detected, PlayerNameTag will not run on server");
            return;
        }
        
        // 初始化名称标签（所有客户端都需要）
        InitializeNameTag();
        
        // 对于非Owner客户端，检查avatar是否已经加载完成
        // 如果avatar已经加载完成且y坐标已经复位，就直接显示username
        if (!IsOwner)
        {
            // 检查NetworkThirdPersonLoader，看avatar是否已经加载完成
            NetworkThirdPersonLoader loader = GetComponent<NetworkThirdPersonLoader>();
            if (loader != null)
            {
                // 延迟检查，确保NetworkThirdPersonLoader已经初始化
                StartCoroutine(CheckAvatarAndShowUsername(loader));
            }
            else
            {
                // 如果没有loader，先隐藏，等待loader初始化
                _isVisible = false;
                if (nameCanvas != null)
                {
                    nameCanvas.gameObject.SetActive(false);
                }
                else if (nameText != null)
                {
                    nameText.gameObject.SetActive(false);
                }
                LogDebug("Non-owner: NetworkThirdPersonLoader not found, username hidden initially");
            }
        }
        
        // 显示临时文本，等待用户名同步（但如果是非Owner且已隐藏，则不会显示）
        UpdateNameDisplay("Loading...");
        
        // 订阅用户名更新事件（仅Owner需要监听PlayFab UsernameManager）
        if (IsOwner)
        {
            PlayFabSystem.UsernameManager.OnUsernameChanged += OnUsernameManagerChanged;
            LogDebug("Owner: Subscribed to UsernameManager.OnUsernameChanged");
            
            // 延迟一小段时间，确保网络对象完全初始化
            StartCoroutine(DelayedSetPlayerName());
        }
        else
        {
            // 非Owner：等待服务器同步用户名
            LogDebug("Non-owner: Waiting for server to sync username...");
            StartCoroutine(CheckNameFromServer());
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
    /// 检查avatar是否已加载完成，如果已加载则显示username
    /// </summary>
    private IEnumerator CheckAvatarAndShowUsername(NetworkThirdPersonLoader loader)
    {
        // 等待一小段时间，确保loader已经初始化
        yield return new WaitForSeconds(0.1f);
        
        // 检查玩家的y坐标，如果接近0，说明avatar已经加载完成并复位了
        float checkInterval = 0.1f;
        float timeout = 3f;
        float elapsed = 0f;
        
        while (elapsed < timeout)
        {
            // 检查玩家位置，如果y坐标接近0，说明可能已经复位了
            Vector3 playerPos = transform.position;
            
            // 如果y坐标接近0（在-0.5到0.5之间），说明avatar已经加载完成并复位了
            // 此时应该显示username（无论是否有用户名，因为用户名可能稍后同步）
            if (Mathf.Abs(playerPos.y) < 0.5f)
            {
                _isVisible = true;
                if (nameCanvas != null)
                {
                    nameCanvas.gameObject.SetActive(true);
                }
                else if (nameText != null)
                {
                    nameText.gameObject.SetActive(true);
                }
                LogDebug($"Non-owner: Avatar already loaded and y position reset (y={playerPos.y}), showing username");
                yield break;
            }
            
            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;
        }
        
        // 如果超时还没显示，说明avatar可能还在加载，保持隐藏状态
        // 等待NetworkThirdPersonLoader在avatar加载完成后调用SetVisible(true)
        _isVisible = false;
        if (nameCanvas != null)
        {
            nameCanvas.gameObject.SetActive(false);
        }
        else if (nameText != null)
        {
            nameText.gameObject.SetActive(false);
        }
        LogDebug("Non-owner: Avatar check timeout, username will be shown after avatar loads");
    }
    
    /// <summary>
    /// 延迟检查用户名（用于非Owner客户端，等待服务器同步）
    /// </summary>
    private IEnumerator CheckNameFromServer()
    {
        // 等待服务器同步用户名（通过RPC）
        // 如果用户名已经设置，会通过UpdateUsernameFromServer更新
        float timeout = 10f;
        float elapsed = 0f;
        float checkInterval = 0.2f;
        
        while (elapsed < timeout && string.IsNullOrEmpty(_currentUsername))
        {
            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;
        }
        
        // 如果超时还没有用户名，使用默认名称
        if (string.IsNullOrEmpty(_currentUsername))
        {
            string defaultName = $"Player_{NetworkObject.OwnerId}";
            UpdateNameDisplay(defaultName);
            LogDebug($"Non-owner: Using default name after timeout: {defaultName}");
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // 取消订阅用户名更新事件
        if (IsOwner)
        {
            PlayFabSystem.UsernameManager.OnUsernameChanged -= OnUsernameManagerChanged;
        }
        
        LogDebug("OnStopClient called, unsubscribed from name change event");
    }
    
    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        
        // 取消订阅用户名更新事件（安全起见，即使可能已经取消订阅）
        PlayFabSystem.UsernameManager.OnUsernameChanged -= OnUsernameManagerChanged;
        
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
        
        // 根据可见性设置Canvas激活状态
        if (nameCanvas != null && nameCanvas.gameObject.activeSelf != _isVisible)
        {
            nameCanvas.gameObject.SetActive(_isVisible);
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
    /// 通过ServerRpc发送到服务器，服务器再同步到所有客户端
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
            // 立即更新本地显示（Owner的本地预览）
            UpdateNameDisplay(username);
            
            // 通过ServerRpc发送用户名到服务器
            SendUsernameToServer(username);
            
            LogDebug($"Player name set locally and sent to server: {username} (IsOwner: {IsOwner})");
        }
        else
        {
            // 如果用户名还没准备好，等待一下
            LogDebug("Username not ready, starting WaitForUsernameAndSet coroutine...");
            StartCoroutine(WaitForUsernameAndSet());
        }
    }
    
    /// <summary>
    /// 通过ServerRpc发送用户名到服务器
    /// </summary>
    [ServerRpc(RequireOwnership = true)]
    private void SendUsernameToServer(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            LogWarning("SendUsernameToServer received empty username");
            return;
        }
        
        LogDebug($"Server received username from owner: {username}");
        
        // 服务器通过ServerUsernameManager管理并同步用户名
        if (ServerUsernameManager.Instance != null)
        {
            ServerUsernameManager.Instance.SetPlayerUsername(NetworkObject, username);
        }
        else
        {
            LogWarning("ServerUsernameManager.Instance is null! Cannot sync username.");
            // 如果ServerUsernameManager不存在，直接通过RPC更新所有客户端
            UpdateUsernameFromServer(username);
        }
    }
    
    /// <summary>
    /// 服务器调用：更新所有客户端的用户名显示
    /// 这个方法会被ServerUsernameManager调用，或者直接通过RPC调用
    /// </summary>
    [ObserversRpc(ExcludeServer = true)]
    public void UpdateUsernameFromServer(string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            LogWarning("UpdateUsernameFromServer received empty username");
            return;
        }
        
        _currentUsername = username;
        
        // 对于非Owner客户端，检查avatar是否已经加载完成
        // 如果avatar已经加载完成（y坐标已经复位），就显示username
        if (!IsOwner)
        {
            Vector3 playerPos = transform.position;
            // 如果y坐标接近0，说明avatar已经加载完成并复位了
            if (Mathf.Abs(playerPos.y) < 0.5f)
            {
                _isVisible = true;
                LogDebug($"Non-owner: Avatar already loaded (y={playerPos.y}), showing username: {username}");
            }
            // 注意：如果y坐标还没复位，保持当前的_isVisible状态
            // 不要强制设置为false，因为可能已经在CheckAvatarAndShowUsername中设置为true了
        }
        
        UpdateNameDisplay(username);
        LogDebug($"Username updated from server: {username} (IsOwner: {IsOwner}, Visible: {_isVisible})");
    }
    
    /// <summary>
    /// 服务器调用：向特定客户端同步已存在玩家的用户名
    /// 用于新玩家加入时，服务器向其发送已存在玩家的用户名
    /// 这个方法在已存在玩家的PlayerNameTag上调用，向新玩家发送该玩家的用户名
    /// </summary>
    [TargetRpc]
    public void SyncUsernameToClient(NetworkConnection targetConnection, string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            LogWarning("SyncUsernameToClient received empty username");
            return;
        }
        
        // 更新当前PlayerNameTag的用户名显示（这是已存在玩家的用户名，新玩家需要看到）
        _currentUsername = username;
        UpdateNameDisplay(username);
        LogDebug($"Username synced to new client: {username} for player {NetworkObject.ObjectId} (IsOwner: {IsOwner})");
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
                
                // 通过ServerRpc发送到服务器
                SendUsernameToServer(username);
                LogDebug($"Player name set locally and sent to server after wait: {username} (elapsed: {elapsed:F1}s)");
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
            SendUsernameToServer(finalUsername);
            LogDebug($"Player name set after timeout: {finalUsername}");
        }
        else
        {
            // 使用默认名称
            string defaultName = $"Player_{NetworkObject.OwnerId}";
            
            // 立即更新本地显示
            UpdateNameDisplay(defaultName);
            
            // 发送默认名称到服务器
            SendUsernameToServer(defaultName);
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
    /// UsernameManager用户名更新回调（仅Owner触发）
    /// 当PlayFab UsernameManager中的用户名发生变化时，同步到服务器
    /// </summary>
    private void OnUsernameManagerChanged(string newUsername)
    {
        // 只有Owner才需要处理这个事件
        if (!IsOwner)
        {
            return;
        }
        
        LogDebug($"UsernameManager username changed to: {newUsername} (IsOwner: {IsOwner})");
        
        // 更新本地显示
        UpdateNameDisplay(newUsername);
        
        // 通过ServerRpc同步到服务器，服务器再同步到所有客户端
        SendUsernameToServer(newUsername);
        
        LogDebug($"Player name updated and synced to server: {newUsername}");
    }
    
    /// <summary>
    /// 设置名称标签的可见性
    /// </summary>
    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        
        if (nameCanvas != null)
        {
            nameCanvas.gameObject.SetActive(visible);
        }
        else if (nameText != null)
        {
            nameText.gameObject.SetActive(visible);
        }
        
        LogDebug($"Name tag visibility set to: {visible} (IsOwner: {IsOwner})");
    }
    
    /// <summary>
    /// 更新名称显示
    /// </summary>
    private void UpdateNameDisplay(string name)
    {
        if (nameText != null)
        {
            nameText.text = name;
            
            // 根据可见性设置激活状态
            bool shouldBeActive = _isVisible;
            if (nameText.gameObject.activeSelf != shouldBeActive)
            {
                nameText.gameObject.SetActive(shouldBeActive);
            }
            if (nameCanvas != null && nameCanvas.gameObject.activeSelf != shouldBeActive)
            {
                nameCanvas.gameObject.SetActive(shouldBeActive);
            }
            
            LogDebug($"Name display updated: '{name}' (IsOwner: {IsOwner}, Visible: {_isVisible})");
        }
        else
        {
            LogDebug("NameText is null, cannot update display. Re-initializing...");
            // 如果nameText为null，尝试重新初始化
            InitializeNameTag();
            if (nameText != null)
            {
                nameText.text = name;
                // 设置可见性
                if (nameCanvas != null)
                {
                    nameCanvas.gameObject.SetActive(_isVisible);
                }
                else if (nameText != null)
                {
                    nameText.gameObject.SetActive(_isVisible);
                }
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
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[PlayerNameTag] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[PlayerNameTag] {message}");
    }
}

