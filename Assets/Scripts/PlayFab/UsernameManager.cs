using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;

namespace PlayFabSystem
{
    public class UsernameManager : MonoBehaviour
    {
        // 单例模式
        public static UsernameManager Instance { get; private set; }
        [Header("用户名设置")]
        [SerializeField] private int maxUsernameLength = 20;
        [SerializeField] private int minUsernameLength = 3;
        [SerializeField] private bool allowSpecialCharacters = false;
        
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 事件
        public static event Action<string> OnUsernameChanged;
        public static event Action<bool> OnUsernameValidationResult;
        public static event Action<string> OnError;
        
        // 当前用户名
        public string CurrentUsername { get; private set; }
        
        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void Start()
        {
            // 订阅PlayFab管理器事件
            PlayFabManager.OnUsernameChanged += OnPlayFabUsernameChanged;
            PlayFabManager.OnError += OnPlayFabError;
        }
        
        private void OnDestroy()
        {
            // 取消订阅
            PlayFabManager.OnUsernameChanged -= OnPlayFabUsernameChanged;
            PlayFabManager.OnError -= OnPlayFabError;
        }
        
        /// <summary>
        /// 设置用户名
        /// </summary>
        public void SetUsername(string newUsername)
        {
            if (string.IsNullOrEmpty(newUsername))
            {
                LogError("用户名不能为空");
                OnError?.Invoke("用户名不能为空");
                return;
            }
            
            // 验证用户名
            if (!ValidateUsername(newUsername))
            {
                return;
            }
            
            // 检查用户名是否已存在
            CheckUsernameAvailability(newUsername, (isAvailable) => {
                if (isAvailable)
                {
                    // 用户名可用，设置用户名
                    PlayFabManager.Instance.SetUsername(newUsername);
                }
                else
                {
                    LogError("用户名已被使用");
                    OnError?.Invoke("用户名已被使用，请选择其他用户名");
                }
            });
        }
        
        /// <summary>
        /// 生成随机用户名
        /// </summary>
        public string GenerateRandomUsername()
        {
            int number = UnityEngine.Random.Range(1000, 99999);
            return $"snek{number}";
        }
        
        /// <summary>
        /// 验证用户名格式
        /// </summary>
        public bool ValidateUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                LogError("用户名不能为空");
                OnError?.Invoke("用户名不能为空");
                OnUsernameValidationResult?.Invoke(false);
                return false;
            }
            
            if (username.Length < minUsernameLength)
            {
                LogError($"用户名长度不能少于{minUsernameLength}个字符");
                OnError?.Invoke($"用户名长度不能少于{minUsernameLength}个字符");
                OnUsernameValidationResult?.Invoke(false);
                return false;
            }
            
            if (username.Length > maxUsernameLength)
            {
                LogError($"用户名长度不能超过{maxUsernameLength}个字符");
                OnError?.Invoke($"用户名长度不能超过{maxUsernameLength}个字符");
                OnUsernameValidationResult?.Invoke(false);
                return false;
            }
            
            // 检查特殊字符
            if (!allowSpecialCharacters)
            {
                foreach (char c in username)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        LogError("用户名只能包含字母和数字");
                        OnError?.Invoke("用户名只能包含字母和数字");
                        OnUsernameValidationResult?.Invoke(false);
                        return false;
                    }
                }
            }
            
            // 检查是否包含不当词汇（简单检查）
            if (ContainsInappropriateWords(username))
            {
                LogError("用户名包含不当词汇");
                OnError?.Invoke("用户名包含不当词汇，请选择其他用户名");
                OnUsernameValidationResult?.Invoke(false);
                return false;
            }
            
            OnUsernameValidationResult?.Invoke(true);
            return true;
        }
        
        /// <summary>
        /// 检查用户名可用性
        /// </summary>
        public void CheckUsernameAvailability(string username, Action<bool> onResult)
        {
            if (!PlayFabManager.Instance.IsLoggedIn)
            {
                LogError("用户未登录");
                onResult?.Invoke(false);
                return;
            }
            
            var request = new GetAccountInfoRequest();
            
            PlayFabClientAPI.GetAccountInfo(request,
                result => {
                    // 这里可以添加更复杂的用户名检查逻辑
                    // 目前简单返回true，表示可用
                    LogDebug($"用户名 '{username}' 可用性检查完成");
                    onResult?.Invoke(true);
                },
                error => {
                    LogError($"检查用户名可用性失败: {error.ErrorMessage}");
                    onResult?.Invoke(false);
                });
        }
        
        /// <summary>
        /// 获取当前用户名
        /// </summary>
        public string GetCurrentUsername()
        {
            return CurrentUsername;
        }
        
        /// <summary>
        /// 保存用户名到本地
        /// </summary>
        public void SaveUsernameLocally(string username)
        {
            PlayerPrefs.SetString("SavedUsername", username);
            PlayerPrefs.Save();
            LogDebug($"用户名已保存到本地: {username}");
        }
        
        /// <summary>
        /// 从本地加载用户名
        /// </summary>
        public string LoadUsernameFromLocal()
        {
            string savedUsername = PlayerPrefs.GetString("SavedUsername", "");
            LogDebug($"从本地加载用户名: {savedUsername}");
            return savedUsername;
        }
        
        /// <summary>
        /// 清除本地保存的用户名
        /// </summary>
        public void ClearLocalUsername()
        {
            PlayerPrefs.DeleteKey("SavedUsername");
            PlayerPrefs.Save();
            LogDebug("本地用户名已清除");
        }
        
        /// <summary>
        /// 检查是否包含不当词汇
        /// </summary>
        private bool ContainsInappropriateWords(string username)
        {
            string[] inappropriateWords = { 
                "admin", "administrator", "moderator", "mod", "staff", "support",
                "playfab", "unity", "system", "root", "guest", "user", "player"
            };
            
            string lowerUsername = username.ToLower();
            
            foreach (string word in inappropriateWords)
            {
                if (lowerUsername.Contains(word))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 生成用户名建议
        /// </summary>
        public List<string> GenerateUsernameSuggestions(string baseName = "")
        {
            List<string> suggestions = new List<string>();
            
            if (!string.IsNullOrEmpty(baseName))
            {
                // 基于用户输入生成建议
                suggestions.Add($"{baseName}{UnityEngine.Random.Range(100, 999)}");
                suggestions.Add($"{baseName}Pro{UnityEngine.Random.Range(10, 99)}");
                suggestions.Add($"{baseName}X{UnityEngine.Random.Range(100, 999)}");
            }
            
            // 添加随机生成的建议
            for (int i = 0; i < 5; i++)
            {
                suggestions.Add(GenerateRandomUsername());
            }
            
            return suggestions;
        }
        
        // PlayFab事件回调
        private void OnPlayFabUsernameChanged(string username)
        {
            CurrentUsername = username;
            SaveUsernameLocally(username);
            OnUsernameChanged?.Invoke(username);
            LogDebug($"用户名已更新: {username}");
        }
        
        private void OnPlayFabError(string error)
        {
            OnError?.Invoke(error);
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[UsernameManager] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[UsernameManager] {message}");
        }
    }
}
