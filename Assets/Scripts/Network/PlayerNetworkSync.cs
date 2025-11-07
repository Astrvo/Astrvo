using UnityEngine;
using FishNet.Object;
using FishNet.Component.Transforming;
using FishNet.Component.Animating;
using System.Reflection;
using System;

/// <summary>
/// 玩家网络同步组件
/// 自动为玩家添加位置和动画同步功能
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetworkSync : NetworkBehaviour
{
    /// <summary>
    /// 当本地玩家初始化完成时触发
    /// </summary>
    public static event Action<NetworkObject> OnLocalPlayerInitialized;
    [Header("同步组件引用")]
    [SerializeField] private NetworkTransform networkTransform;
    [SerializeField] private NetworkAnimator networkAnimator;
    
    [Header("动画器引用")]
    [SerializeField] private Animator animator;
    
    [Header("同步设置")]
    [SerializeField] private bool syncPosition = true;
    [SerializeField] private bool syncRotation = true;
    [SerializeField] private bool syncScale = false;
    [SerializeField] private bool syncAnimations = true;
    
    private bool _componentsInitialized = false;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        InitializeSyncComponents();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 检查是否是本地玩家
        if (IsOwner)
        {
            Debug.Log("[PlayerNetworkSync] Local player initialized");
            OnLocalPlayerInitialized?.Invoke(base.NetworkObject);
        }
    }

    private void Awake()
    {
        // 如果NetworkObject还没有初始化，在Awake中设置基础组件
        InitializeSyncComponents();
    }

    /// <summary>
    /// 初始化同步组件
    /// </summary>
    private void InitializeSyncComponents()
    {
        if (_componentsInitialized)
            return;

        // 获取或添加NetworkTransform组件
        if (networkTransform == null)
        {
            networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform == null)
            {
                networkTransform = gameObject.AddComponent<NetworkTransform>();
                Debug.Log("[PlayerNetworkSync] Added NetworkTransform component");
            }
        }

        // 配置NetworkTransform
        if (networkTransform != null)
        {
            // 这些设置需要在Inspector中配置，但我们可以在这里确保组件存在
            // 实际的同步设置通过Inspector或代码设置
        }

        // 获取Animator组件（如果还没有）
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        // 获取或添加NetworkAnimator组件
        if (animator != null && syncAnimations)
        {
            if (networkAnimator == null)
            {
                networkAnimator = GetComponent<NetworkAnimator>();
                if (networkAnimator == null)
                {
                    networkAnimator = gameObject.AddComponent<NetworkAnimator>();
                    Debug.Log("[PlayerNetworkSync] Added NetworkAnimator component");
                }
            }

            // 设置NetworkAnimator的Animator引用（通过反射在运行时设置）
            if (networkAnimator != null && animator != null && networkAnimator.Animator == null)
            {
                // 使用反射设置私有字段_animator
                var field = typeof(FishNet.Component.Animating.NetworkAnimator).GetField("_animator", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(networkAnimator, animator);
                    Debug.Log("[PlayerNetworkSync] NetworkAnimator Animator set via reflection");
                }
                else
                {
                    Debug.LogWarning("[PlayerNetworkSync] Could not set NetworkAnimator Animator via reflection. Please set it manually in Inspector.");
                }
            }
        }

        _componentsInitialized = true;
        Debug.Log("[PlayerNetworkSync] Sync components initialized");
    }

    /// <summary>
    /// 设置同步位置
    /// </summary>
    public void SetSyncPosition(bool enabled)
    {
        syncPosition = enabled;
        if (networkTransform != null)
        {
            networkTransform.SetSynchronizePosition(enabled);
        }
    }

    /// <summary>
    /// 设置同步旋转
    /// </summary>
    public void SetSyncRotation(bool enabled)
    {
        syncRotation = enabled;
        if (networkTransform != null)
        {
            networkTransform.SetSynchronizeRotation(enabled);
        }
    }

    /// <summary>
    /// 设置同步缩放
    /// </summary>
    public void SetSyncScale(bool enabled)
    {
        syncScale = enabled;
        if (networkTransform != null)
        {
            networkTransform.SetSynchronizeScale(enabled);
        }
    }

    /// <summary>
    /// 获取NetworkTransform组件
    /// </summary>
    public NetworkTransform GetNetworkTransform()
    {
        return networkTransform;
    }

    /// <summary>
    /// 获取NetworkAnimator组件
    /// </summary>
    public NetworkAnimator GetNetworkAnimator()
    {
        return networkAnimator;
    }

    /// <summary>
    /// 获取Animator组件
    /// </summary>
    public Animator GetAnimator()
    {
        return animator;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 在编辑器中自动配置组件
    /// </summary>
    [ContextMenu("Auto Setup Sync Components")]
    private void AutoSetupSyncComponents()
    {
        InitializeSyncComponents();
        
        // 确保NetworkTransform配置正确
        if (networkTransform != null)
        {
            UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(networkTransform);
            so.FindProperty("_synchronizePosition").boolValue = syncPosition;
            so.FindProperty("_synchronizeRotation").boolValue = syncRotation;
            so.FindProperty("_synchronizeScale").boolValue = syncScale;
            so.ApplyModifiedProperties();
        }

        // 确保NetworkAnimator配置正确
        if (networkAnimator != null && animator != null)
        {
            UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(networkAnimator);
            so.FindProperty("_animator").objectReferenceValue = animator;
            so.ApplyModifiedProperties();
        }

        Debug.Log("[PlayerNetworkSync] Auto setup complete. Please check the component settings in Inspector.");
    }
#endif
}

