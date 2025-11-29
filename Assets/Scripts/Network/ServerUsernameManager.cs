using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Component.Spawning;
using System.Collections.Generic;
using System;

/// <summary>
/// 服务器端用户名管理器
/// 在Dedicated Server模式下管理所有玩家的用户名
/// 负责接收客户端发送的用户名并同步到所有客户端
/// 注意：这是一个普通的MonoBehaviour，不需要NetworkObject，只需要在场景中存在
/// </summary>
public class ServerUsernameManager : MonoBehaviour
{
    [Header("调试设置")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // 单例模式（服务器端）
    public static ServerUsernameManager Instance { get; private set; }
    
    // 存储玩家用户名：NetworkObjectId -> Username
    private Dictionary<int, string> _playerUsernames = new Dictionary<int, string>();
    
    // 存储玩家连接：NetworkObjectId -> NetworkConnection
    private Dictionary<int, NetworkConnection> _playerConnections = new Dictionary<int, NetworkConnection>();
    
    private NetworkManager _networkManager;
    private PlayerSpawner _playerSpawner;
    private bool _isServer = false;
    
    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LogDebug("ServerUsernameManager instance created");
        }
        else
        {
            LogWarning("Multiple ServerUsernameManager instances detected! Destroying duplicate.");
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 查找NetworkManager
        _networkManager = FindObjectOfType<NetworkManager>();
        if (_networkManager == null)
        {
            LogError("NetworkManager not found! ServerUsernameManager may not work correctly.");
            return;
        }
        
        // 查找PlayerSpawner
        _playerSpawner = FindObjectOfType<PlayerSpawner>();
        if (_playerSpawner != null)
        {
            // 订阅玩家生成事件
            _playerSpawner.OnSpawned += OnPlayerSpawned;
            LogDebug("Subscribed to PlayerSpawner.OnSpawned event");
        }
        else
        {
            LogWarning("PlayerSpawner not found! Cannot sync existing usernames to new players.");
        }
        
        // 延迟检查服务器状态，因为NetworkManager可能还没完全初始化
        StartCoroutine(CheckServerStatus());
    }
    
    /// <summary>
    /// 检查服务器状态（延迟检查，确保NetworkManager已初始化）
    /// </summary>
    private System.Collections.IEnumerator CheckServerStatus()
    {
        // 等待几帧，确保NetworkManager完全初始化
        yield return new WaitForSeconds(0.1f);
        
        if (_networkManager != null)
        {
            // 检查是否是服务器
            _isServer = _networkManager.IsServerStarted;
            
            if (_isServer)
            {
                LogDebug("ServerUsernameManager initialized on server");
            }
            else
            {
                LogDebug("ServerUsernameManager initialized on client (will not function)");
            }
        }
    }
    
    private void OnDestroy()
    {
        // 取消订阅玩家生成事件
        if (_playerSpawner != null)
        {
            _playerSpawner.OnSpawned -= OnPlayerSpawned;
        }
        
        if (Instance == this)
        {
            Instance = null;
            _playerUsernames.Clear();
            _playerConnections.Clear();
            LogDebug("ServerUsernameManager destroyed");
        }
    }
    
    /// <summary>
    /// 当新玩家生成时调用（服务器端）
    /// 向新玩家发送所有已存在玩家的用户名
    /// </summary>
    private void OnPlayerSpawned(NetworkObject playerObject)
    {
        if (!_isServer)
        {
            return;
        }
        
        if (playerObject == null)
        {
            LogWarning("OnPlayerSpawned called with null playerObject");
            return;
        }
        
        LogDebug($"New player spawned: {playerObject.ObjectId}, syncing existing usernames...");
        
        // 延迟一小段时间，确保新玩家的PlayerNameTag完全初始化
        StartCoroutine(DelayedSyncUsernamesToNewPlayer(playerObject));
    }
    
    /// <summary>
    /// 延迟向新玩家同步已存在玩家的用户名
    /// </summary>
    private System.Collections.IEnumerator DelayedSyncUsernamesToNewPlayer(NetworkObject playerObject)
    {
        // 等待几帧，确保新玩家的PlayerNameTag完全初始化
        yield return new WaitForSeconds(0.5f);
        
        // 再次检查玩家对象是否仍然有效
        if (playerObject != null && playerObject.IsSpawned)
        {
            SyncExistingUsernamesToNewPlayer(playerObject);
        }
    }
    
    /// <summary>
    /// 向新玩家同步所有已存在玩家的用户名
    /// </summary>
    private void SyncExistingUsernamesToNewPlayer(NetworkObject newPlayerObject)
    {
        if (newPlayerObject == null || !newPlayerObject.IsSpawned)
        {
            LogWarning("Cannot sync usernames: new player object is null or not spawned");
            return;
        }
        
        // 获取新玩家对象上的PlayerNameTag组件
        PlayerNameTag newPlayerNameTag = newPlayerObject.GetComponent<PlayerNameTag>();
        if (newPlayerNameTag == null)
        {
            LogWarning($"PlayerNameTag not found on new player {newPlayerObject.ObjectId}");
            return;
        }
        
        // 遍历所有已存在的玩家，向新玩家发送他们的用户名
        int syncedCount = 0;
        foreach (var kvp in _playerUsernames)
        {
            int existingPlayerId = kvp.Key;
            string existingUsername = kvp.Value;
            
            // 跳过新玩家自己
            if (existingPlayerId == newPlayerObject.ObjectId)
            {
                continue;
            }
            
            // 查找已存在玩家的NetworkObject
            if (_networkManager != null && _networkManager.ServerManager != null && 
                _networkManager.ServerManager.Objects != null)
            {
                if (_networkManager.ServerManager.Objects.Spawned.TryGetValue(existingPlayerId, out NetworkObject existingPlayerObject))
                {
                    if (existingPlayerObject != null && existingPlayerObject.IsSpawned)
                    {
                        // 获取已存在玩家的PlayerNameTag
                        PlayerNameTag existingPlayerNameTag = existingPlayerObject.GetComponent<PlayerNameTag>();
                        if (existingPlayerNameTag != null)
                        {
                            // 通过RPC向新玩家发送已存在玩家的用户名
                            // 注意：这里需要调用一个专门的方法来向特定客户端发送用户名
                            existingPlayerNameTag.SyncUsernameToClient(newPlayerObject.Owner, existingUsername);
                            syncedCount++;
                            LogDebug($"Synced username '{existingUsername}' (player {existingPlayerId}) to new player {newPlayerObject.ObjectId}");
                        }
                    }
                }
            }
        }
        
        LogDebug($"Synced {syncedCount} existing username(s) to new player {newPlayerObject.ObjectId}");
    }
    
    /// <summary>
    /// 服务器端方法：设置玩家用户名
    /// 由PlayerNameTag的ServerRpc调用
    /// </summary>
    public void SetPlayerUsername(NetworkObject playerObject, string username)
    {
        if (!_isServer)
        {
            LogWarning("SetPlayerUsername called on non-server instance");
            return;
        }
        
        if (playerObject == null)
        {
            LogWarning("SetPlayerUsername called with null playerObject");
            return;
        }
        
        if (string.IsNullOrEmpty(username))
        {
            LogWarning($"SetPlayerUsername called with empty username for player {playerObject.ObjectId}");
            username = $"Player_{playerObject.OwnerId}"; // 使用默认名称
        }
        
        int objectId = playerObject.ObjectId;
        
        // 存储用户名
        _playerUsernames[objectId] = username;
        
        LogDebug($"Server received username for player {objectId} (OwnerId: {playerObject.OwnerId}): {username}");
        
        // 同步用户名到所有客户端（包括发送者）
        SyncUsernameToClients(playerObject, username);
    }
    
    /// <summary>
    /// 同步用户名到所有客户端
    /// </summary>
    private void SyncUsernameToClients(NetworkObject playerObject, string username)
    {
        if (playerObject == null || !playerObject.IsSpawned)
        {
            LogWarning($"Cannot sync username: player object is null or not spawned");
            return;
        }
        
        // 获取该玩家对象上的PlayerNameTag组件
        PlayerNameTag nameTag = playerObject.GetComponent<PlayerNameTag>();
        if (nameTag != null)
        {
            // 通过RPC更新所有客户端的用户名
            nameTag.UpdateUsernameFromServer(username);
            LogDebug($"Synced username '{username}' to all clients for player {playerObject.ObjectId}");
        }
        else
        {
            LogWarning($"PlayerNameTag not found on player {playerObject.ObjectId}");
        }
    }
    
    /// <summary>
    /// 获取玩家的用户名（服务器端调用）
    /// </summary>
    public string GetPlayerUsername(int networkObjectId)
    {
        if (_playerUsernames.TryGetValue(networkObjectId, out string username))
        {
            return username;
        }
        return null;
    }
    
    /// <summary>
    /// 当玩家断开连接时清理数据
    /// </summary>
    public void OnPlayerDisconnected(NetworkObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }
        
        int objectId = playerObject.ObjectId;
        _playerUsernames.Remove(objectId);
        _playerConnections.Remove(objectId);
        LogDebug($"Cleaned up username data for disconnected player {objectId}");
    }
    
    /// <summary>
    /// 获取所有玩家的用户名（用于调试）
    /// </summary>
    public Dictionary<int, string> GetAllPlayerUsernames()
    {
        return new Dictionary<int, string>(_playerUsernames);
    }
    
    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ServerUsernameManager] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ServerUsernameManager] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[ServerUsernameManager] {message}");
    }
}

