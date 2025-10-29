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
    
    [Header("超时设置")]
    [SerializeField] private float catalogLoadTimeout = 10f; // catalog加载超时时间
    [SerializeField] private float spaceLoadTimeout = 15f; // space加载超时时间
    [SerializeField] private int maxSpaceLoadRetries = 5; // Space加载最大重试次数
    
    private Dictionary<string, GameObject> loadedSpaces = new Dictionary<string, GameObject>();
    private Dictionary<string, bool> loadingSpaces = new Dictionary<string, bool>(); // 跟踪正在加载的space
    private Dictionary<string, int> spaceLoadRetryCount = new Dictionary<string, int>(); // 跟踪每个space的重试次数
    private bool isInitialized = false;
    
    // 事件
    public event Action OnSpaceLoadComplete;
    public event Action<string> OnSpaceLoadFailed;
    public event Action<float> OnSpaceLoadProgress; // 新增：加载进度事件
    
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
            
            // 添加超时处理
            float catalogLoadStartTime = Time.time;
            while (!catalogHandle.IsDone && (Time.time - catalogLoadStartTime) < catalogLoadTimeout)
            {
                yield return null;
            }
            
            if (catalogHandle.Status == AsyncOperationStatus.Succeeded)
            {
                LogVerbose("远程catalog加载完成");
            }
            else
            {
                string errorDetails = catalogHandle.OperationException?.Message ?? "Unknown error";
                if ((Time.time - catalogLoadStartTime) >= catalogLoadTimeout)
                {
                    Debug.LogError($"Remote catalog loading timeout ({catalogLoadTimeout}s): {remoteCatalogUrl}");
                    errorDetails = "Loading timeout";
                }
                else
                {
                    Debug.LogError($"Remote catalog loading failed: {errorDetails}");
                }
                Debug.LogError($"Catalog URL: {remoteCatalogUrl}");
                Debug.LogError($"Status: {catalogHandle.Status}");
                LogVerbose("Will use default Addressable settings...");
            }
        }
        else
        {
            LogVerbose("Using default Addressable settings, skipping remote catalog loading");
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
            
            // 检查是否有Addressable资源配置
            LogVerbose($"开始加载Addressable资源: {addressKey}");
            
            // 尝试加载Addressable资源，但设置较短的超时时间
            var handle = Addressables.LoadAssetAsync<GameObject>(addressKey);
            
            // 添加超时处理和进度回调
            float spaceLoadStartTime = Time.time;
            while (!handle.IsDone && (Time.time - spaceLoadStartTime) < 5f) // 缩短超时时间到5秒
            {
                // 发送进度更新
                OnSpaceLoadProgress?.Invoke(handle.PercentComplete);
                yield return null;
            }
            
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
                // Addressable resource not found or failed to load, try to retry
                string errorDetails = handle.OperationException?.Message ?? "Unknown error";
                if ((Time.time - spaceLoadStartTime) >= 5f)
                {
                    errorDetails = $"Loading timeout (5s)";
                }
                
                // Check retry count
                if (!spaceLoadRetryCount.ContainsKey(actualSpaceId))
                {
                    spaceLoadRetryCount[actualSpaceId] = 0;
                }
                
                spaceLoadRetryCount[actualSpaceId]++;
                
                string errorMsg = $"SpaceManager: Failed to load Space {actualSpaceId} (attempt {spaceLoadRetryCount[actualSpaceId]}/{maxSpaceLoadRetries}), Address Key: {addressKey}. Error: {errorDetails}, Status: {handle.Status}";
                Debug.LogError(errorMsg);
                Debug.LogError($"Attempted Address Key: {addressKey}");
                Debug.LogError($"Handle Status: {handle.Status}");
                Debug.LogError($"Handle Result: {handle.Result}");
                
                if (spaceLoadRetryCount[actualSpaceId] < maxSpaceLoadRetries)
                {
                    LogVerbose($"Retrying Space {actualSpaceId} load in 2 seconds...");
                    yield return new WaitForSeconds(2f);
                    
                    // Clear loading state and retry
                    loadingSpaces[actualSpaceId] = false;
                    StartCoroutine(LoadSpaceCoroutine(actualSpaceId));
                    yield break;
                }
                else
                {
                    string finalErrorMsg = $"SpaceManager: Failed to load Space {actualSpaceId} after {maxSpaceLoadRetries} attempts. Address Key: {addressKey}. Final Error: {errorDetails}";
                    Debug.LogError(finalErrorMsg);
                    OnSpaceLoadFailed?.Invoke(finalErrorMsg);
                }
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
