using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingUI : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private GameObject loadingAnimation;
    
    [Header("动画设置")]
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float pulseSpeed = 2f;
    
    private RectTransform loadingAnimationTransform;
    private Vector3 originalScale;
    
    void Start()
    {
        if (loadingAnimation != null)
        {
            loadingAnimationTransform = loadingAnimation.GetComponent<RectTransform>();
            if (loadingAnimationTransform != null)
            {
                originalScale = loadingAnimationTransform.localScale;
            }
        }
    }
    
    void Update()
    {
        // 旋转动画
        if (loadingAnimationTransform != null)
        {
            loadingAnimationTransform.Rotate(0, 0, -rotationSpeed * Time.deltaTime);
            
            // 脉冲动画
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * 0.1f + 1f;
            loadingAnimationTransform.localScale = originalScale * pulse;
        }
    }
    
    public void SetProgress(float progress)
    {
        if (progressBar != null)
        {
            progressBar.value = Mathf.Clamp01(progress);
        }
        
        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
        }
    }
    
    public void SetLoadingText(string text)
    {
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }
    
    public void Show()
    {
        gameObject.SetActive(true);
    }
    
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
