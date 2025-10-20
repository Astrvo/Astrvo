using UnityEngine;
using UnityEngine.UI;

public class HomeButtonHandler : MonoBehaviour
{
    [Header("按钮配置")]
    [SerializeField] private Button snekHomeButton;
    [SerializeField] private string targetUrl = "https://www.example.com"; // 默认网址，可以在Inspector中修改
    
    [Header("调试设置")]
    [SerializeField] private bool logVerbose = true;
    
    void Start()
    {
        InitializeButton();
    }
    
    private void InitializeButton()
    {
        // 如果没有手动指定按钮，尝试自动查找
        if (snekHomeButton == null)
        {
            snekHomeButton = GetComponent<Button>();
        }
        
        if (snekHomeButton == null)
        {
            Debug.LogError("Home Button Handler: Button component not found, please ensure this script is attached to a GameObject with a Button component");
            return;
        }
        
        // 添加点击事件监听器
        snekHomeButton.onClick.AddListener(OnSnekHomeButtonClick);
        
        LogVerbose("Home Button Handler initialized");
    }
    
    private void OnSnekHomeButtonClick()
    {
        LogVerbose($"Home button clicked, preparing to redirect to: {targetUrl}");
        
        if (string.IsNullOrEmpty(targetUrl))
        {
            Debug.LogWarning("Home Button Handler: Target URL is empty, cannot redirect");
            return;
        }
        
        // 使用Application.OpenURL打开网址
        Application.OpenURL(targetUrl);
        
        LogVerbose($"Attempted to open URL: {targetUrl}");
    }
    
    /// <summary>
    /// 设置目标URL（可以在运行时动态修改）
    /// </summary>
    /// <param name="url">目标网址</param>
    public void SetTargetUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            targetUrl = url;
            LogVerbose($"Target URL updated to: {targetUrl}");
        }
        else
        {
            Debug.LogWarning("HomeButtonHandler: Attempted to set empty URL");
        }
    }
    
    /// <summary>
    /// 获取当前目标URL
    /// </summary>
    /// <returns>当前目标URL</returns>
    public string GetTargetUrl()
    {
        return targetUrl;
    }
    
    private void LogVerbose(string message)
    {
        if (logVerbose)
        {
            Debug.Log($"[HomeButtonHandler] {message}");
        }
    }
    
    void OnDestroy()
    {
        // 清理事件监听器
        if (snekHomeButton != null)
        {
            snekHomeButton.onClick.RemoveListener(OnSnekHomeButtonClick);
        }
    }
}

