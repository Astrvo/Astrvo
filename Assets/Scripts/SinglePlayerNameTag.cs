using UnityEngine;
using TMPro;
using System.Collections;
using PlayFabSystem; // For UsernameManager and PlayFabManager

/// <summary>
/// Single Player Name Tag - Displays username on top of player in Single Player mode.
/// This is a simplified version of PlayerNameTag.cs without any FishNet networking dependencies.
/// </summary>
public class SinglePlayerNameTag : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Canvas nameCanvas;
    
    [Header("Display Settings")]
    [SerializeField] private float nameTagHeight = 2.5f; // Height above player
    [SerializeField] private bool lookAtCamera = true; // Always face camera
    [SerializeField] private Color nameColor = Color.white;
    [SerializeField] private int fontSize = 24;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private Camera _mainCamera;
    private Transform _nameTagTransform;
    
    // Performance optimization: check camera less frequently
    private float _cameraCheckInterval = 0.5f;
    private float _lastCameraCheckTime = 0f;

    private void Start()
    {
        LogDebug("Initializing SinglePlayerNameTag...");
        
        InitializeNameTag();
        
        // Subscribe to PlayFab username events
        SubscribeToEvents();
        
        // Set initial name
        SetPlayerName();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (UsernameManager.Instance != null)
        {
            UsernameManager.OnUsernameChanged += OnUsernameChanged;
        }
        
        if (PlayFabManager.Instance != null)
        {
            PlayFabManager.OnUsernameChanged += OnUsernameChanged;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (UsernameManager.Instance != null)
        {
            UsernameManager.OnUsernameChanged -= OnUsernameChanged;
        }
        
        if (PlayFabManager.Instance != null)
        {
            PlayFabManager.OnUsernameChanged -= OnUsernameChanged;
        }
    }

    /// <summary>
    /// Callback when username changes in PlayFab systems
    /// </summary>
    private void OnUsernameChanged(string newName)
    {
        LogDebug($"Username changed event received: {newName}");
        UpdateNameDisplay(newName);
    }

    /// <summary>
    /// Attempts to get the current username from available sources
    /// </summary>
    private void SetPlayerName()
    {
        string username = GetUsername();
        LogDebug($"Initial username retrieval: {(string.IsNullOrEmpty(username) ? "null/empty" : username)}");

        if (!string.IsNullOrEmpty(username))
        {
            UpdateNameDisplay(username);
        }
        else
        {
            UpdateNameDisplay("Loading...");
            // Keep trying if not found immediately
            StartCoroutine(WaitForUsername());
        }
    }

    private IEnumerator WaitForUsername()
    {
        float timeout = 10f;
        float elapsed = 0f;
        float checkInterval = 0.5f;

        while (elapsed < timeout)
        {
            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;

            string username = GetUsername();
            if (!string.IsNullOrEmpty(username))
            {
                UpdateNameDisplay(username);
                LogDebug($"Username found after waiting: {username}");
                yield break;
            }
        }

        LogDebug("Username retrieval timed out. Using default.");
        UpdateNameDisplay("Player");
    }

    private string GetUsername()
    {
        // 1. Check PlayFabManager
        if (PlayFabManager.Instance != null && !string.IsNullOrEmpty(PlayFabManager.Instance.CurrentUsername))
        {
            return PlayFabManager.Instance.CurrentUsername;
        }
        
        // 2. Check UsernameManager
        if (UsernameManager.Instance != null)
        {
            string managerName = UsernameManager.Instance.GetCurrentUsername();
            if (!string.IsNullOrEmpty(managerName))
            {
                return managerName;
            }
        }

        // 3. Check PlayerPrefs
        string savedUsername = PlayerPrefs.GetString("SavedUsername", "");
        if (!string.IsNullOrEmpty(savedUsername))
        {
            return savedUsername;
        }

        return null;
    }

    private void InitializeNameTag()
    {
        // 1. Find or create Canvas
        if (nameCanvas == null)
        {
            nameCanvas = GetComponentInChildren<Canvas>();
        }

        if (nameCanvas == null)
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("SP_NameTagCanvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = new Vector3(0, nameTagHeight, 0);
            
            nameCanvas = canvasObj.AddComponent<Canvas>();
            nameCanvas.renderMode = RenderMode.WorldSpace;
            
            // Setup Scaler
            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10; // Better text quality

            RectTransform rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2, 1); // Small world size
            rect.localScale = Vector3.one * 0.01f; // Adjust scale for world space
        }
        else
        {
            // Enforce WorldSpace
             if (nameCanvas.renderMode != RenderMode.WorldSpace)
            {
                nameCanvas.renderMode = RenderMode.WorldSpace;
                 RectTransform rect = nameCanvas.GetComponent<RectTransform>();
                 rect.localScale = Vector3.one * 0.01f;
            }
        }

        // 2. Find or create Text
        if (nameText == null)
        {
            nameText = nameCanvas.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (nameText == null)
        {
            GameObject textObj = new GameObject("NameText");
            textObj.transform.SetParent(nameCanvas.transform);
            textObj.transform.localPosition = Vector3.zero;
            
            nameText = textObj.AddComponent<TextMeshProUGUI>();
            nameText.alignment = TextAlignmentOptions.Center;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one; 
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        // 3. Apply Settings
        if (nameText != null)
        {
            nameText.fontSize = fontSize;
            nameText.color = nameColor;
            nameText.text = ""; // Clear initially
        }

        _nameTagTransform = nameCanvas.transform;
        // Ensure strictly LOCAL position relative to parent
        _nameTagTransform.localPosition = new Vector3(0, nameTagHeight, 0);

        // Get Camera
        _mainCamera = Camera.main;
    }

    private void UpdateNameDisplay(string name)
    {
        if (nameText != null)
        {
            nameText.text = name;
        }
    }

    private void LateUpdate()
    {
        if (_nameTagTransform == null) return;
        
        // Ensure position sticks (in case animation overrides it, though unlikely for child object)
        if (Mathf.Abs(_nameTagTransform.localPosition.y - nameTagHeight) > 0.01f)
        {
             _nameTagTransform.localPosition = new Vector3(0, nameTagHeight, 0);
        }

        // Camera Management
        bool needCameraCheck = _mainCamera == null || !_mainCamera.enabled || !_mainCamera.gameObject.activeInHierarchy;
        if (needCameraCheck && (Time.time - _lastCameraCheckTime > _cameraCheckInterval))
        {
            _lastCameraCheckTime = Time.time;
            _mainCamera = Camera.main;
            if (_mainCamera == null) _mainCamera = FindObjectOfType<Camera>();
        }

        if (_mainCamera == null) return;

        // Billboarding
        if (lookAtCamera && nameCanvas.renderMode == RenderMode.WorldSpace)
        {
             // Face the camera
            _nameTagTransform.rotation = _mainCamera.transform.rotation;
        }
        
        // Ensure Canvas camera is set
        if (nameCanvas.worldCamera != _mainCamera)
        {
             nameCanvas.worldCamera = _mainCamera;
        }
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SinglePlayerNameTag] {msg}");
        }
    }
}
