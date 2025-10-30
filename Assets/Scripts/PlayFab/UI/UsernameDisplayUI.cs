using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace PlayFabSystem.UI
{
    public class UsernameDisplayUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private TextMeshProUGUI usernameText; // 使用TextMeshPro显示用户名
        [SerializeField] private Button usernameButton;
        [SerializeField] private GameObject loadingIndicator;
        
        [Header("显示设置")]
        [SerializeField] private string defaultUsernameText = "Loading...";
        [SerializeField] private string loadingText = "Loading username...";
        [SerializeField] private bool showLoadingAnimation = true;
        
        [Header("TextMeshPro设置")]
        [SerializeField] private bool enableRichText = true; // 启用富文本支持
        [SerializeField] private string usernamePrefix = ""; // 用户名前缀
        [SerializeField] private string usernameSuffix = ""; // 用户名后缀
        [SerializeField] private Color usernameColor = Color.white; // 用户名颜色
        
        [Header("动画设置")]
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseScale = 1.1f;
        
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 私有变量
        private CanvasGroup canvasGroup;
        private Vector3 originalScale;
        private bool isAnimating = false;
        
        private void Awake()
        {
            // 获取或添加CanvasGroup组件
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            // 保存原始缩放
            originalScale = transform.localScale;
            
            // 初始化UI状态
            InitializeUI();
        }
        
        private void Start()
        {
            // 订阅事件
            SubscribeToEvents();
            
            // 开始加载用户名
            StartCoroutine(LoadUsernameCoroutine());
        }
        
        private void OnDestroy()
        {
            // 取消订阅事件
            UnsubscribeFromEvents();
        }
        
        private void InitializeUI()
        {
            // 设置初始状态
			if (usernameText == null)
			{
				TryAutoBindUsernameText();
			}
			if (usernameText != null)
            {
                // 设置TextMeshPro属性
                usernameText.richText = enableRichText;
                usernameText.text = defaultUsernameText;
                usernameText.color = usernameColor;
                
                // 如果TextMeshPro组件没有字体，尝试设置默认字体
                if (usernameText.font == null)
                {
                    LogDebug("TextMeshPro font not set, please ensure font resource is assigned");
                }
            }
			else
			{
				LogError("TextMeshProUGUI component not set, please drag TextMeshPro - Text (UI) component to usernameText field");
			}
            
			if (loadingIndicator != null)
			{
				loadingIndicator.SetActive(showLoadingAnimation);
			}
			
		}

		// 在引用未设置时尝试自动绑定 Username 文本组件（避免因为场景/预制体结构调整导致引用丢失）
		private void TryAutoBindUsernameText()
		{
			// 1) 优先在自身或子物体中查找
			if (usernameText == null)
			{
				usernameText = GetComponentInChildren<TextMeshProUGUI>(true);
				if (usernameText != null)
				{
					LogDebug("Auto-bound TextMeshProUGUI from children.");
					return;
				}
			}

			// 2) 在同一 Canvas 范围内查找一个命名包含 Username 的 TMP 文本
			Canvas myCanvas = GetComponentInParent<Canvas>();
			if (myCanvas != null)
			{
				var tmps = myCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
				foreach (var tmp in tmps)
				{
					if (tmp != null && tmp.name.ToLower().Contains("username"))
					{
						usernameText = tmp;
						LogDebug($"Auto-bound TextMeshProUGUI from Canvas child: {tmp.name}");
						return;
					}
				}
			}

			// 3) 兜底：全局查找第一个 TMP 文本（不推荐，但可避免阻塞流程）
			if (usernameText == null)
			{
				var any = FindObjectOfType<TextMeshProUGUI>(true);
				if (any != null)
				{
					usernameText = any;
					LogDebug($"Auto-bound TextMeshProUGUI (fallback): {any.name}");
				}
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
            UsernameManager.OnError += OnUsernameError;
        }
        
        private void UnsubscribeFromEvents()
        {
            // 取消订阅PlayFab事件
            PlayFabManager.OnUsernameChanged -= OnUsernameChanged;
            PlayFabManager.OnLoginResult -= OnLoginResult;
            PlayFabManager.OnError -= OnError;
            
            // 取消订阅用户名管理器事件
            UsernameManager.OnUsernameChanged -= OnUsernameManagerChanged;
            UsernameManager.OnError -= OnUsernameError;
        }
        
        private IEnumerator LoadUsernameCoroutine()
        {
            // 显示加载状态
            ShowLoadingState();
            
            // 等待PlayFab初始化
            yield return new WaitUntil(() => PlayFabManager.Instance != null);
            
            // 等待登录完成
            yield return new WaitUntil(() => PlayFabManager.Instance.IsLoggedIn);
            
            // 获取用户名
            string username = PlayFabManager.Instance.CurrentUsername;
            if (!string.IsNullOrEmpty(username))
            {
                UpdateUsernameDisplay(username);
            }
            else
            {
                // 如果还没有用户名，等待用户名设置
                yield return new WaitUntil(() => !string.IsNullOrEmpty(PlayFabManager.Instance.CurrentUsername));
                UpdateUsernameDisplay(PlayFabManager.Instance.CurrentUsername);
            }
        }
        
        private void ShowLoadingState()
        {
            if (usernameText != null)
            {
                usernameText.text = loadingText;
            }
            
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(showLoadingAnimation);
            }
            
            // 开始淡入动画
            StartCoroutine(FadeInCoroutine());
        }
        
        private void UpdateUsernameDisplay(string username)
        {
            if (usernameText != null)
            {
                // 设置富文本支持
                usernameText.richText = enableRichText;
                
                // 构建完整的用户名文本
                string fullUsername = BuildUsernameText(username);
                usernameText.text = fullUsername;
                
                // 设置用户名颜色
                usernameText.color = usernameColor;
            }
            
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            
            // 开始脉冲动画
            if (!isAnimating)
            {
                StartCoroutine(PulseAnimation());
            }
            
            LogDebug($"Username display updated: {username}");
        }
        
        private IEnumerator FadeInCoroutine()
        {
            if (canvasGroup == null) yield break;
            
            float elapsedTime = 0f;
            float startAlpha = canvasGroup.alpha;
            
            while (elapsedTime < fadeInDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / fadeInDuration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, progress);
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
        }
        
        private IEnumerator PulseAnimation()
        {
            isAnimating = true;
            
            while (true)
            {
                float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.1f + 1f;
                transform.localScale = originalScale * pulse;
                yield return null;
            }
        }
        
        private void OnUsernameButtonClick()
        {
            LogDebug("Username button clicked");
            
            // 这里可以添加点击用户名后的行为
            // 比如打开用户名编辑界面
            OnUsernameClicked();
        }
        
        protected virtual void OnUsernameClicked()
        {
            // 子类可以重写此方法来处理用户名点击事件
            LogDebug("Username clicked, can add custom behavior here");
        }
        
        // 事件回调方法
        private void OnUsernameChanged(string username)
        {
            UpdateUsernameDisplay(username);
        }
        
        private void OnLoginResult(bool success)
        {
            if (success)
            {
                LogDebug("Login successful, starting to load username");
            }
            else
            {
                LogError("Login failed");
                ShowErrorState("Login failed");
            }
        }
        
        private void OnError(string error)
        {
            LogError($"PlayFab error: {error}");
            ShowErrorState(error);
        }
        
        private void OnUsernameManagerChanged(string username)
        {
            UpdateUsernameDisplay(username);
        }
        
        private void OnUsernameError(string error)
        {
            LogError($"Username manager error: {error}");
            ShowErrorState(error);
        }
        
        private void ShowErrorState(string errorMessage)
        {
            if (usernameText != null)
            {
                usernameText.text = "Error";
            }
            
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            
            LogError($"Show error state: {errorMessage}");
        }
        
        /// <summary>
        /// 手动设置用户名显示
        /// </summary>
        public void SetUsernameDisplay(string username)
        {
            UpdateUsernameDisplay(username);
        }
        
        /// <summary>
        /// 显示/隐藏加载指示器
        /// </summary>
        public void SetLoadingState(bool isLoading)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(isLoading);
            }
            
            if (isLoading && usernameText != null)
            {
                usernameText.text = loadingText;
            }
        }
        
        /// <summary>
        /// 设置用户名文本大小
        /// </summary>
        public void SetUsernameSize(float size)
        {
            if (usernameText != null)
            {
                usernameText.fontSize = size;
            }
        }
        
        /// <summary>
        /// 构建完整的用户名文本（支持富文本）
        /// </summary>
        private string BuildUsernameText(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return defaultUsernameText;
            }
            
            string result = username;
            
            // 添加前缀
            if (!string.IsNullOrEmpty(usernamePrefix))
            {
                result = usernamePrefix + result;
            }
            
            // 添加后缀
            if (!string.IsNullOrEmpty(usernameSuffix))
            {
                result = result + usernameSuffix;
            }
            
            // 如果启用富文本，可以添加颜色标签
            if (enableRichText && usernameColor != Color.white)
            {
                string colorHex = ColorUtility.ToHtmlStringRGBA(usernameColor);
                result = $"<color=#{colorHex}>{result}</color>";
            }
            
            return result;
        }
        
        /// <summary>
        /// 设置用户名前缀
        /// </summary>
        public void SetUsernamePrefix(string prefix)
        {
            usernamePrefix = prefix;
            if (!string.IsNullOrEmpty(PlayFabManager.Instance?.CurrentUsername))
            {
                UpdateUsernameDisplay(PlayFabManager.Instance.CurrentUsername);
            }
        }
        
        /// <summary>
        /// 设置用户名后缀
        /// </summary>
        public void SetUsernameSuffix(string suffix)
        {
            usernameSuffix = suffix;
            if (!string.IsNullOrEmpty(PlayFabManager.Instance?.CurrentUsername))
            {
                UpdateUsernameDisplay(PlayFabManager.Instance.CurrentUsername);
            }
        }
        
        /// <summary>
        /// 设置用户名颜色
        /// </summary>
        public void SetUsernameColor(Color color)
        {
            usernameColor = color;
            if (usernameText != null)
            {
                usernameText.color = color;
            }
        }
        
        /// <summary>
        /// 启用/禁用富文本
        /// </summary>
        public void SetRichTextEnabled(bool enabled)
        {
            enableRichText = enabled;
            if (usernameText != null)
            {
                usernameText.richText = enabled;
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[UsernameDisplayUI] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[UsernameDisplayUI] {message}");
        }
    }
}
