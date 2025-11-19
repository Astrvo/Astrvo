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
        
        // 初始化名称标签
        InitializeNameTag();
        
        // 订阅用户名变化事件
        _playerName.OnChange += OnPlayerNameChanged;
        
        // 如果是Owner，设置用户名
        if (IsOwner)
        {
            SetPlayerName();
        }
        else
        {
            // 非Owner等待用户名同步
            if (!string.IsNullOrEmpty(_playerName.Value))
            {
                UpdateNameDisplay(_playerName.Value);
            }
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
            
            nameCanvas = canvasObj.AddComponent<Canvas>();
            nameCanvas.renderMode = RenderMode.WorldSpace;
            nameCanvas.worldCamera = Camera.main;
            
            // 设置Canvas大小
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(1, 1);
                canvasRect.localScale = Vector3.one * 0.01f; // 缩小以适应世界空间
            }
            
            // 创建TextMeshProUGUI
            if (nameText == null)
            {
                GameObject textObj = new GameObject("NameText");
                textObj.transform.SetParent(canvasObj.transform);
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
        
        // 设置名称标签位置
        if (nameCanvas != null)
        {
            _nameTagTransform = nameCanvas.transform;
            _nameTagTransform.localPosition = new Vector3(0, nameTagHeight, 0);
        }
        
        // 配置TextMeshProUGUI
        if (nameText != null)
        {
            nameText.fontSize = fontSize;
            nameText.color = nameColor;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.text = "Loading...";
        }
        
        // 获取主相机
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }
        
        LogDebug("Name tag initialized");
    }
    
    /// <summary>
    /// 设置玩家名称（仅Owner调用）
    /// </summary>
    private void SetPlayerName()
    {
        string username = GetUsername();
        if (!string.IsNullOrEmpty(username))
        {
            _playerName.Value = username;
            UpdateNameDisplay(username);
            LogDebug($"Player name set: {username}");
        }
        else
        {
            // 如果用户名还没准备好，等待一下
            StartCoroutine(WaitForUsernameAndSet());
        }
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
            _playerName.Value = username;
            UpdateNameDisplay(username);
            LogDebug($"Player name set after wait: {username}");
        }
        else
        {
            // 使用默认名称
            string defaultName = $"Player_{NetworkObject.OwnerId}";
            _playerName.Value = defaultName;
            UpdateNameDisplay(defaultName);
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
            LogDebug($"Name display updated: {name}");
        }
        else
        {
            LogDebug("NameText is null, cannot update display");
        }
    }
    
    private void LateUpdate()
    {
        // 让名称标签始终面向相机
        if (lookAtCamera && _nameTagTransform != null)
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    _mainCamera = FindObjectOfType<Camera>();
                }
            }
            
            if (_mainCamera != null)
            {
                _nameTagTransform.LookAt(_nameTagTransform.position + _mainCamera.transform.rotation * Vector3.forward,
                    _mainCamera.transform.rotation * Vector3.up);
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

