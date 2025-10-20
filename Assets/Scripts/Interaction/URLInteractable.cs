using UnityEngine;
using System;

namespace InteractionSystem
{
    /// <summary>
    /// URL交互组件
    /// 继承自InteractableObject，专门处理URL跳转
    /// </summary>
    public class URLInteractable : InteractableObject
    {
        [Header("URL设置")]
        [SerializeField] private string targetURL = "https://www.example.com";
        [SerializeField] private bool openInNewTab = true;
        [SerializeField] private string urlTitle = "Open Link";
        
        [Header("URL验证")]
        [SerializeField] private bool validateURL = false;
        [SerializeField] private string[] allowedDomains = { };
        
        // 事件
        public static event Action<string> OnURLOpened;
        public static event Action<string> OnURLValidationFailed;
        
        private void Start()
        {
            // 设置默认交互名称
            if (string.IsNullOrEmpty(InteractionName))
            {
                SetInteractionName("Open Link");
            }
            
            if (string.IsNullOrEmpty(InteractionDescription))
            {
                SetInteractionDescription($"Click to open: {targetURL}");
            }
        }
        
        /// <summary>
        /// 重写交互触发方法
        /// </summary>
        public override void TriggerInteraction()
        {
            base.TriggerInteraction();
            
            if (ValidateURL())
            {
                OpenURL();
            }
            else
            {
                OnURLValidationFailed?.Invoke(targetURL);
                LogDebug($"URL validation failed: {targetURL}");
            }
        }
        
        /// <summary>
        /// 验证URL
        /// </summary>
        private bool ValidateURL()
        {
            if (!validateURL) return true;
            
            if (string.IsNullOrEmpty(targetURL))
            {
                LogDebug("URL is empty");
                return false;
            }
            
            // 检查URL格式
            if (!IsValidURL(targetURL))
            {
                LogDebug($"URL format invalid: {targetURL}");
                return false;
            }
            
            // 检查域名白名单
            if (allowedDomains.Length > 0)
            {
                if (!IsDomainAllowed(targetURL))
                {
                    LogDebug($"Domain not in allowed list: {targetURL}");
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 检查URL格式是否有效
        /// </summary>
        private bool IsValidURL(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 检查域名是否在白名单中
        /// </summary>
        private bool IsDomainAllowed(string url)
        {
            try
            {
                var uri = new Uri(url);
                string host = uri.Host.ToLower();
                
                foreach (string allowedDomain in allowedDomains)
                {
                    if (host == allowedDomain.ToLower() || host.EndsWith("." + allowedDomain.ToLower()))
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 打开URL
        /// </summary>
        private void OpenURL()
        {
            try
            {
                Application.OpenURL(targetURL);
                OnURLOpened?.Invoke(targetURL);
                LogDebug($"Successfully opened URL: {targetURL}");
            }
            catch (System.Exception e)
            {
                LogDebug($"Failed to open URL: {e.Message}");
            }
        }
        
        /// <summary>
        /// 设置目标URL
        /// </summary>
        public void SetTargetURL(string url)
        {
            targetURL = url;
            SetInteractionDescription($"Click to open: {url}");
        }
        
        /// <summary>
        /// 设置是否在新标签页打开
        /// </summary>
        public void SetOpenInNewTab(bool openInNew)
        {
            openInNewTab = openInNew;
        }
        
        /// <summary>
        /// 添加允许的域名
        /// </summary>
        public void AddAllowedDomain(string domain)
        {
            var domains = new string[allowedDomains.Length + 1];
            allowedDomains.CopyTo(domains, 0);
            domains[allowedDomains.Length] = domain;
            allowedDomains = domains;
        }
        
        /// <summary>
        /// 移除允许的域名
        /// </summary>
        public void RemoveAllowedDomain(string domain)
        {
            var domains = new System.Collections.Generic.List<string>(allowedDomains);
            domains.Remove(domain);
            allowedDomains = domains.ToArray();
        }
        
        /// <summary>
        /// 获取当前URL
        /// </summary>
        public string GetCurrentURL()
        {
            return targetURL;
        }
        
        /// <summary>
        /// 检查URL是否有效
        /// </summary>
        public bool IsURLValid()
        {
            return ValidateURL();
        }
    }
}
