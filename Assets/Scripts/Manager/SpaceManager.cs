using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System.Collections.Generic;
using System;

public class SpaceManager : MonoBehaviour
{
    [Header("Addressable配置")]
    [SerializeField] private bool useRemoteCatalog = true;
    [SerializeField] private string remoteCatalogUrl = "https://pub-271d299f66224f5d937c1c6dde2d403a.r2.dev/WebGL/catalog_0.1.0.bin";
    [SerializeField] private string addressKeyPrefix = "space_";
    [SerializeField] private Transform spaceRoot;
    [SerializeField] private bool clearPreviousOnReload = true;
    
    [Header("调试设置")]
    [SerializeField] private string overrideSpaceId = "";
    [SerializeField] private string defaultLocalSpaceId = "snekspace";
    [SerializeField] private bool logVerbose = true;
    
    private Dictionary<string, GameObject> loadedSpaces = new Dictionary<string, GameObject>();
    private Dictionary<string, bool> loadingSpaces = new Dictionary<string, bool>(); // 跟踪正在加载的space
    private bool isInitialized = false;
    
    // 事件
    public event Action OnSpaceLoadComplete;
    public event Action<string> OnSpaceLoadFailed;
    
    void Start()
    {
        InitializeSpaceManager();
    }
    
    private void InitializeSpaceManager()
    {
        if (isInitialized) return;
        
        StartCoroutine(InitializeSpaceManagerCoroutine());
    }
    
    private IEnumerator InitializeSpaceManagerCoroutine()
    {
        // WebGL特殊处理：等待Addressables系统完全初始化
        #if UNITY_WEBGL && !UNITY_EDITOR
        LogVerbose("WebGL平台检测到，等待Addressables系统初始化...");
        yield return new WaitForSeconds(1f); // 给WebGL更多时间初始化
        #endif
        
        // 设置远程catalog URL
        if (useRemoteCatalog && !string.IsNullOrEmpty(remoteCatalogUrl))
        {
            LogVerbose($"正在加载远程catalog: {remoteCatalogUrl}");
            var catalogHandle = Addressables.LoadContentCatalogAsync(remoteCatalogUrl);
            yield return catalogHandle;
            
            if (catalogHandle.Status == AsyncOperationStatus.Succeeded)
            {
                LogVerbose("远程catalog加载完成");
            }
            else
            {
                string errorDetails = catalogHandle.OperationException?.Message ?? "未知错误";
                Debug.LogError($"远程catalog加载失败: {errorDetails}");
                Debug.LogError($"Catalog URL: {remoteCatalogUrl}");
                Debug.LogError($"Status: {catalogHandle.Status}");
                LogVerbose("将使用默认Addressable设置...");
            }
        }
        else
        {
            LogVerbose("使用默认Addressable设置，跳过远程catalog加载");
        }
        
        // 确保Addressables系统已初始化
        var initHandle = Addressables.InitializeAsync();
        yield return initHandle;
        
        if (initHandle.Status == AsyncOperationStatus.Succeeded)
        {
            LogVerbose("Addressables系统初始化成功");
        }
        else
        {
            Debug.LogError($"Addressables系统初始化失败: {initHandle.OperationException?.Message}");
        }
        
        // 如果没有指定spaceRoot，尝试在场景中查找Space GameObject
        if (spaceRoot == null)
        {
            GameObject spaceObject = GameObject.Find("Space");
            if (spaceObject != null)
            {
                spaceRoot = spaceObject.transform;
                LogVerbose("找到场景中的Space GameObject");
            }
            else
            {
                Debug.LogError("SpaceManager: 未找到Space GameObject，请确保场景中存在名为'Space'的GameObject");
                yield break;
            }
        }
        
        isInitialized = true;
        LogVerbose("SpaceManager初始化完成");
        
        // 注意：不再自动加载默认space，让LoadingManager来负责加载
        // 这样可以避免与LoadingManager的重复加载冲突
    }
    
    /// <summary>
    /// 根据spaceId加载对应的space
    /// </summary>
    /// <param name="spaceId">Space的ID</param>
    public void LoadSpace(string spaceId)
    {
        LogVerbose($"[LoadSpace] 被调用，spaceId: {spaceId}");
        
        if (!isInitialized)
        {
            Debug.LogWarning("SpaceManager尚未初始化，请稍后再试");
            return;
        }
        
        // 使用overrideSpaceId如果设置了
        string actualSpaceId = !string.IsNullOrEmpty(overrideSpaceId) ? overrideSpaceId : spaceId;
        LogVerbose($"[LoadSpace] 实际spaceId: {actualSpaceId} (overrideSpaceId: {overrideSpaceId})");
        
        // 检查是否已经加载过或正在加载
        if (loadedSpaces.ContainsKey(actualSpaceId))
        {
            LogVerbose($"Space {actualSpaceId} 已经加载过了，跳过重复加载");
            OnSpaceLoadComplete?.Invoke();
            return;
        }
        
        if (loadingSpaces.ContainsKey(actualSpaceId) && loadingSpaces[actualSpaceId])
        {
            LogVerbose($"Space {actualSpaceId} 正在加载中，跳过重复加载");
            return;
        }
        
        LogVerbose($"[LoadSpace] 开始加载Space: {actualSpaceId}");
        StartCoroutine(LoadSpaceCoroutine(actualSpaceId));
    }
    
    /// <summary>
    /// 加载Space的协程
    /// </summary>
    /// <param name="spaceId">Space的ID</param>
    private IEnumerator LoadSpaceCoroutine(string spaceId)
    {
        LogVerbose($"[LoadSpaceCoroutine] 开始，spaceId: {spaceId}");
        
        if (string.IsNullOrEmpty(spaceId))
        {
            Debug.LogError("SpaceManager: spaceId不能为空");
            yield break;
        }
        
        // 注意：这里spaceId已经是actualSpaceId了，不需要再次应用overrideSpaceId
        string actualSpaceId = spaceId;
        string addressKey = addressKeyPrefix + actualSpaceId;
        
        LogVerbose($"[LoadSpaceCoroutine] 开始加载Space: {actualSpaceId}, Address Key: {addressKey}");
        
        // 标记为正在加载
        loadingSpaces[actualSpaceId] = true;
        LogVerbose($"[LoadSpaceCoroutine] 已标记 {actualSpaceId} 为正在加载状态");
        
        try
        {
            // 如果设置了清除之前的space，先清除
            if (clearPreviousOnReload)
            {
                ClearAllLoadedSpaces();
            }
            
            // 再次检查是否已经加载过（防止竞态条件）
            if (loadedSpaces.ContainsKey(actualSpaceId))
            {
                LogVerbose($"Space {actualSpaceId} 已经加载过了");
                yield break;
            }
            
            // 加载Addressable资源
            LogVerbose($"开始加载Addressable资源: {addressKey}");
            var handle = Addressables.LoadAssetAsync<GameObject>(addressKey);
            yield return handle;
            
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                GameObject spacePrefab = handle.Result;
                
                // 实例化space
                GameObject spaceInstance = Instantiate(spacePrefab, spaceRoot);
                spaceInstance.name = actualSpaceId;

                #if UNITY_WEBGL && !UNITY_EDITOR
                // WebGL特殊处理：等待物理系统稳定
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                #endif
                
                // 记录已加载的space
                loadedSpaces[actualSpaceId] = spaceInstance;
                
                LogVerbose($"Space {actualSpaceId} 加载成功并挂载到Space GameObject下");
                OnSpaceLoadComplete?.Invoke();
            }
            else
            {
                string errorDetails = handle.OperationException?.Message ?? "未知错误";
                string errorMsg = $"SpaceManager: 无法加载Space {actualSpaceId}，请检查Address Key: {addressKey}。错误: {errorDetails}，状态: {handle.Status}";
                Debug.LogError(errorMsg);
                Debug.LogError($"尝试的Address Key: {addressKey}");
                Debug.LogError($"Handle Status: {handle.Status}");
                Debug.LogError($"Handle Result: {handle.Result}");
                OnSpaceLoadFailed?.Invoke(errorMsg);
            }
        }
        finally
        {
            // 清除加载状态标记
            loadingSpaces[actualSpaceId] = false;
        }
    }
    
    /// <summary>
    /// 卸载指定的space
    /// </summary>
    /// <param name="spaceId">要卸载的Space ID</param>
    public void UnloadSpace(string spaceId)
    {
        if (loadedSpaces.ContainsKey(spaceId))
        {
            GameObject spaceInstance = loadedSpaces[spaceId];
            if (spaceInstance != null)
            {
                Destroy(spaceInstance);
            }
            loadedSpaces.Remove(spaceId);
            LogVerbose($"Space {spaceId} 已卸载");
        }
        else
        {
            Debug.LogWarning($"SpaceManager: 未找到要卸载的Space {spaceId}");
        }
        
        // 同时清理加载状态
        if (loadingSpaces.ContainsKey(spaceId))
        {
            loadingSpaces.Remove(spaceId);
        }
    }
    
    /// <summary>
    /// 清除所有已加载的space
    /// </summary>
    public void ClearAllLoadedSpaces()
    {
        foreach (var kvp in loadedSpaces)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        loadedSpaces.Clear();
        loadingSpaces.Clear(); // 同时清除加载状态
        LogVerbose("所有已加载的Space已清除");
    }
    
    /// <summary>
    /// 获取已加载的space列表
    /// </summary>
    /// <returns>已加载的space ID列表</returns>
    public List<string> GetLoadedSpaceIds()
    {
        return new List<string>(loadedSpaces.Keys);
    }
    
    /// <summary>
    /// 检查指定space是否已加载
    /// </summary>
    /// <param name="spaceId">Space ID</param>
    /// <returns>是否已加载</returns>
    public bool IsSpaceLoaded(string spaceId)
    {
        return loadedSpaces.ContainsKey(spaceId);
    }
    
    /// <summary>
    /// 检查指定space是否正在加载
    /// </summary>
    /// <param name="spaceId">Space ID</param>
    /// <returns>是否正在加载</returns>
    public bool IsSpaceLoading(string spaceId)
    {
        return loadingSpaces.ContainsKey(spaceId) && loadingSpaces[spaceId];
    }
    
    private void LogVerbose(string message)
    {
        if (logVerbose)
        {
            Debug.Log($"[SpaceManager] {message}");
        }
    }
    
    void OnDestroy()
    {
        // 清理资源
        ClearAllLoadedSpaces();
    }
}
