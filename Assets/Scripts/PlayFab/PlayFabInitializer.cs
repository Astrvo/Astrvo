using UnityEngine;
using PlayFabSystem;
using PlayFabSystem.UI;

namespace PlayFabSystem
{
    public class PlayFabInitializer : MonoBehaviour
    {
        [Header("PlayFab配置")]
        [SerializeField] private string playFabTitleId = "130209"; // 需要在PlayFab控制台获取
        
        [Header("UI引用")]
        [SerializeField] private UsernameDisplayUI usernameDisplayUI;
        [SerializeField] private SettingsUI settingsUI;
        
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool autoInitialize = true;
        
        // 组件引用
        private PlayFabManager playFabManager;
        private UserAuthentication userAuthentication;
        private UsernameManager usernameManager;
        
        private void Awake()
        {
            // 确保只有一个初始化器
            if (FindObjectsOfType<PlayFabInitializer>().Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            if (autoInitialize)
            {
                InitializePlayFabSystem();
            }
        }
        
        /// <summary>
        /// 初始化PlayFab系统
        /// </summary>
        public void InitializePlayFabSystem()
        {
            LogDebug("开始初始化PlayFab系统");
            
            // 创建PlayFab管理器
            CreatePlayFabManager();
            
            // 创建用户认证管理器
            CreateUserAuthentication();
            
            // 创建用户名管理器
            CreateUsernameManager();
            
            // 初始化UI
            InitializeUI();
            
            // 开始认证流程
            StartAuthentication();
            
            LogDebug("PlayFab系统初始化完成");
        }
        
        private void CreatePlayFabManager()
        {
            // 查找现有的PlayFab管理器
            playFabManager = FindObjectOfType<PlayFabManager>();
            
            if (playFabManager == null)
            {
                // 创建新的PlayFab管理器
                GameObject managerObject = new GameObject("PlayFabManager");
                playFabManager = managerObject.AddComponent<PlayFabManager>();
                
                // 设置Title ID
                if (!string.IsNullOrEmpty(playFabTitleId) && playFabTitleId != "YOUR_TITLE_ID")
                {
                    playFabManager.SetTitleId(playFabTitleId);
                }
                else
                {
                    LogDebug("使用PlayFabManager中的默认Title ID");
                }
                
                DontDestroyOnLoad(managerObject);
                LogDebug("PlayFab管理器已创建");
            }
            else
            {
                LogDebug("使用现有的PlayFab管理器");
            }
        }
        
        private void CreateUserAuthentication()
        {
            // 查找现有的用户认证管理器
            userAuthentication = FindObjectOfType<UserAuthentication>();
            
            if (userAuthentication == null)
            {
                // 创建新的用户认证管理器
                GameObject authObject = new GameObject("UserAuthentication");
                userAuthentication = authObject.AddComponent<UserAuthentication>();
                DontDestroyOnLoad(authObject);
                LogDebug("用户认证管理器已创建");
            }
            else
            {
                LogDebug("使用现有的用户认证管理器");
            }
        }
        
        private void CreateUsernameManager()
        {
            // 查找现有的用户名管理器
            usernameManager = FindObjectOfType<UsernameManager>();
            
            if (usernameManager == null)
            {
                // 创建新的用户名管理器
                GameObject usernameObject = new GameObject("UsernameManager");
                usernameManager = usernameObject.AddComponent<UsernameManager>();
                DontDestroyOnLoad(usernameObject);
                LogDebug("用户名管理器已创建");
            }
            else
            {
                LogDebug("使用现有的用户名管理器");
            }
        }
        
        private void InitializeUI()
        {
            // 初始化用户名显示UI
            if (usernameDisplayUI != null)
            {
                LogDebug("用户名显示UI已设置");
            }
            else
            {
                LogDebug("警告: 用户名显示UI未设置");
            }
            
            // 初始化设置UI
            if (settingsUI != null)
            {
                LogDebug("设置UI已设置");
            }
            else
            {
                LogDebug("警告: 设置UI未设置");
            }
        }
        
        private void StartAuthentication()
        {
            if (userAuthentication != null)
            {
                userAuthentication.InitializeAuthentication();
                LogDebug("开始用户认证流程");
            }
            else
            {
                LogError("用户认证管理器未找到，无法开始认证流程");
            }
        }
        
        /// <summary>
        /// 设置PlayFab Title ID
        /// </summary>
        public void SetPlayFabTitleId(string titleId)
        {
            playFabTitleId = titleId;
            LogDebug($"PlayFab Title ID已设置为: {titleId}");
        }
        
        /// <summary>
        /// 获取PlayFab管理器
        /// </summary>
        public PlayFabManager GetPlayFabManager()
        {
            return playFabManager;
        }
        
        /// <summary>
        /// 获取用户认证管理器
        /// </summary>
        public UserAuthentication GetUserAuthentication()
        {
            return userAuthentication;
        }
        
        /// <summary>
        /// 获取用户名管理器
        /// </summary>
        public UsernameManager GetUsernameManager()
        {
            return usernameManager;
        }
        
        /// <summary>
        /// 重新初始化系统
        /// </summary>
        public void ReinitializeSystem()
        {
            LogDebug("重新初始化PlayFab系统");
            
            // 清理现有组件
            if (playFabManager != null)
            {
                Destroy(playFabManager.gameObject);
            }
            if (userAuthentication != null)
            {
                Destroy(userAuthentication.gameObject);
            }
            if (usernameManager != null)
            {
                Destroy(usernameManager.gameObject);
            }
            
            // 重新初始化
            InitializePlayFabSystem();
        }
        
        /// <summary>
        /// 检查系统状态
        /// </summary>
        public bool IsSystemReady()
        {
            return playFabManager != null && 
                   userAuthentication != null && 
                   usernameManager != null;
        }
        
        /// <summary>
        /// 获取系统状态信息
        /// </summary>
        public string GetSystemStatus()
        {
            string status = "PlayFab系统状态:\n";
            status += $"PlayFab管理器: {(playFabManager != null ? "已加载" : "未加载")}\n";
            status += $"用户认证: {(userAuthentication != null ? "已加载" : "未加载")}\n";
            status += $"用户名管理器: {(usernameManager != null ? "已加载" : "未加载")}\n";
            status += $"用户名显示UI: {(usernameDisplayUI != null ? "已设置" : "未设置")}\n";
            status += $"设置UI: {(settingsUI != null ? "已设置" : "未设置")}\n";
            
            if (playFabManager != null)
            {
                status += $"登录状态: {(playFabManager.IsLoggedIn ? "已登录" : "未登录")}\n";
                status += $"当前用户名: {playFabManager.CurrentUsername}\n";
            }
            
            return status;
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayFabInitializer] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[PlayFabInitializer] {message}");
        }
        
        // 在编辑器中显示系统状态
        [ContextMenu("显示系统状态")]
        private void ShowSystemStatus()
        {
            Debug.Log(GetSystemStatus());
        }
        
        [ContextMenu("重新初始化系统")]
        private void ReinitializeSystemContextMenu()
        {
            ReinitializeSystem();
        }
    }
}
