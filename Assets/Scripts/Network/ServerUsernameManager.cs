using UnityEngine;
using FishNet.Object;
using FishNet.Managing;
using FishNet.Connection;
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
        if (Instance == this)
        {
            Instance = null;
            _playerUsernames.Clear();
            _playerConnections.Clear();
            LogDebug("ServerUsernameManager destroyed");
        }
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

