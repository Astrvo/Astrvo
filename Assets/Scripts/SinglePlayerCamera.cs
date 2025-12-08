using UnityEngine;
using UnityEngine.InputSystem;
using Astrvo.Space;

/// <summary>
/// Single Player Camera Controller
/// Combines Orbit, Follow, Zoom, and Cursor Locking into one script.
/// Loosely based on NetworkCameraController logic.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SinglePlayerCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    [Tooltip("If true, attempts to find a GameObject tagged 'Player' on Start if target is null.")]
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Follow Settings")]
    [SerializeField] private float cameraDistance = -2.4f;
    [SerializeField] private bool followOnStart = true;
    
    [Header("Orbit Settings")]
    [SerializeField] private float mouseSensitivityX = 2f;
    [SerializeField] private float mouseSensitivityY = 2f;
    [SerializeField] private float minRotationX = -60f;
    [SerializeField] private float maxRotationX = 50f;
    [SerializeField] private bool smoothDamp = false; // Smooth camera rotation
    private const float SMOOTH_TIME = 0.1f;

    [Header("Zoom Settings")]
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float maxDistance = 10f;
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float firstPersonThreshold = 1.0f;

    [Header("First Person Settings")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Cursor Settings")]
    [SerializeField] private CursorLockMode cursorLockMode = CursorLockMode.Locked;
    [SerializeField] private bool hideCursor = true;
    [SerializeField] private bool applyCursorOnStart = true;

    // References
    private Camera _cam;
    private UnityEngine.InputSystem.PlayerInput _playerInput;
    private InputAction _lookAction;
    private InputAction _scrollAction;

    // State
    private Vector2 _lookInput;
    private float _scrollInput;
    
    // Rotation state
    private Vector3 _rotation;
    private Vector3 _currentVelocity; // For SmoothDamp
    private float _pitch;
    private float _yaw;
    private bool _isRotating;
    
    // Zoom state
    private float _currentDistance;
    private float _targetDistance;
    private float _zoomVelocity; // For SmoothDamp
    private bool _isFirstPerson;
    
    // Follow state
    private bool _isFollowing;

    private void Start()
    {
        _cam = GetComponent<Camera>();

        // 1. Find Target
        if (target == null && autoFindPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else Debug.LogWarning("[SinglePlayerCamera] Player target not found!");
        }

        // 2. Setup Input
        SetupInput();

        // 3. Initialize Rotation/Zoom
        if (target != null)
        {
            _yaw = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;
            _rotation = new Vector3(_pitch, _yaw, 0f);
        }
        else
        {
            _yaw = transform.eulerAngles.y;
            _rotation = transform.eulerAngles;
        }

        _currentDistance = Mathf.Abs(cameraDistance);
        _targetDistance = _currentDistance;

        // 4. Cursor
        if (applyCursorOnStart)
        {
            ApplyCursorSettings();
        }

        // 5. Start following
        if (followOnStart && target != null)
        {
            _isFollowing = true;
        }

        Debug.Log("[SinglePlayerCamera] Initialized.");
    }

    private void SetupInput()
    {
        // Try finding PlayerInput on target first
        if (target != null)
        {
            _playerInput = target.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        }
        
        // Fallback to finding in scene if not on target (e.g. separate Input manager)
        if (_playerInput == null)
        {
            _playerInput = FindObjectOfType<UnityEngine.InputSystem.PlayerInput>();
        }

        if (_playerInput != null)
        {
            // Cache actions
            _lookAction = _playerInput.actions["Look"];
            _scrollAction = _playerInput.actions["Scroll"];
            
            if (_lookAction == null) Debug.LogWarning("[SinglePlayerCamera] 'Look' action not found in PlayerInput.");
            if (_scrollAction == null) Debug.LogWarning("[SinglePlayerCamera] 'Scroll' action not found in PlayerInput.");
        }
        else
        {
            Debug.LogWarning("[SinglePlayerCamera] PlayerInput component not found in scene or on target!");
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleInput();

        // Right mouse button to rotate
        _isRotating = Input.GetMouseButton(1) || Input.touchCount > 0;

        if (_isRotating)
        {
            HandleRotation();
        }

        HandleZoom();
        UpdateViewMode();

        if (_isFollowing)
        {
            HandleFollow();
        }
    }

    private void HandleInput()
    {
        if (_lookAction != null)
            _lookInput = _lookAction.ReadValue<Vector2>();
        
        if (_scrollAction != null)
        {
            Vector2 scrollVal = _scrollAction.ReadValue<Vector2>();
            _scrollInput = scrollVal.y;
        }
        else
        {
            // Start Fallback to legacy mouse scroll if action missing (just in case)
            _scrollInput = Input.mouseScrollDelta.y;
        }
    }

    private void HandleRotation()
    {
        _yaw += _lookInput.x * mouseSensitivityX;
        _pitch -= _lookInput.y * mouseSensitivityY;
        
        // Clamp pitch
        _pitch = ClampAngle(_pitch, minRotationX, maxRotationX);

        if (smoothDamp)
        {
            _rotation = Vector3.SmoothDamp(_rotation, new Vector3(_pitch, _yaw, 0), ref _currentVelocity, SMOOTH_TIME);
        }
        else
        {
            _rotation = new Vector3(_pitch, _yaw, 0);
        }
    }

    private void HandleZoom()
    {
        if (Mathf.Abs(_scrollInput) > 0.01f)
        {
            _targetDistance -= _scrollInput * zoomSpeed * 0.1f; // Scale down scroll input slightly if needed
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }

        _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _zoomVelocity, 0.2f);
    }

    private void UpdateViewMode()
    {
        bool shouldBeFirstPerson = _currentDistance <= firstPersonThreshold;
        if (shouldBeFirstPerson != _isFirstPerson)
        {
            _isFirstPerson = shouldBeFirstPerson;
        }
    }

    private void HandleFollow()
    {
        Quaternion targetRotation = Quaternion.Euler(_rotation);

        if (_isFirstPerson)
        {
            // First Person: Camera at head + offset
            // We rotate the camera itself locally
            transform.position = target.position + firstPersonOffset;
            transform.rotation = targetRotation;
        }
        else
        {
            // Third Person: Camera orbits behind
            // Calculate position based on rotation and distance
            Vector3 offset = targetRotation * Vector3.back * _currentDistance;
            transform.position = target.position + offset;
            transform.rotation = targetRotation;
        }
    }

    private float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }

    public void ApplyCursorSettings()
    {
        Cursor.visible = !hideCursor;
        Cursor.lockState = cursorLockMode;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ApplyCursorSettings();
        }
    }
}
