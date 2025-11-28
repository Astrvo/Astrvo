using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;
using Astrvo.Space;

public class LoadingManager : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI progressText;
    
    [Header("管理器引用")]
    [SerializeField] private SpaceManager spaceManager;
    
    [Header("配置")]
    [SerializeField] private float loadingDelay = 0.5f; // 延迟显示加载完成，让用户看到100%
    [SerializeField] private float progressAnimationSpeed = 2f; // 进度条动画速度
    [SerializeField] private float progressAnimationDelay = 0.1f; // 进度条动画延迟
    
    [Header("进度显示配置")]
    [SerializeField] private bool showDetailedProgress = true; // 是否显示详细进度信息
    [SerializeField] private bool showDownloadSpeed = true; // 是否显示下载速度
    [SerializeField] private bool showEstimatedTime = true; // 是否显示预计剩余时间
    
    [Header("超时设置")]
    [SerializeField] private float spaceLoadTimeout = 20f; // Space加载超时时间
    [SerializeField] private float totalLoadingTimeout = 60f; // 总加载超时时间
    
    private bool spaceLoaded = false;
    private float currentProgress = 0f;
    private float targetProgress = 0f;
    private bool isLoadingComplete = false;
    private Coroutine progressAnimationCoroutine;
    private int retryCount = 0;
    private const int maxRetries = 10; // Increased retry count
    
    // 进度跟踪变量
    private float spaceLoadProgress = 0f; // Space加载进度
    private float spaceLoadStartTime = 0f; // Space加载开始时间
    private float lastProgressTime = 0f; // 上次进度更新时间
    private float lastProgressValue = 0f; // 上次进度值
    private float downloadSpeed = 0f; // 下载速度 (MB/s)
    private float estimatedTimeRemaining = 0f; // 预计剩余时间 (秒)
    
    void Start()
    {
        InitializeLoading();
    }
    
    private void InitializeLoading()
    {
        // 显示加载界面
        ShowLoadingUI();
        
        // 订阅事件
        if (spaceManager != null)
        {
            spaceManager.OnSpaceLoadComplete += OnSpaceLoadComplete;
            spaceManager.OnSpaceLoadFailed += OnSpaceLoadFailed;
            spaceManager.OnSpaceLoadProgress += OnSpaceLoadProgress;
        }
        
        // 开始加载流程（只加载Space，Player由DelayedPlayerSpawner处理）
        StartCoroutine(LoadingSequence());
    }
    
    private IEnumerator LoadingSequence()
    {
        float totalStartTime = Time.time;
        
        // 步骤1: 等待SpaceManager初始化完成
        UpdateLoadingUI("Initializing", 0.05f);
        yield return new WaitForSeconds(0.5f);
        
        // 等待SpaceManager初始化完成
        if (spaceManager != null)
        {
            Debug.Log("[LoadingManager] 等待SpaceManager初始化完成...");
            // 这里可以添加一个检查SpaceManager是否初始化的方法
            yield return new WaitForSeconds(1f); // 给SpaceManager足够时间初始化
        }
        
        // 检查总超时
        if (Time.time - totalStartTime > totalLoadingTimeout)
        {
            Debug.LogError("[LoadingManager] 总加载时间超时，停止加载");
            UpdateLoadingUI("Timeout, please refresh", 0f);
            yield break;
        }
        
        // 步骤2: 加载Space
        UpdateLoadingUI("Loading Space", 0.1f);
        yield return new WaitForSeconds(0.5f);
        
        if (spaceManager != null)
        {
            // 记录Space加载开始时间
            spaceLoadStartTime = Time.time;
            lastProgressTime = Time.time;
            lastProgressValue = 0f;
            Debug.Log($"[LoadingManager] 检查space状态 - 已加载: {spaceManager.IsSpaceLoaded("snekspace")}, 正在加载: {spaceManager.IsSpaceLoading("snekspace")}");
            
            // 检查space是否已经加载
            if (spaceManager.IsSpaceLoaded("snekspace"))
            {
                Debug.Log("Space snekspace 已经加载完成");
                spaceLoaded = true; // 已经加载完成，直接标记
            }
            // 检查space是否正在加载
            else if (spaceManager.IsSpaceLoading("snekspace"))
            {
                Debug.Log("Space snekspace 正在加载中，等待加载完成...");
                // 不要设置 spaceLoaded = true，而是等待 OnSpaceLoadComplete 事件
                // 重置 spaceLoaded 确保等待循环能正常工作
                spaceLoaded = false;
            }
            // 如果既没有加载也没有正在加载，则开始加载
            else
            {
                Debug.Log("[LoadingManager] 开始加载space snekspace");
                spaceLoaded = false; // 确保状态正确
                spaceManager.LoadSpace("snekspace");
            }
        }
        
        // 等待Space加载完成，带超时
        float spaceLoadTimeoutStartTime = Time.time;
        while (!spaceLoaded && !isLoadingComplete && (Time.time - spaceLoadTimeoutStartTime) < spaceLoadTimeout)
        {
            // 如果space已经加载完成但事件还没触发，检查状态
            if (spaceManager != null && spaceManager.IsSpaceLoaded("snekspace") && !spaceLoaded)
            {
                Debug.Log("[LoadingManager] 检测到space已加载完成，但事件未触发，手动标记");
                spaceLoaded = true;
                break;
            }
            yield return null;
        }
        
        if (!spaceLoaded)
        {
            if (Time.time - spaceLoadTimeoutStartTime >= spaceLoadTimeout)
            {
                Debug.LogError("[LoadingManager] Space loading timeout");
                if (retryCount < maxRetries)
                {
                    UpdateLoadingUI($"Timeout, retry {retryCount + 1}/{maxRetries}", 0f);
                    yield return new WaitForSeconds(2f); // Wait 2 seconds before retry
                    RetryLoading();
                    yield break;
                }
                else
                {
                    UpdateLoadingUI("Failed, please refresh", 0f);
                }
            }
            else
            {
                // Space loading failed for other reasons (not timeout)
                Debug.LogError("[LoadingManager] Space loading failed");
                if (retryCount < maxRetries)
                {
                    UpdateLoadingUI($"Failed, retry {retryCount + 1}/{maxRetries}", 0f);
                    yield return new WaitForSeconds(2f); // Wait 2 seconds before retry
                    RetryLoading();
                    yield break;
                }
                else
                {
                    UpdateLoadingUI("Max retries reached, refresh", 0f);
                }
            }
            yield break; // If Space loading failed, stop loading
        }
        
        // Check total timeout
        if (Time.time - totalStartTime > totalLoadingTimeout)
        {
            Debug.LogError("[LoadingManager] Total loading timeout, stopping");
            UpdateLoadingUI("Timeout, please refresh", 0f);
            yield break;
        }
        
        // 步骤3: 完成加载（Space加载完成后即可，Player和Avatar由DelayedPlayerSpawner处理）
        UpdateLoadingUI("Complete", 1.0f);
        yield return new WaitForSeconds(loadingDelay);
        
        // 隐藏加载界面
        CompleteLoading();
    }
    
    private void OnSpaceLoadComplete()
    {
        spaceLoaded = true;
        spaceLoadProgress = 1.0f; // 确保进度为100%
        Debug.Log("Space加载完成");
    }
    
    private void OnSpaceLoadProgress(float progress)
    {
        spaceLoadProgress = progress;
        
        if (showDetailedProgress)
        {
            // 计算下载速度
            if (showDownloadSpeed && progress > 0f)
            {
                float currentTime = Time.time;
                float timeDelta = currentTime - lastProgressTime;
                float progressDelta = progress - lastProgressValue;
                
                if (timeDelta > 0.1f && progressDelta > 0f) // 至少0.1秒和进度变化才计算速度
                {
                    // 假设平均资源大小为50MB，这是一个估算值
                    float estimatedTotalSize = 50f; // MB
                    downloadSpeed = (progressDelta * estimatedTotalSize) / timeDelta;
                    
                    // 计算预计剩余时间
                    if (showEstimatedTime && downloadSpeed > 0f)
                    {
                        float remainingProgress = 1.0f - progress;
                        estimatedTimeRemaining = (remainingProgress * estimatedTotalSize) / downloadSpeed;
                    }
                    
                    lastProgressTime = currentTime;
                    lastProgressValue = progress;
                }
            }
            
            // 更新UI显示
            UpdateDetailedProgressUI();
        }
    }
    
    private void OnSpaceLoadFailed(string error)
    {
        Debug.LogError($"Space Load Failed: {error}");
        UpdateLoadingUI("Network error", 0f);
        isLoadingComplete = true;
    }
    
    
    private void UpdateLoadingUI(string text, float progress)
    {
        if (loadingText != null)
        {
            loadingText.text = text;
        }
        
        // 设置目标进度并开始平滑动画
        targetProgress = progress;
        StartSmoothProgressAnimation();
    }
    
    private void UpdateDetailedProgressUI()
    {
        if (loadingText != null && showDetailedProgress)
        {
            string progressText = GetLoadingStageText();
            
            // 添加进度百分比
            progressText += $" ({Mathf.RoundToInt(spaceLoadProgress * 100)}%)";
            
            if (showDownloadSpeed && downloadSpeed > 0f)
            {
                progressText += $" - {downloadSpeed:F1} MB/s";
            }
            
            if (showEstimatedTime && estimatedTimeRemaining > 0f)
            {
                if (estimatedTimeRemaining < 60f)
                {
                    progressText += $" - {estimatedTimeRemaining:F0}s remaining";
                }
                else
                {
                    int minutes = Mathf.FloorToInt(estimatedTimeRemaining / 60f);
                    int seconds = Mathf.FloorToInt(estimatedTimeRemaining % 60f);
                    progressText += $" - {minutes}m {seconds}s remaining";
                }
            }
            
            loadingText.text = progressText;
        }
        
        // 更新进度条（只显示Space加载进度）
        targetProgress = spaceLoadProgress;
        StartSmoothProgressAnimation();
    }
    
    private string GetLoadingStageText()
    {
        if (spaceLoadProgress < 0.1f)
        {
            return "Connecting";
        }
        else if (spaceLoadProgress < 0.3f)
        {
            return "Downloading";
        }
        else if (spaceLoadProgress < 0.7f)
        {
            return "Loading models";
        }
        else if (spaceLoadProgress < 0.9f)
        {
            return "Processing";
        }
        else if (spaceLoadProgress < 1.0f)
        {
            return "Finalizing";
        }
        else
        {
            return "Complete";
        }
    }
    
    private void StartSmoothProgressAnimation()
    {
        // 停止之前的动画
        if (progressAnimationCoroutine != null)
        {
            StopCoroutine(progressAnimationCoroutine);
        }
        
        // 开始新的动画
        progressAnimationCoroutine = StartCoroutine(SmoothProgressAnimation());
    }
    
    private IEnumerator SmoothProgressAnimation()
    {
        float startProgress = currentProgress;
        float progressDifference = targetProgress - startProgress;
        
        // 如果进度差异很小，直接设置
        if (Mathf.Abs(progressDifference) < 0.01f)
        {
            currentProgress = targetProgress;
            UpdateProgressBar();
            yield break;
        }
        
        float animationTime = Mathf.Abs(progressDifference) / progressAnimationSpeed;
        float elapsedTime = 0f;
        
        while (elapsedTime < animationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationTime;
            
            // 使用平滑的插值曲线
            t = Mathf.SmoothStep(0f, 1f, t);
            currentProgress = Mathf.Lerp(startProgress, targetProgress, t);
            
            UpdateProgressBar();
            yield return null;
        }
        
        // 确保最终值准确
        currentProgress = targetProgress;
        UpdateProgressBar();
    }
    
    private void UpdateProgressBar()
    {
        if (progressBar != null)
        {
            progressBar.value = currentProgress;
        }
        
        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(currentProgress * 100)}%";
        }
    }
    
    private void ShowLoadingUI()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
        
        // 初始化进度条
        currentProgress = 0f;
        targetProgress = 0f;
        if (progressBar != null)
        {
            progressBar.value = 0f;
        }
        if (progressText != null)
        {
            progressText.text = "0%";
        }
        if (loadingText != null)
        {
            loadingText.text = "Initializing...";
        }
    }
    
    private void CompleteLoading()
    {
        // 隐藏加载界面
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
        
        Debug.Log("All resources loaded, game started!");
    }
    
    void OnDestroy()
    {
        // 停止所有协程
        if (progressAnimationCoroutine != null)
        {
            StopCoroutine(progressAnimationCoroutine);
        }
        
        // 取消订阅事件
        if (spaceManager != null)
        {
            spaceManager.OnSpaceLoadComplete -= OnSpaceLoadComplete;
            spaceManager.OnSpaceLoadFailed -= OnSpaceLoadFailed;
            spaceManager.OnSpaceLoadProgress -= OnSpaceLoadProgress;
        }
    }
    
    // 公共方法，用于外部触发加载
    public void StartLoading()
    {
        if (!isLoadingComplete)
        {
            StartCoroutine(LoadingSequence());
        }
    }
    
    public bool IsLoadingComplete()
    {
        return isLoadingComplete;
    }
    
    /// <summary>
    /// 重试加载
    /// </summary>
    public void RetryLoading()
    {
        if (retryCount < maxRetries)
        {
            retryCount++;
            Debug.Log($"[LoadingManager] 重试加载 (第 {retryCount} 次)");
            
            // 检查space是否正在加载或已加载
            if (spaceManager != null)
            {
                bool isCurrentlyLoading = spaceManager.IsSpaceLoading("snekspace");
                bool isAlreadyLoaded = spaceManager.IsSpaceLoaded("snekspace");
                
                if (isCurrentlyLoading)
                {
                    Debug.Log("[LoadingManager] Space正在加载中，等待当前加载完成而不是重新加载");
                    // 如果正在加载，不要重置状态，而是等待当前加载完成
                    spaceLoaded = false; // 重置标记，等待 OnSpaceLoadComplete 事件
                    StartCoroutine(WaitForCurrentLoad());
                    return;
                }
                else if (isAlreadyLoaded)
                {
                    Debug.Log("[LoadingManager] Space已经加载完成，直接完成加载流程");
                    spaceLoaded = true;
                    StartCoroutine(CompleteLoadingSequence());
                    return;
                }
            }
            
            // 重置状态
            spaceLoaded = false;
            isLoadingComplete = false;
            currentProgress = 0f;
            targetProgress = 0f;
            
            // 重新开始加载
            StartCoroutine(LoadingSequence());
        }
        else
        {
            Debug.LogError("[LoadingManager] Maximum retry attempts reached, please refresh the page");
            UpdateLoadingUI("Failed, please refresh", 0f);
        }
    }
    
    /// <summary>
    /// 等待当前正在进行的加载完成
    /// </summary>
    private IEnumerator WaitForCurrentLoad()
    {
        float waitStartTime = Time.time;
        float waitTimeout = spaceLoadTimeout;
        
        Debug.Log("[LoadingManager] 等待当前space加载完成...");
        
        while (!spaceLoaded && !isLoadingComplete && (Time.time - waitStartTime) < waitTimeout)
        {
            // 检查space是否已经加载完成
            if (spaceManager != null && spaceManager.IsSpaceLoaded("snekspace"))
            {
                Debug.Log("[LoadingManager] 检测到space已加载完成");
                spaceLoaded = true;
                break;
            }
            yield return null;
        }
        
        if (spaceLoaded)
        {
            StartCoroutine(CompleteLoadingSequence());
        }
        else
        {
            Debug.LogError("[LoadingManager] 等待当前加载超时");
            if (retryCount < maxRetries)
            {
                UpdateLoadingUI($"Timeout, retry {retryCount + 1}/{maxRetries}", 0f);
                yield return new WaitForSeconds(2f);
                RetryLoading();
            }
            else
            {
                UpdateLoadingUI("Failed, please refresh", 0f);
            }
        }
    }
    
    /// <summary>
    /// 完成加载序列（Space已加载完成）
    /// </summary>
    private IEnumerator CompleteLoadingSequence()
    {
        UpdateLoadingUI("Complete", 1.0f);
        yield return new WaitForSeconds(loadingDelay);
        CompleteLoading();
    }
}
