using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 网络相机控制器 - 整合CameraFollow和CameraOrbit功能
/// 支持第三人称和第一人称视角切换
/// 符合FishNet多人标准，支持新输入系统
/// 只有IsOwner才能控制相机
/// </summary>
[RequireComponent(typeof(Camera))]
public class NetworkCameraController : NetworkBehaviour
{
    [Header("相机跟随设置 - 参考CameraFollow")]
    [SerializeField] private Transform target; // 跟随的目标（通常是玩家）
    [SerializeField] private float cameraDistance = -2.4f; // 相机距离（负值表示在目标后方）
    [SerializeField] private bool followOnStart = true;
    
    [Header("相机旋转设置 - 参考CameraOrbit")]
    [SerializeField] private float mouseSensitivityX = 2f;
    [SerializeField] private float mouseSensitivityY = 2f;
    [SerializeField] private float minRotationX = -60f;
    [SerializeField] private float maxRotationX = 50f;
    [SerializeField] private bool smoothDamp = false;
    private const float SMOOTH_TIME = 0.1f; // 参考CameraOrbit
    
    [Header("缩放设置")]
    [SerializeField] private float minDistance = 0.5f; // 最小距离（第一人称）
    [SerializeField] private float maxDistance = 10f; // 最大距离
    [SerializeField] private float zoomSpeed = 2f; // 缩放速度
    [SerializeField] private float firstPersonThreshold = 1.0f; // 切换到第一人称的距离阈值
    
    [Header("第一人称设置")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 1.6f, 0f); // 第一人称相机偏移（相对于玩家）
    [SerializeField] private float firstPersonTransitionSpeed = 5f; // 第一人称切换速度
    
    [Header("鼠标光标设置")]
    [SerializeField] private CursorLockMode cursorLockMode = CursorLockMode.Locked;
    [SerializeField] private bool hideCursor = true;
    [SerializeField] private bool applyCursorOnStart = true;
    
    private Camera cam;
    private UnityEngine.InputSystem.PlayerInput playerInput;
    private InputAction lookAction;
    private InputAction scrollAction;
    private Vector2 lookInput;
    private float scrollInput;
    
    // 相机旋转 - 参考CameraOrbit
    private Vector3 rotation;
    private Vector3 currentVelocity;
    private float pitch;
    private float yaw;
    
    // 相机距离
    private float currentDistance;
    private float targetDistance;
    private float zoomVelocity;
    
    // 视角模式
    private bool isFirstPerson = false;
    private bool isFollowing = false;
    private bool isRotating = false; // 是否正在旋转（鼠标右键按住）

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        cam = GetComponent<Camera>();
        
        // 只有Owner才启用此组件和相机
        if (IsOwner)
        {
            this.enabled = true;

            // 启用相机并设置为MainCamera（如果场景中没有其他主相机）
            if (cam != null)
            {
                cam.enabled = true;
                // 如果场景中没有MainCamera，将当前相机设置为MainCamera
                if (Camera.main == null)
                {
                    cam.tag = "MainCamera";
                }
            }
            
            // 获取或添加PlayerInput组件
            playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput == null)
            {
                playerInput = GetComponentInParent<UnityEngine.InputSystem.PlayerInput>();
            }
            
            // 如果找到了PlayerInput，直接使用Input Actions
            if (playerInput != null)
            {
                // 获取Look和Scroll actions
                lookAction = playerInput.actions["Look"];
                scrollAction = playerInput.actions["Scroll"];
                
                if (lookAction != null)
                {
                    lookAction.Enable();
                    Debug.Log("[NetworkCameraController] Look action enabled");
                }
                else
                {
                    Debug.LogWarning("[NetworkCameraController] Look action not found!");
                }
                
                if (scrollAction != null)
                {
                    scrollAction.Enable();
                    Debug.Log("[NetworkCameraController] Scroll action enabled");
                }
                else
                {
                    Debug.LogWarning("[NetworkCameraController] Scroll action not found!");
                }
            }
            else
            {
                Debug.LogWarning("[NetworkCameraController] PlayerInput component not found!");
            }
            
            // 如果没有目标，尝试从父对象获取（相机通常是玩家对象的子对象）
            if (target == null)
            {
                target = transform.parent;
                if (target == null)
                {
                    // 尝试找到当前玩家对象（通过NetworkObject）
                    NetworkObject networkObject = GetComponentInParent<NetworkObject>();
                    if (networkObject != null && networkObject.IsOwner)
                    {
                        target = networkObject.transform;
                    }
                    else
                    {
                        // 尝试找到本地玩家的NetworkObject
                        NetworkObject[] networkObjects = FindObjectsOfType<NetworkObject>();
                        foreach (NetworkObject no in networkObjects)
                        {
                            if (no.IsOwner)
                            {
                                target = no.transform;
                                break;
                            }
                        }
                    }
                }
            }
            
            // 初始化旋转角度 - 参考CameraOrbit.Start
            // 初始设置为平视（pitch = 0），yaw使用相机当前角度（不跟随目标）
            yaw = transform.eulerAngles.y;
            pitch = 0f; // 初始平视，不俯视
            rotation = new Vector3(pitch, yaw, 0f);
            
            // 确保相机的yaw独立于人物旋转，只在鼠标右键时更新
            
            // 初始化相机距离
            currentDistance = Mathf.Abs(cameraDistance);
            targetDistance = currentDistance;
            
            // 检查初始视角模式
            UpdateViewMode();
            
            // 应用鼠标光标设置
            if (applyCursorOnStart)
            {
                ApplyCursorSettings();
            }
            
            // 开始跟随
            if (followOnStart && target != null)
            {
                StartFollow();
            }
            
            Debug.Log($"[NetworkCameraController] Camera initialized for owner. Target: {(target != null ? target.name : "null")}");
        }
        else
        {
            // 非Owner禁用此组件和相机
            this.enabled = false;

            if (cam != null)
            {
                cam.enabled = false;
            }
        }
    }
    
    // 保留这些方法以兼容PlayerInput的自动绑定（如果使用）
    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }
    
    public void OnScroll(InputValue value)
    {
        Vector2 scrollValue = value.Get<Vector2>();
        scrollInput = scrollValue.y;
    }

    void LateUpdate()
    {
        if (target == null)
            return;
        
        // 直接从Input Actions读取输入（如果可用）
        if (lookAction != null)
        {
            lookInput = lookAction.ReadValue<Vector2>();
        }
        
        if (scrollAction != null)
        {
            Vector2 scrollValue = scrollAction.ReadValue<Vector2>();
            scrollInput = scrollValue.y;
        }
        
        // 检查是否正在旋转（鼠标右键按住或触摸）- 参考CameraOrbit
        isRotating = Input.GetMouseButton(1) || Input.touchCount > 0;
        
        // 处理相机旋转（类似CameraOrbit）
        // 只在鼠标右键按住时旋转
        if (isRotating)
        {
            // 使用Look action，但只在右键按住时处理
            // 如果lookInput有值，说明鼠标在移动
            if (lookInput.magnitude > 0.01f)
            {
                HandleCameraRotation();
            }
        }
        else
        {
            // 不旋转时，清空lookInput（避免在非右键时累积输入）
            lookInput = Vector2.zero;
        }
        
        // 处理相机缩放
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            HandleCameraZoom();
        }
        
        // 更新视角模式
        UpdateViewMode();
        
        // 处理相机跟随（类似CameraFollow）
        if (isFollowing)
        {
            UpdateCameraFollow();
        }
    }
    
    /// <summary>
    /// 处理相机旋转 - 完全参考CameraOrbit.LateUpdate
    /// </summary>
    private void HandleCameraRotation()
    {
        // 更新 yaw 和 pitch - 参考CameraOrbit
        yaw += lookInput.x * mouseSensitivityX;
        pitch -= lookInput.y * mouseSensitivityY;
        
        if (smoothDamp)
        {
            // 参考CameraOrbit：Vector3.SmoothDamp
            rotation = Vector3.SmoothDamp(rotation, new Vector3(pitch, yaw), ref currentVelocity, SMOOTH_TIME);
        }
        else
        {
            // 参考CameraOrbit：直接设置
            rotation = new Vector3(pitch, yaw, rotation.z);
        }
        
        // 限制摄像头旋转角度 - 参考CameraOrbit
        rotation.x = ClampAngle(rotation.x, minRotationX, maxRotationX);
    }
    
    /// <summary>
    /// 处理相机缩放
    /// </summary>
    private void HandleCameraZoom()
    {
        // 根据滚轮输入调整距离
        // scrollInput.y 是滚轮值，向上为正，向下为负
        targetDistance -= scrollInput * zoomSpeed;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        
        // 平滑插值到目标距离
        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, 0.2f);
        
        // 重置scrollInput，避免持续缩放
        scrollInput = 0f;
    }
    
    /// <summary>
    /// 更新视角模式（第一人称/第三人称）
    /// </summary>
    private void UpdateViewMode()
    {
        bool shouldBeFirstPerson = currentDistance <= firstPersonThreshold;
        
        if (shouldBeFirstPerson != isFirstPerson)
        {
            isFirstPerson = shouldBeFirstPerson;
            Debug.Log($"[NetworkCameraController] View mode changed to: {(isFirstPerson ? "First Person" : "Third Person")}");
        }
    }
    
    /// <summary>
    /// 更新相机跟随 - 完全参考CameraFollow.LateUpdate
    /// CameraFollow的结构：组件在父对象上，playerCamera是子对象
    /// </summary>
    private void UpdateCameraFollow()
    {
        // 参考CameraFollow.LateUpdate：
        // transform.position = target.position; (父对象跟随目标)
        // playerCamera.transform.localPosition = Vector3.forward * cameraDistance; (相机本地位置)
        // playerCamera.transform.localRotation = Quaternion.Euler(Vector3.zero); (相机本地旋转)
        
        if (isFirstPerson)
        {
            // 第一人称模式：相机在玩家头部位置
            if (transform.parent != null)
            {
                // 父对象跟随目标位置
                transform.parent.position = target.position;
                
                // 相机位置在玩家头部
                transform.localPosition = firstPersonOffset;
                
                // 相机旋转跟随鼠标输入
                transform.localRotation = Quaternion.Euler(rotation);
            }
            else
            {
                // 如果没有父对象，直接设置
                transform.position = target.position + firstPersonOffset;
                transform.rotation = Quaternion.Euler(rotation);
            }
        }
        else
        {
            // 第三人称模式：相机在玩家后方
            if (transform.parent != null)
            {
                // 父对象跟随目标位置 - 参考CameraFollow
                // 注意：父对象只跟随位置，不跟随旋转，保持相机的独立旋转
                transform.parent.position = target.position;
                transform.parent.rotation = Quaternion.identity; // 父对象保持无旋转
                
                // 计算相机位置（基于旋转角度和距离）
                Quaternion rot = Quaternion.Euler(rotation);
                Vector3 offset = rot * Vector3.back * currentDistance;
                
                // 设置Camera本地位置和旋转 - 参考CameraFollow
                transform.localPosition = offset;
                transform.localRotation = rot;
            }
            else
            {
                // 如果Camera没有父对象，直接计算世界位置
                Quaternion rot = Quaternion.Euler(rotation);
                Vector3 offset = rot * Vector3.back * currentDistance;
                transform.position = target.position + offset;
                transform.rotation = rot;
            }
        }
    }
    
    /// <summary>
    /// 限制角度 - 完全参考CameraOrbit.ClampAngle
    /// </summary>
    private float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F)
        {
            angle += 360F;
        }
        if (angle > 360F)
        {
            angle -= 360F;
        }
        return Mathf.Clamp(angle, min, max);
    }
    
    /// <summary>
    /// 应用鼠标光标设置
    /// </summary>
    public void ApplyCursorSettings()
    {
        Cursor.visible = !hideCursor;
        Cursor.lockState = cursorLockMode;
    }
    
    /// <summary>
    /// 开始跟随 - 参考CameraFollow.StartFollow
    /// </summary>
    public void StartFollow()
    {
        isFollowing = true;
    }
    
    /// <summary>
    /// 停止跟随 - 参考CameraFollow.StopFollow
    /// </summary>
    public void StopFollow()
    {
        isFollowing = false;
    }
    
    /// <summary>
    /// 设置相机目标
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    /// <summary>
    /// 设置相机距离
    /// </summary>
    public void SetDistance(float distance)
    {
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        currentDistance = targetDistance;
    }
    
    /// <summary>
    /// 重置相机旋转
    /// </summary>
    public void ResetRotation()
    {
        yaw = target != null ? target.eulerAngles.y : 0f;
        pitch = 0f;
        rotation = new Vector3(pitch, yaw, 0f);
    }
    
    /// <summary>
    /// 获取当前视角模式
    /// </summary>
    public bool IsFirstPerson()
    {
        return isFirstPerson;
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (IsOwner && hasFocus)
        {
            ApplyCursorSettings();
        }
    }
    
    public override void OnStopClient()
    {
        base.OnStopClient();
        
        // 禁用Input Actions
        if (lookAction != null)
        {
            lookAction.Disable();
        }
        
        if (scrollAction != null)
        {
            scrollAction.Disable();
        }
    }
}
