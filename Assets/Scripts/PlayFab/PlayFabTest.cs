using UnityEngine;
using PlayFabSystem;

namespace PlayFabSystem
{
    public class PlayFabTest : MonoBehaviour
    {
        [Header("测试设置")]
        [SerializeField] private bool runTestOnStart = false;
        [SerializeField] private bool enableDebugLogs = true;
        
        private void Start()
        {
            if (runTestOnStart)
            {
                StartCoroutine(RunTestAfterDelay());
            }
        }
        
        private System.Collections.IEnumerator RunTestAfterDelay()
        {
            // 等待系统初始化
            yield return new WaitForSeconds(2f);
            
            // 运行测试
            RunSystemTest();
        }
        
        /// <summary>
        /// 运行系统测试
        /// </summary>
        [ContextMenu("运行系统测试")]
        public void RunSystemTest()
        {
            LogDebug("开始PlayFab系统测试");
            
            // 测试PlayFab管理器
            TestPlayFabManager();
            
            // 测试用户认证
            TestUserAuthentication();
            
            // 测试用户名管理器
            TestUsernameManager();
            
            LogDebug("PlayFab系统测试完成");
        }
        
        private void TestPlayFabManager()
        {
            LogDebug("测试PlayFab管理器...");
            
            if (PlayFabManager.Instance != null)
            {
                LogDebug($"PlayFab管理器状态: 已加载");
                LogDebug($"登录状态: {PlayFabManager.Instance.IsLoggedIn}");
                LogDebug($"当前用户名: {PlayFabManager.Instance.CurrentUsername}");
                LogDebug($"PlayFab ID: {PlayFabManager.Instance.PlayFabId}");
            }
            else
            {
                LogError("PlayFab管理器未找到");
            }
        }
        
        private void TestUserAuthentication()
        {
            LogDebug("测试用户认证...");
            
            if (UserAuthentication.Instance != null)
            {
                LogDebug("用户认证管理器状态: 已加载");
            }
            else
            {
                LogError("用户认证管理器未找到");
            }
        }
        
        private void TestUsernameManager()
        {
            LogDebug("测试用户名管理器...");
            
            if (UsernameManager.Instance != null)
            {
                LogDebug("用户名管理器状态: 已加载");
                LogDebug($"当前用户名: {UsernameManager.Instance.GetCurrentUsername()}");
                
                // 测试生成随机用户名
                string randomUsername = UsernameManager.Instance.GenerateRandomUsername();
                LogDebug($"生成的随机用户名: {randomUsername}");
            }
            else
            {
                LogError("用户名管理器未找到");
            }
        }
        
        /// <summary>
        /// 测试用户名设置
        /// </summary>
        [ContextMenu("测试用户名设置")]
        public void TestUsernameSetting()
        {
            if (UsernameManager.Instance != null)
            {
                string testUsername = "TestUser" + Random.Range(100, 999);
                LogDebug($"测试设置用户名: {testUsername}");
                UsernameManager.Instance.SetUsername(testUsername);
            }
            else
            {
                LogError("用户名管理器未找到");
            }
        }
        
        /// <summary>
        /// 测试用户数据存储
        /// </summary>
        [ContextMenu("测试用户数据存储")]
        public void TestUserDataStorage()
        {
            if (PlayFabManager.Instance != null && PlayFabManager.Instance.IsLoggedIn)
            {
                string testKey = "TestData";
                string testValue = "TestValue" + Random.Range(100, 999);
                
                LogDebug($"测试存储用户数据: {testKey} = {testValue}");
                
                PlayFabManager.Instance.SetUserData(testKey, testValue,
                    () => {
                        LogDebug("用户数据存储成功");
                        
                        // 测试获取用户数据
                        PlayFabManager.Instance.GetUserData(testKey,
                            value => LogDebug($"获取用户数据成功: {value}"),
                            error => LogError($"获取用户数据失败: {error}"));
                    },
                    error => LogError($"存储用户数据失败: {error}"));
            }
            else
            {
                LogError("PlayFab管理器未登录或未找到");
            }
        }
        
        /// <summary>
        /// 显示系统状态
        /// </summary>
        [ContextMenu("显示系统状态")]
        public void ShowSystemStatus()
        {
            LogDebug("=== PlayFab系统状态 ===");
            
            // PlayFab管理器状态
            if (PlayFabManager.Instance != null)
            {
                LogDebug($"PlayFab管理器: 已加载");
                LogDebug($"登录状态: {PlayFabManager.Instance.IsLoggedIn}");
                LogDebug($"当前用户名: {PlayFabManager.Instance.CurrentUsername}");
                LogDebug($"PlayFab ID: {PlayFabManager.Instance.PlayFabId}");
            }
            else
            {
                LogError("PlayFab管理器: 未加载");
            }
            
            // 用户认证状态
            if (UserAuthentication.Instance != null)
            {
                LogDebug("用户认证管理器: 已加载");
            }
            else
            {
                LogError("用户认证管理器: 未加载");
            }
            
            // 用户名管理器状态
            if (UsernameManager.Instance != null)
            {
                LogDebug("用户名管理器: 已加载");
                LogDebug($"当前用户名: {UsernameManager.Instance.GetCurrentUsername()}");
            }
            else
            {
                LogError("用户名管理器: 未加载");
            }
            
            LogDebug("=== 系统状态结束 ===");
        }
        
        /// <summary>
        /// 清除本地数据并重新开始
        /// </summary>
        [ContextMenu("清除数据并重新开始")]
        public void ClearDataAndRestart()
        {
            if (PlayFabManager.Instance != null)
            {
                PlayFabManager.Instance.ClearLocalDataAndRestart();
                LogDebug("已清除本地数据并重新开始登录");
            }
            else
            {
                LogError("PlayFab管理器未找到");
            }
        }
        
        /// <summary>
        /// 验证玩家是否在PlayFab后端存在
        /// </summary>
        [ContextMenu("验证玩家在PlayFab后端")]
        public void VerifyPlayerInBackend()
        {
            if (PlayFabManager.Instance != null)
            {
                PlayFabManager.Instance.VerifyPlayerInBackend();
            }
            else
            {
                LogError("PlayFab管理器未找到");
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayFabTest] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[PlayFabTest] {message}");
        }
    }
}
