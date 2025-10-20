using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace PlayFabSystem.UI
{
    public class SettingsUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button closeButton;
        
        [Header("用户名设置")]
        [SerializeField] private TMP_InputField usernameInputField;
        [SerializeField] private Button changeUsernameButton;
        [SerializeField] private Button generateUsernameButton;
        [SerializeField] private TextMeshProUGUI currentUsernameText;
        [SerializeField] private TextMeshProUGUI usernameStatusText;
        
        [Header("登录方式设置")]
        [SerializeField] private Button walletLoginButton;
        [SerializeField] private Button googleLoginButton;
        [SerializeField] private Button facebookLoginButton;
        [SerializeField] private Button steamLoginButton;
        [SerializeField] private Button logoutButton;
        
        [Header("其他设置")]
        [SerializeField] private Button clearDataButton;
        [SerializeField] private Button aboutButton;
        
        [Header("动画设置")]
        [SerializeField] private float panelAnimationDuration = 0.3f;
        [SerializeField] private AnimationCurve panelAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 私有变量
        private bool isPanelOpen = false;
        private Vector3 originalPanelPosition;
        private CanvasGroup panelCanvasGroup;
        
        private void Awake()
        {
            // 获取或添加CanvasGroup组件
            panelCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = settingsPanel.AddComponent<CanvasGroup>();
            }
            
            // 保存原始位置
            originalPanelPosition = settingsPanel.transform.localPosition;
            
            // 初始化UI状态
            InitializeUI();
        }
        
        private void Start()
        {
            // 订阅事件
            SubscribeToEvents();
            
            // 设置初始状态
            SetPanelState(false);
        }
        
        private void OnDestroy()
        {
            // 取消订阅事件
            UnsubscribeFromEvents();
        }
        
        private void InitializeUI()
        {
            // 设置按钮事件
            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(ToggleSettingsPanel);
            }
            
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(CloseSettingsPanel);
            }
            
            if (changeUsernameButton != null)
            {
                changeUsernameButton.onClick.AddListener(OnChangeUsernameClicked);
            }
            
            if (generateUsernameButton != null)
            {
                generateUsernameButton.onClick.AddListener(OnGenerateUsernameClicked);
            }
            
            if (walletLoginButton != null)
            {
                walletLoginButton.onClick.AddListener(OnWalletLoginClicked);
            }

            if (googleLoginButton != null)
            {
                googleLoginButton.onClick.AddListener(OnGoogleLoginClicked);
            }
            
            if (facebookLoginButton != null)
            {
                facebookLoginButton.onClick.AddListener(OnFacebookLoginClicked);
            }
            
            if (steamLoginButton != null)
            {
                steamLoginButton.onClick.AddListener(OnSteamLoginClicked);
            }
            
            if (logoutButton != null)
            {
                logoutButton.onClick.AddListener(OnLogoutClicked);
            }
            
            if (clearDataButton != null)
            {
                clearDataButton.onClick.AddListener(OnClearDataClicked);
            }
            
            if (aboutButton != null)
            {
                aboutButton.onClick.AddListener(OnAboutClicked);
            }
            
            // 设置输入框事件
            if (usernameInputField != null)
            {
                usernameInputField.onValueChanged.AddListener(OnUsernameInputChanged);
                usernameInputField.onEndEdit.AddListener(OnUsernameInputEndEdit);
            }
        }
        
        private void SubscribeToEvents()
        {
            // 订阅PlayFab事件
            PlayFabManager.OnUsernameChanged += OnUsernameChanged;
            PlayFabManager.OnLoginResult += OnLoginResult;
            PlayFabManager.OnError += OnError;
            
            // 订阅用户名管理器事件
            UsernameManager.OnUsernameChanged += OnUsernameManagerChanged;
            UsernameManager.OnUsernameValidationResult += OnUsernameValidationResult;
            UsernameManager.OnError += OnUsernameError;
            
            // 订阅认证事件
            UserAuthentication.OnAuthenticationResult += OnAuthenticationResult;
            UserAuthentication.OnError += OnAuthenticationError;
        }
        
        private void UnsubscribeFromEvents()
        {
            // 取消订阅PlayFab事件
            PlayFabManager.OnUsernameChanged -= OnUsernameChanged;
            PlayFabManager.OnLoginResult -= OnLoginResult;
            PlayFabManager.OnError -= OnError;
            
            // 取消订阅用户名管理器事件
            UsernameManager.OnUsernameChanged -= OnUsernameManagerChanged;
            UsernameManager.OnUsernameValidationResult -= OnUsernameValidationResult;
            UsernameManager.OnError -= OnUsernameError;
            
            // 取消订阅认证事件
            UserAuthentication.OnAuthenticationResult -= OnAuthenticationResult;
            UserAuthentication.OnError -= OnAuthenticationError;
        }
        
        private void ToggleSettingsPanel()
        {
            if (isPanelOpen)
            {
                CloseSettingsPanel();
            }
            else
            {
                OpenSettingsPanel();
            }
        }
        
        private void OpenSettingsPanel()
        {
            if (isPanelOpen) return;
            
            isPanelOpen = true;
            settingsPanel.SetActive(true);
            
            // 更新当前用户名显示
            UpdateCurrentUsernameDisplay();
            
            // 开始打开动画
            StartCoroutine(AnimatePanelOpen());
            
            LogDebug("设置面板已打开");
        }
        
        private void CloseSettingsPanel()
        {
            if (!isPanelOpen) return;
            
            isPanelOpen = false;
            
            // 开始关闭动画
            StartCoroutine(AnimatePanelClose());
            
            LogDebug("设置面板已关闭");
        }
        
        private System.Collections.IEnumerator AnimatePanelOpen()
        {
            float elapsedTime = 0f;
            Vector3 startPosition = originalPanelPosition + Vector3.up * 100f;
            Vector3 endPosition = originalPanelPosition;
            
            while (elapsedTime < panelAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / panelAnimationDuration;
                float curveValue = panelAnimationCurve.Evaluate(progress);
                
                settingsPanel.transform.localPosition = Vector3.Lerp(startPosition, endPosition, curveValue);
                panelCanvasGroup.alpha = curveValue;
                
                yield return null;
            }
            
            settingsPanel.transform.localPosition = endPosition;
            panelCanvasGroup.alpha = 1f;
            
            // 启用交互，让按钮可以点击
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
        
        private System.Collections.IEnumerator AnimatePanelClose()
        {
            // 在动画开始时禁用交互
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
            
            float elapsedTime = 0f;
            Vector3 startPosition = originalPanelPosition;
            Vector3 endPosition = originalPanelPosition + Vector3.up * 100f;
            
            while (elapsedTime < panelAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / panelAnimationDuration;
                float curveValue = panelAnimationCurve.Evaluate(1f - progress);
                
                settingsPanel.transform.localPosition = Vector3.Lerp(startPosition, endPosition, 1f - curveValue);
                panelCanvasGroup.alpha = curveValue;
                
                yield return null;
            }
            
            settingsPanel.SetActive(false);
        }
        
        private void SetPanelState(bool isOpen)
        {
            isPanelOpen = isOpen;
            settingsPanel.SetActive(isOpen);
            
            if (isOpen)
            {
                settingsPanel.transform.localPosition = originalPanelPosition;
                panelCanvasGroup.alpha = 1f;
                // 启用交互
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
            }
            else
            {
                // 禁用交互
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }
        }
        
        private void UpdateCurrentUsernameDisplay()
        {
            if (currentUsernameText != null)
            {
                string currentUsername = PlayFabManager.Instance?.CurrentUsername ?? "Not Logged In";
                currentUsernameText.text = $"Current Name: {currentUsername}";
            }
        }
        
        // 按钮点击事件处理
        private void OnChangeUsernameClicked()
        {
            string newUsername = usernameInputField?.text?.Trim();
            
            if (string.IsNullOrEmpty(newUsername))
            {
                ShowUsernameStatus("Please enter a username", Color.red);
                return;
            }
            
            // 验证用户名
            if (UsernameManager.Instance != null)
            {
                UsernameManager.Instance.SetUsername(newUsername);
            }
            else
            {
                ShowUsernameStatus("Username manager not initialized", Color.red);
            }
        }
        
        private void OnGenerateUsernameClicked()
        {
            if (UsernameManager.Instance != null)
            {
                string generatedUsername = UsernameManager.Instance.GenerateRandomUsername();
                if (usernameInputField != null)
                {
                    usernameInputField.text = generatedUsername;
                }
                ShowUsernameStatus("Generated new username", Color.green);
            }
            else
            {
                ShowUsernameStatus("Username manager not initialized", Color.red);
            }
        }


        private void OnWalletLoginClicked()
        {
            ShowUsernameStatus("Wallet login integration still under development", Color.yellow);
            LogDebug("Wallet login button clicked");
        }
        
        private void OnGoogleLoginClicked()
        {
            ShowUsernameStatus("Google login integration still under development", Color.yellow);
            LogDebug("Google login button clicked");
        }
        
        private void OnFacebookLoginClicked()
        {
            ShowUsernameStatus("Facebook login integration still under development", Color.yellow);
            LogDebug("Facebook login button clicked");
        }
        
        private void OnSteamLoginClicked()
        {
            ShowUsernameStatus("Steam login integration still under development", Color.yellow);
            LogDebug("Steam login button clicked");
        }
        
        private void OnLogoutClicked()
        {
            if (UserAuthentication.Instance != null)
            {
                UserAuthentication.Instance.Logout();
                ShowUsernameStatus("Logged out", Color.yellow);
            }
            else
            {
                ShowUsernameStatus("Authentication manager not initialized", Color.red);
            }
        }
        
        private void OnClearDataClicked()
        {
            // 清除本地数据
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            
            ShowUsernameStatus("Local data has been cleared", Color.yellow);
            LogDebug("Local data has been cleared");
        }
        
        private void OnAboutClicked()
        {
            ShowUsernameStatus("PlayFab User System v1.0", Color.blue);
            LogDebug("About button clicked");
        }
        
        // 输入框事件处理
        private void OnUsernameInputChanged(string value)
        {
            // 可以在这里添加实时验证
        }
        
        private void OnUsernameInputEndEdit(string value)
        {
            // 输入完成时的处理
        }
        
        // 事件回调方法
        private void OnUsernameChanged(string username)
        {
            UpdateCurrentUsernameDisplay();
            ShowUsernameStatus($"Username updated: {username}", Color.green);
        }
        
        private void OnLoginResult(bool success)
        {
            if (success)
            {
                UpdateCurrentUsernameDisplay();
                ShowUsernameStatus("Login successful", Color.green);
            }
            else
            {
                ShowUsernameStatus("Login failed", Color.red);
            }
        }
        
        private void OnError(string error)
        {
            ShowUsernameStatus($"Error: {error}", Color.red);
        }
        
        private void OnUsernameManagerChanged(string username)
        {
            UpdateCurrentUsernameDisplay();
            ShowUsernameStatus($"Username updated: {username}", Color.green);
        }
        
        private void OnUsernameValidationResult(bool isValid)
        {
            if (isValid)
            {
                ShowUsernameStatus("Username format is correct", Color.green);
            }
            else
            {
                ShowUsernameStatus("Username format is incorrect", Color.red);
            }
        }
        
        private void OnUsernameError(string error)
        {
            ShowUsernameStatus($"Username error: {error}", Color.red);
        }
        
        private void OnAuthenticationResult(bool success, string message)
        {
            if (success)
            {
                ShowUsernameStatus($"Authentication successful: {message}", Color.green);
                UpdateCurrentUsernameDisplay();
            }
            else
            {
                ShowUsernameStatus($"Authentication failed: {message}", Color.red);
            }
        }
        
        private void OnAuthenticationError(string error)
        {
            ShowUsernameStatus($"Authentication error: {error}", Color.red);
        }
        
        private void ShowUsernameStatus(string message, Color color)
        {
            if (usernameStatusText != null)
            {
                usernameStatusText.text = message;
                usernameStatusText.color = color;
            }
            
            LogDebug($"Username status: {message}");
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SettingsUI] {message}");
            }
        }
    }
}
