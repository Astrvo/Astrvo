using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PlayFabSystem.UI
{
    public class SimpleSettingsUICreator : MonoBehaviour
    {
        [Header("UI创建设置")]
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private bool createOnStart = true;
        [SerializeField] private bool enableDebugLogs = true;
        
        private void Start()
        {
            if (createOnStart)
            {
                CreateSimpleSettingsUI();
            }
        }
        
        /// <summary>
        /// 创建简化的Settings UI
        /// </summary>
        [ContextMenu("创建简化Settings UI")]
        public void CreateSimpleSettingsUI()
        {
            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
                if (targetCanvas == null)
                {
                    LogError("未找到Canvas，请先创建Canvas");
                    return;
                }
            }
            
            LogDebug("开始创建简化Settings UI...");
            
            // 创建主设置按钮
            CreateSettingsButton();
            
            // 创建设置面板
            CreateSettingsPanel();
            
            LogDebug("简化Settings UI创建完成！");
        }
        
        private void CreateSettingsButton()
        {
            LogDebug("创建主设置按钮...");
            
            // 创建设置按钮
            GameObject settingsButtonObj = new GameObject("SettingsButton");
            settingsButtonObj.transform.SetParent(targetCanvas.transform, false);
            
            // 添加组件
            RectTransform rectTransform = settingsButtonObj.AddComponent<RectTransform>();
            Image image = settingsButtonObj.AddComponent<Image>();
            Button button = settingsButtonObj.AddComponent<Button>();
            
            // 设置位置和大小
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(100, -50);
            rectTransform.sizeDelta = new Vector2(100, 40);
            
            // 设置样式
            image.color = new Color(0.2f, 0.4f, 0.8f, 1f);
            
            // 创建按钮文本
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(settingsButtonObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = "⚙️ 设置";
            textComponent.color = Color.white;
            textComponent.fontSize = 16;
            textComponent.alignment = TextAlignmentOptions.Center;
        }
        
        private void CreateSettingsPanel()
        {
            LogDebug("创建设置面板...");
            
            // 创建主面板
            GameObject settingsPanelObj = new GameObject("SettingsPanel");
            settingsPanelObj.transform.SetParent(targetCanvas.transform, false);
            
            // 添加组件
            RectTransform panelRect = settingsPanelObj.AddComponent<RectTransform>();
            Image panelImage = settingsPanelObj.AddComponent<Image>();
            CanvasGroup canvasGroup = settingsPanelObj.AddComponent<CanvasGroup>();
            
            // 设置面板位置和大小
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(600, 400);
            
            // 设置背景色
            panelImage.color = new Color(0, 0, 0, 0.8f);
            
            // 设置CanvasGroup
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            
            // 初始隐藏面板
            settingsPanelObj.SetActive(false);
            
            // 创建面板内容
            CreatePanelContent(settingsPanelObj);
        }
        
        private void CreatePanelContent(GameObject parent)
        {
            LogDebug("创建面板内容...");
            
            // 创建关闭按钮
            CreateCloseButton(parent);
            
            // 创建用户名设置区域
            CreateUsernameSection(parent);
            
            // 创建登录方式区域
            CreateLoginSection(parent);
            
            // 创建其他设置区域
            CreateOtherSection(parent);
        }
        
        private void CreateCloseButton(GameObject parent)
        {
            LogDebug("创建关闭按钮...");
            
            GameObject closeButtonObj = CreateSimpleButton("CloseButton", "✕");
            closeButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform closeButtonRect = closeButtonObj.GetComponent<RectTransform>();
            closeButtonRect.anchorMin = new Vector2(1, 1);
            closeButtonRect.anchorMax = new Vector2(1, 1);
            closeButtonRect.anchoredPosition = new Vector2(-30, -30);
            closeButtonRect.sizeDelta = new Vector2(40, 40);
            
            Image buttonImage = closeButtonObj.GetComponent<Image>();
            buttonImage.color = Color.red;
        }
        
        private void CreateUsernameSection(GameObject parent)
        {
            LogDebug("创建用户名设置区域...");
            
            // 当前用户名显示
            GameObject currentUsernameTextObj = CreateSimpleText("CurrentUsernameText", "当前用户名: 未登录");
            currentUsernameTextObj.transform.SetParent(parent.transform, false);
            
            RectTransform currentUsernameRect = currentUsernameTextObj.GetComponent<RectTransform>();
            currentUsernameRect.anchorMin = new Vector2(0.5f, 1);
            currentUsernameRect.anchorMax = new Vector2(0.5f, 1);
            currentUsernameRect.anchoredPosition = new Vector2(0, -50);
            currentUsernameRect.sizeDelta = new Vector2(400, 30);
            
            // 用户名输入框 - 使用简单的Text代替InputField
            GameObject usernameInputObj = CreateSimpleText("UsernameInputField", "输入新用户名");
            usernameInputObj.transform.SetParent(parent.transform, false);
            
            RectTransform usernameInputRect = usernameInputObj.GetComponent<RectTransform>();
            usernameInputRect.anchorMin = new Vector2(0.5f, 0.5f);
            usernameInputRect.anchorMax = new Vector2(0.5f, 0.5f);
            usernameInputRect.anchoredPosition = new Vector2(-100, 50);
            usernameInputRect.sizeDelta = new Vector2(200, 40);
            
            // 添加背景
            Image inputBg = usernameInputObj.AddComponent<Image>();
            inputBg.color = new Color(1, 1, 1, 0.1f);
            
            // 修改文本样式
            TextMeshProUGUI inputText = usernameInputObj.GetComponent<TextMeshProUGUI>();
            inputText.color = new Color(1, 1, 1, 0.5f);
            inputText.alignment = TextAlignmentOptions.Left;
            
            // 修改用户名输入框
            usernameInputObj.name = "UsernameInputField";
            
            // 修改用户名按钮
            GameObject changeUsernameButtonObj = CreateSimpleButton("ChangeUsernameButton", "修改");
            changeUsernameButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform changeUsernameRect = changeUsernameButtonObj.GetComponent<RectTransform>();
            changeUsernameRect.anchorMin = new Vector2(0.5f, 0.5f);
            changeUsernameRect.anchorMax = new Vector2(0.5f, 0.5f);
            changeUsernameRect.anchoredPosition = new Vector2(120, 50);
            changeUsernameRect.sizeDelta = new Vector2(80, 40);
            
            // 生成用户名按钮
            GameObject generateUsernameButtonObj = CreateSimpleButton("GenerateUsernameButton", "生成随机用户名");
            generateUsernameButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform generateUsernameRect = generateUsernameButtonObj.GetComponent<RectTransform>();
            generateUsernameRect.anchorMin = new Vector2(0.5f, 0.5f);
            generateUsernameRect.anchorMax = new Vector2(0.5f, 0.5f);
            generateUsernameRect.anchoredPosition = new Vector2(0, 0);
            generateUsernameRect.sizeDelta = new Vector2(200, 40);
            
            // 状态文本
            GameObject usernameStatusTextObj = CreateSimpleText("UsernameStatusText", "准备就绪");
            usernameStatusTextObj.transform.SetParent(parent.transform, false);
            
            RectTransform usernameStatusRect = usernameStatusTextObj.GetComponent<RectTransform>();
            usernameStatusRect.anchorMin = new Vector2(0.5f, 0.5f);
            usernameStatusRect.anchorMax = new Vector2(0.5f, 0.5f);
            usernameStatusRect.anchoredPosition = new Vector2(0, -50);
            usernameStatusRect.sizeDelta = new Vector2(400, 30);
        }
        
        private void CreateLoginSection(GameObject parent)
        {
            LogDebug("创建登录方式区域...");
            
            // Google登录按钮
            GameObject googleLoginButtonObj = CreateSimpleButton("GoogleLoginButton", "Google登录");
            googleLoginButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform googleLoginRect = googleLoginButtonObj.GetComponent<RectTransform>();
            googleLoginRect.anchorMin = new Vector2(0, 0.5f);
            googleLoginRect.anchorMax = new Vector2(0, 0.5f);
            googleLoginRect.anchoredPosition = new Vector2(100, -100);
            googleLoginRect.sizeDelta = new Vector2(120, 40);
            
            // Facebook登录按钮
            GameObject facebookLoginButtonObj = CreateSimpleButton("FacebookLoginButton", "Facebook登录");
            facebookLoginButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform facebookLoginRect = facebookLoginButtonObj.GetComponent<RectTransform>();
            facebookLoginRect.anchorMin = new Vector2(0, 0.5f);
            facebookLoginRect.anchorMax = new Vector2(0, 0.5f);
            facebookLoginRect.anchoredPosition = new Vector2(100, -150);
            facebookLoginRect.sizeDelta = new Vector2(120, 40);
            
            // Steam登录按钮
            GameObject steamLoginButtonObj = CreateSimpleButton("SteamLoginButton", "Steam登录");
            steamLoginButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform steamLoginRect = steamLoginButtonObj.GetComponent<RectTransform>();
            steamLoginRect.anchorMin = new Vector2(0, 0.5f);
            steamLoginRect.anchorMax = new Vector2(0, 0.5f);
            steamLoginRect.anchoredPosition = new Vector2(100, -200);
            steamLoginRect.sizeDelta = new Vector2(120, 40);
            
            // 登出按钮
            GameObject logoutButtonObj = CreateSimpleButton("LogoutButton", "登出");
            logoutButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform logoutRect = logoutButtonObj.GetComponent<RectTransform>();
            logoutRect.anchorMin = new Vector2(0, 0.5f);
            logoutRect.anchorMax = new Vector2(0, 0.5f);
            logoutRect.anchoredPosition = new Vector2(100, -250);
            logoutRect.sizeDelta = new Vector2(120, 40);
        }
        
        private void CreateOtherSection(GameObject parent)
        {
            LogDebug("创建其他设置区域...");
            
            // 清除数据按钮
            GameObject clearDataButtonObj = CreateSimpleButton("ClearDataButton", "清除本地数据");
            clearDataButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform clearDataRect = clearDataButtonObj.GetComponent<RectTransform>();
            clearDataRect.anchorMin = new Vector2(1, 0.5f);
            clearDataRect.anchorMax = new Vector2(1, 0.5f);
            clearDataRect.anchoredPosition = new Vector2(-100, -100);
            clearDataRect.sizeDelta = new Vector2(120, 40);
            
            // 关于按钮
            GameObject aboutButtonObj = CreateSimpleButton("AboutButton", "关于");
            aboutButtonObj.transform.SetParent(parent.transform, false);
            
            RectTransform aboutRect = aboutButtonObj.GetComponent<RectTransform>();
            aboutRect.anchorMin = new Vector2(1, 0.5f);
            aboutRect.anchorMax = new Vector2(1, 0.5f);
            aboutRect.anchoredPosition = new Vector2(-100, -150);
            aboutRect.sizeDelta = new Vector2(120, 40);
        }
        
        private GameObject CreateSimpleButton(string name, string text)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.AddComponent<RectTransform>();
            buttonObj.AddComponent<Image>();
            buttonObj.AddComponent<Button>();
            
            // 创建按钮文本
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            textObj.AddComponent<RectTransform>();
            
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.color = Color.white;
            textComponent.fontSize = 16;
            textComponent.alignment = TextAlignmentOptions.Center;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            return buttonObj;
        }
        
        private GameObject CreateSimpleText(string name, string text)
        {
            GameObject textObj = new GameObject(name);
            textObj.AddComponent<RectTransform>();
            
            TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.color = Color.white;
            textComponent.fontSize = 16;
            textComponent.alignment = TextAlignmentOptions.Center;
            
            return textObj;
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SimpleSettingsUICreator] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[SimpleSettingsUICreator] {message}");
        }
    }
}
