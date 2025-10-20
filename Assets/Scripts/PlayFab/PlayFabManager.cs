using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;

namespace PlayFabSystem
{
    public class PlayFabManager : MonoBehaviour
    {
        [Header("PlayFab配置")]
        [SerializeField] private string titleId = "130209"; // 你的PlayFab Title ID
        
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 单例模式
        public static PlayFabManager Instance { get; private set; }
        
        // 事件
        public static event Action<bool> OnLoginResult;
        public static event Action<string> OnUsernameChanged;
        public static event Action<string> OnError;
        
        // 当前用户信息
        public string CurrentUsername { get; set; }
        public string PlayFabId { get; set; }
        public bool IsLoggedIn { get; set; }
        
        // 重试机制
        private int loginRetryCount = 0;
        private const int maxRetryAttempts = 3;
        
        private void Awake()
        {
            // 单例模式
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePlayFab();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void InitializePlayFab()
        {
            if (string.IsNullOrEmpty(titleId) || titleId == "YOUR_TITLE_ID")
            {
                LogError("请设置正确的PlayFab Title ID");
                return;
            }
            
            PlayFabSettings.staticSettings.TitleId = titleId;
            LogDebug($"PlayFab初始化完成，Title ID: {titleId}");
        }
        
        /// <summary>
        /// 尝试自动登录（检查本地存储的登录信息）
        /// </summary>
        public void TryAutoLogin()
        {
            // 检查是否有保存的登录信息
            string savedCustomId = PlayerPrefs.GetString("PlayFabCustomId", "");
            
            if (!string.IsNullOrEmpty(savedCustomId))
            {
                LoginWithCustomId(savedCustomId);
            }
            else
            {
                // 生成新的自定义ID并登录
                string newCustomId = GenerateCustomId();
                PlayerPrefs.SetString("PlayFabCustomId", newCustomId);
                LoginWithCustomId(newCustomId);
            }
        }
        
        /// <summary>
        /// 使用自定义ID登录
        /// </summary>
        private void LoginWithCustomId(string customId)
        {
            var request = new LoginWithCustomIDRequest
            {
                CustomId = customId,
                CreateAccount = true, // 如果账户不存在则创建
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetPlayerProfile = true,
                    GetUserAccountInfo = true
                }
            };
            
            PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
        }
        
        /// <summary>
        /// 登录成功回调
        /// </summary>
        private void OnLoginSuccess(LoginResult result)
        {
            IsLoggedIn = true;
            PlayFabId = result.PlayFabId;
            loginRetryCount = 0; // 重置重试计数
            
            LogDebug($"登录成功，PlayFab ID: {PlayFabId}");
            LogDebug($"Session Ticket: {result.SessionTicket}");
            LogDebug($"Entity Token: {result.EntityToken?.Entity?.Id}");
            
            // 获取或设置用户名
            if (result.InfoResultPayload?.PlayerProfile != null)
            {
                CurrentUsername = result.InfoResultPayload.PlayerProfile.DisplayName;
                LogDebug($"现有用户名: {CurrentUsername}");
            }
            
            if (string.IsNullOrEmpty(CurrentUsername))
            {
                // 如果没有用户名，生成一个
                LogDebug("没有用户名，开始生成新用户名");
                GenerateAndSetUsername();
            }
            else
            {
                OnLoginResult?.Invoke(true);
                OnUsernameChanged?.Invoke(CurrentUsername);
            }
        }
        
        /// <summary>
        /// 登录失败回调
        /// </summary>
        private void OnLoginFailure(PlayFabError error)
        {
            IsLoggedIn = false;
            LogError($"登录失败: {error.ErrorMessage}");
            
            // 提供具体的错误指导
            if (error.ErrorMessage.Contains("Player creations have been disabled"))
            {
                LogError("解决方案: 请在PlayFab控制台的Settings → API Features中启用'Allow Client to Create Players'");
            }
            else if (error.ErrorMessage.Contains("User not found"))
            {
                LogError("解决方案: 用户不存在，系统将尝试创建新用户。如果持续失败，请检查PlayFab控制台设置。");
                
                // 尝试重试登录
                if (loginRetryCount < maxRetryAttempts)
                {
                    loginRetryCount++;
                    LogDebug($"重试登录 (第{loginRetryCount}次尝试)");
                    
                    // 生成新的Custom ID并重试
                    string newCustomId = GenerateCustomId();
                    PlayerPrefs.SetString("PlayFabCustomId", newCustomId);
                    LoginWithCustomId(newCustomId);
                    return; // 不触发失败事件，等待重试结果
                }
                else
                {
                    LogError("登录重试次数已达上限，请检查PlayFab控制台设置");
                }
            }
            else if (error.ErrorMessage.Contains("Invalid title ID"))
            {
                LogError("解决方案: 请检查PlayFab Title ID是否正确");
            }
            else if (error.ErrorMessage.Contains("Network error"))
            {
                LogError("解决方案: 请检查网络连接");
            }
            
            OnLoginResult?.Invoke(false);
            OnError?.Invoke(error.ErrorMessage);
        }
        
        /// <summary>
        /// 生成并设置用户名
        /// </summary>
        private void GenerateAndSetUsername()
        {
            string newUsername = GenerateRandomUsername();
            SetUsername(newUsername);
        }
        
        /// <summary>
        /// 设置用户名
        /// </summary>
        public void SetUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                LogError("用户名不能为空");
                return;
            }
            
            var request = new UpdateUserTitleDisplayNameRequest
            {
                DisplayName = username
            };
            
            PlayFabClientAPI.UpdateUserTitleDisplayName(request, 
                result => {
                    CurrentUsername = username;
                    LogDebug($"用户名设置成功: {username}");
                    OnUsernameChanged?.Invoke(CurrentUsername);
                },
                error => {
                    LogError($"设置用户名失败: {error.ErrorMessage}");
                    OnError?.Invoke(error.ErrorMessage);
                });
        }
        
        /// <summary>
        /// 生成随机用户名
        /// </summary>
        private string GenerateRandomUsername()
        {
            int number = UnityEngine.Random.Range(1000, 99999);
            return $"snek{number}";
        }
        
        /// <summary>
        /// 生成自定义ID
        /// </summary>
        private string GenerateCustomId()
        {
            return $"Player_{System.Guid.NewGuid().ToString("N")[..8]}";
        }
        
        /// <summary>
        /// 获取用户数据
        /// </summary>
        public void GetUserData(string key, Action<string> onSuccess, Action<string> onError = null)
        {
            if (!IsLoggedIn)
            {
                LogError("用户未登录");
                onError?.Invoke("用户未登录");
                return;
            }
            
            var request = new GetUserDataRequest
            {
                Keys = new List<string> { key }
            };
            
            PlayFabClientAPI.GetUserData(request,
                result => {
                    if (result.Data.ContainsKey(key))
                    {
                        onSuccess?.Invoke(result.Data[key].Value);
                    }
                    else
                    {
                        onSuccess?.Invoke("");
                    }
                },
                error => {
                    LogError($"获取用户数据失败: {error.ErrorMessage}");
                    onError?.Invoke(error.ErrorMessage);
                });
        }
        
        /// <summary>
        /// 设置PlayFab Title ID
        /// </summary>
        public void SetTitleId(string newTitleId)
        {
            if (!string.IsNullOrEmpty(newTitleId))
            {
                titleId = newTitleId;
                PlayFabSettings.staticSettings.TitleId = titleId;
                LogDebug($"PlayFab Title ID已设置为: {titleId}");
            }
            else
            {
                LogError("Title ID不能为空");
            }
        }
        
        /// <summary>
        /// 清除本地数据并重新开始
        /// </summary>
        public void ClearLocalDataAndRestart()
        {
            // 清除本地存储
            PlayerPrefs.DeleteKey("PlayFabCustomId");
            PlayerPrefs.Save();
            
            // 重置状态
            IsLoggedIn = false;
            PlayFabId = "";
            CurrentUsername = "";
            loginRetryCount = 0;
            
            LogDebug("本地数据已清除，准备重新开始登录");
            
            // 重新开始登录流程
            TryAutoLogin();
        }
        
        /// <summary>
        /// 验证玩家是否在PlayFab后端存在
        /// </summary>
        public void VerifyPlayerInBackend()
        {
            if (!IsLoggedIn)
            {
                LogError("用户未登录，无法验证");
                return;
            }
            
            LogDebug("开始验证玩家是否在PlayFab后端存在...");
            
            // 获取玩家账户信息
            var request = new GetAccountInfoRequest();
            PlayFabClientAPI.GetAccountInfo(request,
                result => {
                    LogDebug("✅ 玩家在PlayFab后端存在！");
                    LogDebug($"PlayFab ID: {result.AccountInfo.PlayFabId}");
                    LogDebug($"用户名: {result.AccountInfo.Username}");
                    LogDebug($"显示名: {result.AccountInfo.TitleInfo?.DisplayName}");
                    LogDebug($"创建时间: {result.AccountInfo.Created}");
                    LogDebug($"自定义ID: {result.AccountInfo.CustomIdInfo?.CustomId}");
                    LogDebug($"账户信息获取成功，玩家确实存在于PlayFab后端！");
                },
                error => {
                    LogError($"❌ 验证失败: {error.ErrorMessage}");
                });
        }
        
        /// <summary>
        /// 设置用户数据
        /// </summary>
        public void SetUserData(string key, string value, Action onSuccess = null, Action<string> onError = null)
        {
            if (!IsLoggedIn)
            {
                LogError("用户未登录");
                onError?.Invoke("用户未登录");
                return;
            }
            
            var request = new UpdateUserDataRequest
            {
                Data = new Dictionary<string, string> { { key, value } }
            };
            
            PlayFabClientAPI.UpdateUserData(request,
                result => {
                    LogDebug($"用户数据设置成功: {key} = {value}");
                    onSuccess?.Invoke();
                },
                error => {
                    LogError($"设置用户数据失败: {error.ErrorMessage}");
                    onError?.Invoke(error.ErrorMessage);
                });
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayFabManager] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[PlayFabManager] {message}");
        }
    }
}
