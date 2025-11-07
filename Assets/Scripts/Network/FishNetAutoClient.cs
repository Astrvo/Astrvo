using System.Collections;
using UnityEngine;
using FishNet.Managing;
using FishNet.Example;

/// <summary>
/// 自动配置FishNet网络管理器
/// - 禁用NetworkHudCanvas
/// - 自动启动Client连接
/// </summary>
public class FishNetAutoClient : MonoBehaviour
{
    [Header("网络配置")]
    [SerializeField] private string serverAddress = "localhost";
    [SerializeField] private ushort serverPort = 7770;
    [SerializeField] private bool autoStartClient = true;
    [SerializeField] private float startDelay = 1f; // 延迟启动时间
    
    [Header("WebGL特殊设置")]
    [SerializeField] private float webglStartDelay = 2f; // WebGL环境下额外延迟
    [SerializeField] private bool useBayouPort = true; // WebGL下使用Bayou的端口设置（不手动设置端口）
    [SerializeField] private bool retryOnFailure = true; // 连接失败时是否重试
    [SerializeField] private int maxRetries = 3; // 最大重试次数
    [SerializeField] private float retryDelay = 2f; // 重试延迟
    
    private NetworkManager _networkManager;
    private bool _hasStarted = false;
    private int _retryCount = 0;

    private void Start()
    {
        // WebGL环境下需要更长的延迟
        float delay = startDelay;
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        delay = webglStartDelay;
        Debug.Log("[FishNetAutoClient] WebGL environment detected, using longer delay");
        #endif
        
        // 延迟启动，确保所有组件都已初始化
        if (autoStartClient)
        {
            StartCoroutine(InitializeNetworkCoroutine(delay));
        }
    }
    
    private IEnumerator InitializeNetworkCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL环境下额外等待一帧，确保环境完全初始化
        yield return null;
        yield return new WaitForEndOfFrame();
        #endif
        
        InitializeNetwork();
    }

    private void InitializeNetwork()
    {
        // 禁用NetworkHudCanvas
        DisableNetworkHud();
        
        // 获取NetworkManager
        _networkManager = FindObjectOfType<NetworkManager>();
        
        if (_networkManager == null)
        {
            Debug.LogWarning("[FishNetAutoClient] NetworkManager not found in scene, will retry...");
            if (retryOnFailure && _retryCount < maxRetries)
            {
                RetryConnection();
            }
            return;
        }

        // 自动启动Client
        if (autoStartClient && !_hasStarted)
        {
            StartClient();
        }
    }

    /// <summary>
    /// 禁用NetworkHudCanvas
    /// </summary>
    private void DisableNetworkHud()
    {
        // 查找所有NetworkHudCanvas对象
        GameObject[] hudCanvases = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject obj in hudCanvases)
        {
            if (obj.name == "NetworkHudCanvas")
            {
                obj.SetActive(false);
                Debug.Log("[FishNetAutoClient] NetworkHudCanvas disabled");
            }
        }

        // 也尝试通过组件查找
        NetworkHudCanvases hudComponent = FindObjectOfType<NetworkHudCanvases>();
        if (hudComponent != null)
        {
            hudComponent.gameObject.SetActive(false);
            Debug.Log("[FishNetAutoClient] NetworkHudCanvases component disabled");
        }
    }

    /// <summary>
    /// 启动Client连接
    /// </summary>
    private void StartClient()
    {
        if (_networkManager == null)
        {
            _networkManager = FindObjectOfType<NetworkManager>();
        }

        if (_networkManager == null)
        {
            Debug.LogError("[FishNetAutoClient] NetworkManager not found");
            if (retryOnFailure && _retryCount < maxRetries)
            {
                RetryConnection();
            }
            return;
        }

        if (_networkManager.ClientManager == null)
        {
            Debug.LogError("[FishNetAutoClient] ClientManager not found");
            if (retryOnFailure && _retryCount < maxRetries)
            {
                RetryConnection();
            }
            return;
        }

        if (_networkManager.ClientManager.Started)
        {
            Debug.Log("[FishNetAutoClient] Client already started");
            _hasStarted = true;
            return;
        }

        // WebGL环境下检查地址格式
        string address = serverAddress;
        #if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL可能需要使用ws://或wss://协议
        if (!address.StartsWith("ws://") && !address.StartsWith("wss://"))
        {
            // 如果不是localhost，可能需要添加协议
            if (address != "localhost" && !address.Contains("://"))
            {
                // 如果地址包含端口，使用wss（安全），否则使用ws
                address = $"ws://{address}";
                Debug.Log($"[FishNetAutoClient] WebGL: Adjusted address to {address}");
            }
        }
        
        // WebGL下如果使用Bayou的端口设置，只设置地址，不设置端口
        if (useBayouPort)
        {
            bool success = _networkManager.ClientManager.StartConnection(address);
            if (success)
            {
                _hasStarted = true;
                _retryCount = 0;
                Debug.Log($"[FishNetAutoClient] Client started connecting to {address} (using Bayou port settings)");
                
                // 订阅连接状态变化事件
                if (_networkManager.ClientManager != null)
                {
                    _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
                }
            }
            else
            {
                Debug.LogError($"[FishNetAutoClient] Failed to start client connection to {address}");
                if (retryOnFailure && _retryCount < maxRetries)
                {
                    RetryConnection();
                }
            }
            return;
        }
        #endif

        // 非WebGL或使用手动端口设置
        bool connectionSuccess = _networkManager.ClientManager.StartConnection(address, serverPort);
        if (connectionSuccess)
        {
            _hasStarted = true;
            _retryCount = 0; // 重置重试计数
            Debug.Log($"[FishNetAutoClient] Client started connecting to {address}:{serverPort}");
            
            // 订阅连接状态变化事件，用于检测连接失败
            if (_networkManager.ClientManager != null)
            {
                _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            }
        }
        else
        {
            Debug.LogError($"[FishNetAutoClient] Failed to start client connection to {address}:{serverPort}");
            if (retryOnFailure && _retryCount < maxRetries)
            {
                RetryConnection();
            }
        }
    }

    /// <summary>
    /// 客户端连接状态变化回调
    /// </summary>
    private void OnClientConnectionState(FishNet.Transporting.ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped && _hasStarted)
        {
            Debug.LogWarning("[FishNetAutoClient] Client connection stopped unexpectedly");
            _hasStarted = false;
            
            // 如果连接意外断开且允许重试，尝试重连
            if (retryOnFailure && _retryCount < maxRetries)
            {
                RetryConnection();
            }
        }
        else if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
        {
            Debug.Log("[FishNetAutoClient] Client connection established");
            _hasStarted = true;
            _retryCount = 0; // 重置重试计数
        }
    }

    /// <summary>
    /// 重试连接
    /// </summary>
    private void RetryConnection()
    {
        _retryCount++;
        Debug.Log($"[FishNetAutoClient] Retrying connection (attempt {_retryCount}/{maxRetries}) in {retryDelay} seconds...");
        Invoke(nameof(StartClient), retryDelay);
    }

    /// <summary>
    /// 手动启动Client（供外部调用）
    /// </summary>
    public void ManualStartClient(string address = null, ushort port = 0, bool useBayouPortOverride = false)
    {
        if (!string.IsNullOrEmpty(address))
        {
            serverAddress = address;
        }
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL下如果指定使用Bayou端口，忽略port参数
        if (useBayouPortOverride || useBayouPort)
        {
            useBayouPort = true;
        }
        else if (port > 0)
        {
            serverPort = port;
            useBayouPort = false;
        }
        #else
        if (port > 0)
        {
            serverPort = port;
        }
        #endif
        
        StartClient();
    }

    /// <summary>
    /// 停止Client连接
    /// </summary>
    public void StopClient()
    {
        // 取消订阅事件
        if (_networkManager != null && _networkManager.ClientManager != null)
        {
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
        
        if (_networkManager != null && _networkManager.ClientManager != null && _networkManager.ClientManager.Started)
        {
            _networkManager.ClientManager.StopConnection();
            _hasStarted = false;
            Debug.Log("[FishNetAutoClient] Client stopped");
        }
    }

    private void OnDestroy()
    {
        // 取消订阅事件
        if (_networkManager != null && _networkManager.ClientManager != null)
        {
            _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
    }
}

