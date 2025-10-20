using UnityEngine;
using UnityEngine.UI;

public class SnekHomeButtonHandler : MonoBehaviour
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
            Debug.LogError("SnekHomeButtonHandler: 未找到Button组件，请确保此脚本挂载在有Button组件的GameObject上");
            return;
        }
        
        // 添加点击事件监听器
        snekHomeButton.onClick.AddListener(OnSnekHomeButtonClick);
        
        LogVerbose("SnekHome按钮初始化完成");
    }
    
    private void OnSnekHomeButtonClick()
    {
        LogVerbose($"SnekHome按钮被点击，准备跳转到: {targetUrl}");
        
        if (string.IsNullOrEmpty(targetUrl))
        {
            Debug.LogWarning("SnekHomeButtonHandler: 目标URL为空，无法跳转");
            return;
        }
        
        // 使用Application.OpenURL打开网址
        Application.OpenURL(targetUrl);
        
        LogVerbose($"已尝试打开网址: {targetUrl}");
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
            LogVerbose($"目标URL已更新为: {targetUrl}");
        }
        else
        {
            Debug.LogWarning("SnekHomeButtonHandler: 尝试设置空URL");
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
            Debug.Log($"[SnekHomeButtonHandler] {message}");
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
