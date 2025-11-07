using System;
using UnityEngine;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Component.Spawning;

/// <summary>
/// 延迟玩家生成器
/// 等待Space加载完成后才生成玩家，避免玩家掉出场景
/// </summary>
public class DelayedPlayerSpawner : MonoBehaviour
{
    [Header("玩家生成配置")]
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool waitForSpaceLoad = true;
    
    [Header("延迟设置")]
    [SerializeField] private float additionalDelay = 1.0f; // Space加载完成后的额外延迟
    [SerializeField] private int waitFixedUpdates = 3; // 等待的FixedUpdate次数，确保物理系统初始化
    [SerializeField] private bool verifySpaceReady = true; // 是否验证Space是否准备好（检查地面等）
    
    /// <summary>
    /// 当玩家生成完成时触发（仅对本地玩家）
    /// 注意：现在使用PlayerNetworkSync.OnLocalPlayerInitialized事件，这个事件保留用于向后兼容
    /// </summary>
    [Obsolete("使用PlayerNetworkSync.OnLocalPlayerInitialized事件代替")]
    public event System.Action<NetworkObject> OnLocalPlayerSpawned;
    
    private NetworkManager _networkManager;
    private PlayerSpawner _playerSpawner;
    private SpaceManager _spaceManager;
    private int _nextSpawnIndex = 0;
    private bool _isWaitingForSpace = false;
    private System.Collections.Generic.HashSet<int> _spawnedConnections = new System.Collections.Generic.HashSet<int>();

    private void Awake()
    {
        // 获取NetworkManager
        _networkManager = FindObjectOfType<NetworkManager>();
        
        // 获取PlayerSpawner组件
        _playerSpawner = GetComponent<PlayerSpawner>();
        if (_playerSpawner != null)
        {
            // 暂时禁用默认的PlayerSpawner，我们将使用自定义逻辑
            _playerSpawner.enabled = false;
        }
        
        // 获取SpaceManager
        _spaceManager = FindObjectOfType<SpaceManager>();
    }

    private void Start()
    {
        if (_networkManager == null)
        {
            Debug.LogError("[DelayedPlayerSpawner] NetworkManager not found!");
            return;
        }

        // 订阅NetworkManager事件
        if (_networkManager != null && _networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        // 订阅SpaceManager事件
        if (_spaceManager != null && waitForSpaceLoad)
        {
            _spaceManager.OnSpaceLoadComplete += OnSpaceLoadComplete;
            _isWaitingForSpace = true;
            Debug.Log("[DelayedPlayerSpawner] Waiting for Space to load before spawning players");
        }
        else if (!waitForSpaceLoad)
        {
            Debug.Log("[DelayedPlayerSpawner] Space loading check disabled, players will spawn normally");
        }
    }

    /// <summary>
    /// 当客户端加载完初始场景时调用
    /// </summary>
    private void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
    {
        if (!asServer)
            return;

        // 检查这个连接是否已经生成过玩家
        if (_spawnedConnections.Contains(conn.ClientId))
        {
            Debug.Log($"[DelayedPlayerSpawner] Player already spawned for connection {conn.ClientId}");
            return;
        }

        // 如果不需要等待Space加载，直接生成玩家
        if (!waitForSpaceLoad || _spaceManager == null)
        {
            SpawnPlayer(conn);
            return;
        }

        // 检查Space是否已经加载并初始化
        if (_spaceManager != null && _spaceManager.IsSpaceLoaded("snekspace"))
        {
            // Space已经加载，但需要确保初始化完成
            Debug.Log($"[DelayedPlayerSpawner] Space already loaded, waiting for initialization for connection {conn.ClientId}");
            StartCoroutine(WaitForSpaceInitializationForConnection(conn));
        }
        else
        {
            // Space还没加载，等待Space加载完成
            _isWaitingForSpace = true;
            Debug.Log($"[DelayedPlayerSpawner] Space not loaded yet, waiting for connection {conn.ClientId}...");
        }
    }

    /// <summary>
    /// Space加载完成回调
    /// </summary>
    private void OnSpaceLoadComplete()
    {
        if (_isWaitingForSpace)
        {
            _isWaitingForSpace = false;
            Debug.Log("[DelayedPlayerSpawner] Space loaded, waiting for initialization before spawning players");
            
            // 等待Space完全初始化后再生成玩家
            StartCoroutine(WaitForSpaceInitialization());
        }
    }

    /// <summary>
    /// 等待Space完全初始化
    /// </summary>
    private System.Collections.IEnumerator WaitForSpaceInitialization()
    {
        Debug.Log("[DelayedPlayerSpawner] Waiting for Space to fully initialize...");
        
        // 等待几个FixedUpdate，确保物理系统初始化完成
        for (int i = 0; i < waitFixedUpdates; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        
        // 验证Space是否准备好
        if (verifySpaceReady)
        {
            yield return StartCoroutine(VerifySpaceReady());
        }
        
        // 额外延迟
        if (additionalDelay > 0)
        {
            yield return new WaitForSeconds(additionalDelay);
        }
        
        Debug.Log("[DelayedPlayerSpawner] Space initialization complete, spawning players for all waiting connections");
        
        // 为所有等待的连接生成玩家
        if (_networkManager != null && _networkManager.ServerManager != null)
        {
            foreach (NetworkConnection conn in _networkManager.ServerManager.Clients.Values)
            {
                if (conn != null && !_spawnedConnections.Contains(conn.ClientId))
                {
                    SpawnPlayer(conn);
                }
            }
        }
    }

    /// <summary>
    /// 验证Space是否准备好
    /// </summary>
    private System.Collections.IEnumerator VerifySpaceReady()
    {
        int maxChecks = 10;
        int checkCount = 0;
        bool spaceReady = false;

        while (!spaceReady && checkCount < maxChecks)
        {
            checkCount++;
            
            // 检查Space GameObject是否存在
            GameObject spaceObject = GameObject.Find("Space");
            if (spaceObject != null)
            {
                // 检查Space是否有子对象（说明已经实例化）
                if (spaceObject.transform.childCount > 0)
                {
                    // 尝试从Space中查找地面或碰撞体
                    Collider[] colliders = spaceObject.GetComponentsInChildren<Collider>();
                    if (colliders.Length > 0)
                    {
                        spaceReady = true;
                        Debug.Log($"[DelayedPlayerSpawner] Space verified ready (found {colliders.Length} colliders)");
                    }
                    else
                    {
                        Debug.Log($"[DelayedPlayerSpawner] Space exists but no colliders found, waiting... (check {checkCount}/{maxChecks})");
                    }
                }
                else
                {
                    Debug.Log($"[DelayedPlayerSpawner] Space GameObject exists but has no children, waiting... (check {checkCount}/{maxChecks})");
                }
            }
            else
            {
                Debug.Log($"[DelayedPlayerSpawner] Space GameObject not found, waiting... (check {checkCount}/{maxChecks})");
            }

            if (!spaceReady)
            {
                yield return new WaitForSeconds(0.2f); // 等待0.2秒后再次检查
            }
        }

        if (!spaceReady)
        {
            Debug.LogWarning("[DelayedPlayerSpawner] Space verification timeout, spawning players anyway");
        }
    }

    /// <summary>
    /// 为特定连接等待Space初始化
    /// </summary>
    private System.Collections.IEnumerator WaitForSpaceInitializationForConnection(NetworkConnection conn)
    {
        Debug.Log($"[DelayedPlayerSpawner] Waiting for Space initialization for connection {conn.ClientId}");
        
        // 等待几个FixedUpdate，确保物理系统初始化完成
        for (int i = 0; i < waitFixedUpdates; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        
        // 验证Space是否准备好
        if (verifySpaceReady)
        {
            yield return StartCoroutine(VerifySpaceReady());
        }
        
        // 额外延迟
        if (additionalDelay > 0)
        {
            yield return new WaitForSeconds(additionalDelay);
        }
        
        Debug.Log($"[DelayedPlayerSpawner] Space initialization complete, spawning player for connection {conn.ClientId}");
        SpawnPlayer(conn);
    }

    /// <summary>
    /// 生成玩家
    /// </summary>
    private void SpawnPlayer(NetworkConnection conn)
    {
        // 检查这个连接是否已经生成过玩家
        if (_spawnedConnections.Contains(conn.ClientId))
        {
            Debug.LogWarning($"[DelayedPlayerSpawner] Player already spawned for connection {conn.ClientId}");
            return;
        }

        if (playerPrefab == null)
        {
            // 从Resources加载玩家预制体
            GameObject playerObj = Resources.Load<GameObject>("Prefab/Player");
            if (playerObj != null)
            {
                playerPrefab = playerObj.GetComponent<NetworkObject>();
            }
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[DelayedPlayerSpawner] Player prefab not found in Resources/Prefab/Player!");
            return;
        }

        if (_networkManager == null || _networkManager.ServerManager == null)
        {
            Debug.LogError("[DelayedPlayerSpawner] NetworkManager or ServerManager not found!");
            return;
        }

        // 获取生成位置
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        GetSpawnPosition(out spawnPosition, out spawnRotation);

        // 实例化玩家
        NetworkObject playerInstance = _networkManager.GetPooledInstantiated(playerPrefab, spawnPosition, spawnRotation, true);
        
        if (playerInstance != null)
        {
            // 生成网络对象
            _networkManager.ServerManager.Spawn(playerInstance, conn);
            
            // 添加到默认场景
            if (_networkManager.SceneManager != null)
            {
                _networkManager.SceneManager.AddOwnerToDefaultScene(playerInstance);
            }

            // 标记这个连接已经生成过玩家
            _spawnedConnections.Add(conn.ClientId);
            Debug.Log($"[DelayedPlayerSpawner] Player spawned for connection {conn.ClientId} at {spawnPosition}");
            
            // 注意：本地玩家的检测现在由PlayerNetworkSync组件在客户端处理
            // 这里保留旧的事件触发逻辑以保持向后兼容
            if (_networkManager != null && _networkManager.ClientManager != null && 
                _networkManager.ClientManager.Connection != null && 
                _networkManager.ClientManager.Connection.ClientId == conn.ClientId)
            {
                Debug.Log("[DelayedPlayerSpawner] Local player connection detected");
                OnLocalPlayerSpawned?.Invoke(playerInstance);
            }
        }
        else
        {
            Debug.LogError("[DelayedPlayerSpawner] Failed to instantiate player prefab!");
        }
    }

    /// <summary>
    /// 获取生成位置
    /// </summary>
    private void GetSpawnPosition(out Vector3 position, out Quaternion rotation)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform spawnPoint = spawnPoints[_nextSpawnIndex % spawnPoints.Length];
            if (spawnPoint != null)
            {
                position = spawnPoint.position;
                rotation = spawnPoint.rotation;
                _nextSpawnIndex++;
                return;
            }
        }

        // 使用默认位置
        if (playerPrefab != null)
        {
            position = playerPrefab.transform.position;
            rotation = playerPrefab.transform.rotation;
        }
        else
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
        }
    }

    private void OnDestroy()
    {
        // 取消订阅事件
        if (_networkManager != null && _networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        if (_spaceManager != null)
        {
            _spaceManager.OnSpaceLoadComplete -= OnSpaceLoadComplete;
        }
    }
}

