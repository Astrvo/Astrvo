using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;

namespace PlayFabSystem
{
    public class UserAuthentication : MonoBehaviour
    {
        // 单例模式
        public static UserAuthentication Instance { get; private set; }
        [Header("认证设置")]
        [SerializeField] private bool enableGoogleLogin = true;
        [SerializeField] private bool enableFacebookLogin = true;
        [SerializeField] private bool enableSteamLogin = true;
        
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 事件
        public static event Action<bool, string> OnAuthenticationResult;
        public static event Action<string> OnError;
        
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
            PlayFabManager.OnLoginResult += OnPlayFabLoginResult;
            PlayFabManager.OnError += OnPlayFabError;
        }
        
        private void OnDestroy()
        {
            // 取消订阅
            PlayFabManager.OnLoginResult -= OnPlayFabLoginResult;
            PlayFabManager.OnError -= OnPlayFabError;
        }
        
        /// <summary>
        /// 初始化用户认证
        /// </summary>
        public void InitializeAuthentication()
        {
            LogDebug("开始用户认证初始化");
            
            // 首先尝试自动登录
            PlayFabManager.Instance.TryAutoLogin();
        }
        
        /// <summary>
        /// 使用Google登录
        /// </summary>
        public void LoginWithGoogle(string googleId, string googleToken)
        {
            if (!enableGoogleLogin)
            {
                LogError("Google登录未启用");
                return;
            }
            
            var request = new LoginWithGoogleAccountRequest
            {
                ServerAuthCode = googleToken,
                CreateAccount = true,
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetPlayerProfile = true,
                    GetUserAccountInfo = true
                }
            };
            
            PlayFabClientAPI.LoginWithGoogleAccount(request, OnGoogleLoginSuccess, OnGoogleLoginFailure);
        }
        
        /// <summary>
        /// 使用Facebook登录
        /// </summary>
        public void LoginWithFacebook(string facebookToken)
        {
            if (!enableFacebookLogin)
            {
                LogError("Facebook登录未启用");
                return;
            }
            
            var request = new LoginWithFacebookRequest
            {
                AccessToken = facebookToken,
                CreateAccount = true,
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetPlayerProfile = true,
                    GetUserAccountInfo = true
                }
            };
            
            PlayFabClientAPI.LoginWithFacebook(request, OnFacebookLoginSuccess, OnFacebookLoginFailure);
        }
        
        /// <summary>
        /// 使用Steam登录
        /// </summary>
        public void LoginWithSteam(string steamTicket)
        {
            if (!enableSteamLogin)
            {
                LogError("Steam登录未启用");
                return;
            }
            
            var request = new LoginWithSteamRequest
            {
                SteamTicket = steamTicket,
                CreateAccount = true,
                InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
                {
                    GetPlayerProfile = true,
                    GetUserAccountInfo = true
                }
            };
            
            PlayFabClientAPI.LoginWithSteam(request, OnSteamLoginSuccess, OnSteamLoginFailure);
        }
        
        /// <summary>
        /// 链接Google账户
        /// </summary>
        public void LinkGoogleAccount(string googleToken, Action<bool> onResult = null)
        {
            var request = new LinkGoogleAccountRequest
            {
                ServerAuthCode = googleToken
            };
            
            PlayFabClientAPI.LinkGoogleAccount(request,
                result => {
                    LogDebug("Google账户链接成功");
                    onResult?.Invoke(true);
                },
                error => {
                    LogError($"Google账户链接失败: {error.ErrorMessage}");
                    onResult?.Invoke(false);
                });
        }
        
        /// <summary>
        /// 链接Facebook账户
        /// </summary>
        public void LinkFacebookAccount(string facebookToken, Action<bool> onResult = null)
        {
            var request = new LinkFacebookAccountRequest
            {
                AccessToken = facebookToken
            };
            
            PlayFabClientAPI.LinkFacebookAccount(request,
                result => {
                    LogDebug("Facebook账户链接成功");
                    onResult?.Invoke(true);
                },
                error => {
                    LogError($"Facebook账户链接失败: {error.ErrorMessage}");
                    onResult?.Invoke(false);
                });
        }
        
        /// <summary>
        /// 获取账户信息
        /// </summary>
        public void GetAccountInfo(Action<GetAccountInfoResult> onResult = null)
        {
            var request = new GetAccountInfoRequest();
            
            PlayFabClientAPI.GetAccountInfo(request,
                result => {
                    LogDebug("账户信息获取成功");
                    onResult?.Invoke(result);
                },
                error => {
                    LogError($"获取账户信息失败: {error.ErrorMessage}");
                    onResult?.Invoke(null);
                });
        }
        
        /// <summary>
        /// 登出
        /// </summary>
        public void Logout()
        {
            // 清除本地存储的登录信息
            PlayerPrefs.DeleteKey("PlayFabCustomId");
            
            // 重置PlayFab管理器状态
            PlayFabManager.Instance.IsLoggedIn = false;
            PlayFabManager.Instance.PlayFabId = "";
            PlayFabManager.Instance.CurrentUsername = "";
            
            LogDebug("用户已登出");
            OnAuthenticationResult?.Invoke(false, "用户已登出");
        }
        
        // Google登录回调
        private void OnGoogleLoginSuccess(LoginResult result)
        {
            LogDebug("Google登录成功");
            HandleLoginSuccess(result);
        }
        
        private void OnGoogleLoginFailure(PlayFabError error)
        {
            LogError($"Google登录失败: {error.ErrorMessage}");
            OnError?.Invoke(error.ErrorMessage);
        }
        
        // Facebook登录回调
        private void OnFacebookLoginSuccess(LoginResult result)
        {
            LogDebug("Facebook登录成功");
            HandleLoginSuccess(result);
        }
        
        private void OnFacebookLoginFailure(PlayFabError error)
        {
            LogError($"Facebook登录失败: {error.ErrorMessage}");
            OnError?.Invoke(error.ErrorMessage);
        }
        
        // Steam登录回调
        private void OnSteamLoginSuccess(LoginResult result)
        {
            LogDebug("Steam登录成功");
            HandleLoginSuccess(result);
        }
        
        private void OnSteamLoginFailure(PlayFabError error)
        {
            LogError($"Steam登录失败: {error.ErrorMessage}");
            OnError?.Invoke(error.ErrorMessage);
        }
        
        // 处理登录成功
        private void HandleLoginSuccess(LoginResult result)
        {
            PlayFabManager.Instance.IsLoggedIn = true;
            PlayFabManager.Instance.PlayFabId = result.PlayFabId;
            
            // 获取用户名
            string username = "";
            if (result.InfoResultPayload?.PlayerProfile != null)
            {
                username = result.InfoResultPayload.PlayerProfile.DisplayName;
            }
            
            if (string.IsNullOrEmpty(username))
            {
                // 生成新用户名
                username = GenerateRandomUsername();
                PlayFabManager.Instance.SetUsername(username);
            }
            
            PlayFabManager.Instance.CurrentUsername = username;
            OnAuthenticationResult?.Invoke(true, username);
        }
        
        // PlayFab管理器事件回调
        private void OnPlayFabLoginResult(bool success)
        {
            if (success)
            {
                OnAuthenticationResult?.Invoke(true, PlayFabManager.Instance.CurrentUsername);
            }
            else
            {
                OnAuthenticationResult?.Invoke(false, "登录失败");
            }
        }
        
        private void OnPlayFabError(string error)
        {
            OnError?.Invoke(error);
        }
        
        /// <summary>
        /// 生成随机用户名
        /// </summary>
        private string GenerateRandomUsername()
        {
            int number = UnityEngine.Random.Range(1000, 99999);
            return $"snek{number}";
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[UserAuthentication] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[UserAuthentication] {message}");
        }
    }
}
