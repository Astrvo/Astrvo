using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace InteractionSystem
{
    /// <summary>
    /// 交互UI组件
    /// 负责显示交互提示和按钮
    /// </summary>
    public class InteractionUI : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField] private TextMeshProUGUI interactionText;
        [SerializeField] private Button interactionButton;
        [SerializeField] private GameObject multipleInteractionsPanel;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject interactionButtonPrefab;
        
        [Header("设置")]
        [SerializeField] private string defaultInteractionText = "按E键交互";
        [SerializeField] private KeyCode interactionKey = KeyCode.E;
        [SerializeField] private bool enableKeyboardInput = true;
        
        [Header("调试")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 私有变量
        private List<InteractableObject> currentInteractables = new List<InteractableObject>();
        private List<GameObject> currentButtons = new List<GameObject>();
        private InteractableObject selectedInteractable;
        
        private void Start()
        {
            SetupUI();
        }
        
        private void Update()
        {
            if (enableKeyboardInput && Input.GetKeyDown(interactionKey))
            {
                TriggerInteraction();
            }
        }
        
        private void SetupUI()
        {
            // 设置默认文本
            if (interactionText != null)
            {
                interactionText.text = defaultInteractionText;
            }
            
            // 设置按钮事件
            if (interactionButton != null)
            {
                interactionButton.onClick.AddListener(TriggerInteraction);
            }
            
            // 设置多交互面板
            if (multipleInteractionsPanel != null)
            {
                multipleInteractionsPanel.SetActive(false);
            }
        }
        
        /// <summary>
        /// 设置UI内容
        /// </summary>
        public void SetupUI(List<InteractableObject> interactables)
        {
            currentInteractables = interactables;
            
            if (interactables.Count == 0)
            {
                HideUI();
                return;
            }
            
            if (interactables.Count == 1)
            {
                ShowSingleInteraction(interactables[0]);
            }
            else
            {
                ShowMultipleInteractions(interactables);
            }
        }
        
        /// <summary>
        /// 显示单个交互
        /// </summary>
        private void ShowSingleInteraction(InteractableObject interactable)
        {
            selectedInteractable = interactable;
            
            if (interactionText != null)
            {
                // 检查是否是URL交互
                if (interactable is URLInteractable urlInteractable)
                {
                    string url = urlInteractable.GetCurrentURL();
                    interactionText.text = $"🌐 {interactable.InteractionName}\n点击打开: {url}";
                }
                else
                {
                    interactionText.text = $"{interactable.InteractionName}\n{interactable.InteractionDescription}";
                }
            }
            
            if (multipleInteractionsPanel != null)
            {
                multipleInteractionsPanel.SetActive(false);
            }
            
            gameObject.SetActive(true);
            LogDebug($"显示单个交互: {interactable.InteractionName}");
        }
        
        /// <summary>
        /// 显示多个交互
        /// </summary>
        private void ShowMultipleInteractions(List<InteractableObject> interactables)
        {
            if (multipleInteractionsPanel == null)
            {
                // 如果没有多交互面板，只显示第一个
                ShowSingleInteraction(interactables[0]);
                return;
            }
            
            multipleInteractionsPanel.SetActive(true);
            
            // 清除现有按钮
            ClearCurrentButtons();
            
            // 创建新按钮
            foreach (var interactable in interactables)
            {
                CreateInteractionButton(interactable);
            }
            
            LogDebug($"显示多个交互: {interactables.Count} 个选项");
        }
        
        /// <summary>
        /// 创建交互按钮
        /// </summary>
        private void CreateInteractionButton(InteractableObject interactable)
        {
            GameObject buttonGO;
            
            if (interactionButtonPrefab != null)
            {
                buttonGO = Instantiate(interactionButtonPrefab, buttonContainer);
            }
            else
            {
                // 创建默认按钮
                buttonGO = CreateDefaultButton();
            }
            
            // 设置按钮文本
            var buttonText = buttonGO.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                // 检查是否是URL交互
                if (interactable is URLInteractable urlInteractable)
                {
                    buttonText.text = $"🌐 {interactable.InteractionName}";
                }
                else
                {
                    buttonText.text = interactable.InteractionName;
                }
            }
            
            // 设置按钮事件
            var button = buttonGO.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnInteractionButtonClicked(interactable));
            }
            
            currentButtons.Add(buttonGO);
        }
        
        /// <summary>
        /// 创建默认按钮
        /// </summary>
        private GameObject CreateDefaultButton()
        {
            GameObject buttonGO = new GameObject("InteractionButton");
            buttonGO.transform.SetParent(buttonContainer);
            
            // 添加RectTransform
            var rectTransform = buttonGO.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 50);
            
            // 添加Image
            var image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // 添加Button
            var button = buttonGO.AddComponent<Button>();
            
            // 添加文本
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform);
            
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = "交互";
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            
            return buttonGO;
        }
        
        /// <summary>
        /// 清除当前按钮
        /// </summary>
        private void ClearCurrentButtons()
        {
            foreach (var button in currentButtons)
            {
                if (button != null)
                {
                    Destroy(button);
                }
            }
            currentButtons.Clear();
        }
        
        /// <summary>
        /// 交互按钮点击事件
        /// </summary>
        private void OnInteractionButtonClicked(InteractableObject interactable)
        {
            selectedInteractable = interactable;
            TriggerInteraction();
        }
        
        /// <summary>
        /// 触发交互
        /// </summary>
        private void TriggerInteraction()
        {
            if (selectedInteractable != null)
            {
                selectedInteractable.TriggerInteraction();
                LogDebug($"触发交互: {selectedInteractable.InteractionName}");
            }
            else if (currentInteractables.Count > 0)
            {
                // 如果没有选择特定交互，触发第一个
                currentInteractables[0].TriggerInteraction();
                LogDebug($"触发第一个交互: {currentInteractables[0].InteractionName}");
            }
        }
        
        /// <summary>
        /// 隐藏UI
        /// </summary>
        private void HideUI()
        {
            gameObject.SetActive(false);
            ClearCurrentButtons();
            selectedInteractable = null;
        }
        
        /// <summary>
        /// 设置交互键
        /// </summary>
        public void SetInteractionKey(KeyCode key)
        {
            interactionKey = key;
        }
        
        /// <summary>
        /// 设置是否启用键盘输入
        /// </summary>
        public void SetKeyboardInputEnabled(bool enabled)
        {
            enableKeyboardInput = enabled;
        }
        
        /// <summary>
        /// 更新UI文本
        /// </summary>
        public void UpdateInteractionText(string text)
        {
            if (interactionText != null)
            {
                interactionText.text = text;
            }
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[InteractionUI] {message}");
            }
        }
        
        private void OnDestroy()
        {
            ClearCurrentButtons();
        }
    }
}
