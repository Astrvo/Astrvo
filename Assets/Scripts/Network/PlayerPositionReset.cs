using FishNet.Object;
using FishNet.Component.Spawning;
using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家位置重置组件
/// 在Space加载完成后，重置Owner玩家的位置到生成点
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerPositionReset : NetworkBehaviour
{
    [Header("重置设置")]
    [SerializeField] private bool resetOnSpaceLoad = true;
    [SerializeField] private float resetDelay = 0.5f; // Space加载完成后的延迟时间
    
    [Header("生成点设置")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool useDefaultSpawnPoint = true;
    [SerializeField] private Vector3 defaultSpawnPosition = Vector3.zero;
    
    private SpaceManager spaceManager;
    private bool hasReset = false;
    private int spawnIndex = 0;

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 只有Owner才需要重置位置
        if (!IsOwner)
            return;
        
        // 查找SpaceManager
        spaceManager = FindObjectOfType<SpaceManager>();
        
        if (spaceManager != null && resetOnSpaceLoad)
        {
            // 订阅Space加载完成事件
            spaceManager.OnSpaceLoadComplete += OnSpaceLoadComplete;
            
            // 检查Space是否已经加载
            if (spaceManager.IsSpaceLoaded("snekspace"))
            {
                // Space已经加载，延迟重置位置
                StartCoroutine(DelayedResetPosition());
            }
        }
        else if (resetOnSpaceLoad)
        {
            Debug.LogWarning("[PlayerPositionReset] SpaceManager not found, cannot reset position on Space load");
        }
    }
    
    /// <summary>
    /// Space加载完成回调
    /// </summary>
    private void OnSpaceLoadComplete()
    {
        if (!IsOwner || hasReset)
            return;
        
        // 延迟重置位置，确保Space完全初始化
        StartCoroutine(DelayedResetPosition());
    }
    
    /// <summary>
    /// 延迟重置位置
    /// </summary>
    private IEnumerator DelayedResetPosition()
    {
        if (hasReset)
            yield break;
        
        // 等待延迟时间
        yield return new WaitForSeconds(resetDelay);
        
        // 等待几个FixedUpdate，确保物理系统稳定
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForFixedUpdate();
        }
        
        // 重置位置
        ResetPosition();
    }
    
    /// <summary>
    /// 重置玩家位置
    /// </summary>
    public void ResetPosition()
    {
        if (!IsOwner)
            return;
        
        Vector3 spawnPosition = GetSpawnPosition();
        Quaternion spawnRotation = GetSpawnRotation();
        
        // 使用CharacterController的话，需要先禁用再设置位置
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
            controller.enabled = true;
        }
        else
        {
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
        }
        
        hasReset = true;
        Debug.Log($"[PlayerPositionReset] Player position reset to {spawnPosition}");
    }
    
    /// <summary>
    /// 获取生成位置
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        // 尝试从PlayerSpawner获取spawn points
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            PlayerSpawner spawner = FindObjectOfType<PlayerSpawner>();
            if (spawner != null && spawner.Spawns != null && spawner.Spawns.Length > 0)
            {
                spawnPoints = spawner.Spawns;
            }
        }
        
        // 如果有spawn points，使用它们
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform spawnPoint = spawnPoints[spawnIndex % spawnPoints.Length];
            if (spawnPoint != null)
            {
                spawnIndex++;
                return spawnPoint.position;
            }
        }
        
        // 使用默认位置
        if (useDefaultSpawnPoint)
        {
            return defaultSpawnPosition;
        }
        
        // 如果都不行，使用当前位置（不重置）
        return transform.position;
    }
    
    /// <summary>
    /// 获取生成旋转
    /// </summary>
    private Quaternion GetSpawnRotation()
    {
        // 尝试从spawn points获取旋转
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            int index = (spawnIndex - 1) % spawnPoints.Length;
            if (index >= 0 && spawnPoints[index] != null)
            {
                return spawnPoints[index].rotation;
            }
        }
        
        // 使用默认旋转
        return Quaternion.identity;
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        if (spaceManager != null)
        {
            spaceManager.OnSpaceLoadComplete -= OnSpaceLoadComplete;
        }
    }
}

