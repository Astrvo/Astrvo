using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Component.Spawning;
using FishNet.Connection;

/// <summary>
/// 玩家可见性管理器
/// 确保所有玩家对所有客户端可见
/// </summary>
public class PlayerVisibilityManager : MonoBehaviour
{
    [Header("调试设置")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private NetworkManager _networkManager;
    private PlayerSpawner _playerSpawner;
    
    private void Awake()
    {
        _networkManager = FindObjectOfType<NetworkManager>();
        _playerSpawner = GetComponent<PlayerSpawner>();
        
        if (_playerSpawner == null)
        {
            _playerSpawner = FindObjectOfType<PlayerSpawner>();
        }
    }
    
    private void Start()
    {
        if (_playerSpawner != null)
        {
            // 订阅玩家生成事件
            _playerSpawner.OnSpawned += OnPlayerSpawned;
            LogDebug("Subscribed to PlayerSpawner.OnSpawned event");
        }
        else
        {
            LogError("PlayerSpawner not found! Player visibility may not work correctly.");
        }
    }
    
    private void OnDestroy()
    {
        if (_playerSpawner != null)
        {
            _playerSpawner.OnSpawned -= OnPlayerSpawned;
        }
    }
    
    /// <summary>
    /// 当玩家生成时调用
    /// </summary>
    private void OnPlayerSpawned(NetworkObject playerObject)
    {
        if (playerObject == null)
        {
            LogError("Player object is null!");
            return;
        }
        
        if (_networkManager == null || _networkManager.ServerManager == null)
        {
            LogError("NetworkManager or ServerManager is null!");
            return;
        }
        
        LogDebug($"Player spawned: {playerObject.ObjectId}, ensuring visibility for all clients...");
        
        // 确保玩家对所有已连接的客户端可见
        EnsurePlayerVisibleToAllClients(playerObject);
    }
    
    /// <summary>
    /// 确保玩家对所有客户端可见
    /// </summary>
    private void EnsurePlayerVisibleToAllClients(NetworkObject playerObject)
    {
        if (_networkManager.ServerManager.Objects == null)
        {
            LogError("ServerManager.Objects is null!");
            return;
        }
        
        // 为所有已连接的客户端重建观察者
        int visibleCount = 0;
        foreach (var clientConn in _networkManager.ServerManager.Clients.Values)
        {
            if (clientConn != null && clientConn.IsActive)
            {
                // 重建观察者，确保这个客户端能看到玩家
                _networkManager.ServerManager.Objects.RebuildObservers(playerObject, clientConn);
                visibleCount++;
                LogDebug($"Rebuilt observers for player {playerObject.ObjectId} for client {clientConn.ClientId}");
            }
        }
        
        LogDebug($"Player {playerObject.ObjectId} is now visible to {visibleCount} client(s)");
        
        // 确保玩家对象在客户端是激活的
        // 注意：这应该在服务器端执行，但我们需要确保客户端也能看到
        if (playerObject.IsSpawned)
        {
            // 检查NetworkObserver配置
            if (playerObject.NetworkObserver == null)
            {
                LogWarning($"Player {playerObject.ObjectId} has no NetworkObserver component. Adding one...");
                var observer = playerObject.gameObject.AddComponent<FishNet.Observing.NetworkObserver>();
                // 使用默认的观察者条件（通常是所有客户端可见）
            }
        }
    }
    
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerVisibilityManager] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[PlayerVisibilityManager] {message}");
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[PlayerVisibilityManager] {message}");
    }
}

