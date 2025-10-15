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
    [SerializeField] private ThirdPersonLoader thirdPersonLoader;
    [SerializeField] private ThirdPersonController thirdPersonController;
    [SerializeField] private CameraOrbit cameraOrbit;
    
    [Header("配置")]
    [SerializeField] private string defaultAvatarUrl = "https://models.readyplayer.me/64a1a5a0b0b8b8b8b8b8b8b8.glb";
    [SerializeField] private float loadingDelay = 0.5f; // 延迟显示加载完成，让用户看到100%
    [SerializeField] private float progressAnimationSpeed = 2f; // 进度条动画速度
    [SerializeField] private float progressAnimationDelay = 0.1f; // 进度条动画延迟
    
    private bool spaceLoaded = false;
    private bool avatarLoaded = false;
    private float currentProgress = 0f;
    private float targetProgress = 0f;
    private bool isLoadingComplete = false;
    private Coroutine progressAnimationCoroutine;
    
    void Start()
    {
        InitializeLoading();
    }
    
    private void InitializeLoading()
    {
        // 显示加载界面
        ShowLoadingUI();
        
        // 禁用玩家控制
        SetPlayerControlsEnabled(false);
        
        // 订阅事件
        if (spaceManager != null)
        {
            spaceManager.OnSpaceLoadComplete += OnSpaceLoadComplete;
            spaceManager.OnSpaceLoadFailed += OnSpaceLoadFailed;
        }
        
        if (thirdPersonLoader != null)
        {
            thirdPersonLoader.OnLoadComplete += OnAvatarLoadComplete;
        }
        
        // 开始加载流程
        StartCoroutine(LoadingSequence());
    }
    
    private IEnumerator LoadingSequence()
    {
        // 步骤1: 加载Space
        UpdateLoadingUI("Loading Space", 0.1f);
        yield return new WaitForSeconds(0.5f);
        
        if (spaceManager != null)
        {
            spaceManager.LoadSpace("snekspace");
        }
        
        // 等待Space加载完成
        while (!spaceLoaded && !isLoadingComplete)
        {
            yield return null;
        }
        
        if (!spaceLoaded)
        {
            yield break; // 如果Space加载失败，停止加载
        }
        
        // 步骤2: 加载Avatar
        UpdateLoadingUI("Loading Avatar", 0.5f);
        yield return new WaitForSeconds(0.5f);
        
        if (thirdPersonLoader != null && !string.IsNullOrEmpty(defaultAvatarUrl))
        {
            thirdPersonLoader.LoadAvatar(defaultAvatarUrl);
        }
        
        // 等待Avatar加载完成
        while (!avatarLoaded && !isLoadingComplete)
        {
            yield return null;
        }
        
        // 步骤3: 完成加载
        UpdateLoadingUI("Load Finished", 1.0f);
        yield return new WaitForSeconds(loadingDelay);
        
        // 隐藏加载界面，启用玩家控制
        CompleteLoading();
    }
    
    private void OnSpaceLoadComplete()
    {
        spaceLoaded = true;
        Debug.Log("Space加载完成");
    }
    
    private void OnSpaceLoadFailed(string error)
    {
        Debug.LogError($"Space Load Failed: {error}");
        UpdateLoadingUI("空间加载失败，请检查网络连接", 0f);
        isLoadingComplete = true;
    }
    
    private void OnAvatarLoadComplete()
    {
        avatarLoaded = true;
        Debug.Log("Avatar Loaded");
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
        
        // 启用玩家控制
        SetPlayerControlsEnabled(true);
        
        Debug.Log("All resources loaded, game started!");
    }
    
    private void SetPlayerControlsEnabled(bool enabled)
    {
        if (thirdPersonController != null)
        {
            thirdPersonController.enabled = enabled;
        }
        
        if (cameraOrbit != null)
        {
            cameraOrbit.enabled = enabled;
        }
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
        }
        
        if (thirdPersonLoader != null)
        {
            thirdPersonLoader.OnLoadComplete -= OnAvatarLoadComplete;
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
}
