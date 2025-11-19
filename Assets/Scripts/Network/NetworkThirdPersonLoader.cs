using System;
using ReadyPlayerMe.Core;
using UnityEngine;
using FishNet.Object;

/// <summary>
/// 网络第三人称加载器 - 用于多人游戏
/// 加载GLB avatar并设置NetworkPlayerAnimationController
/// </summary>
public class NetworkThirdPersonLoader : NetworkBehaviour
    {
        private readonly Vector3 avatarPositionOffset = new Vector3(0, -0.08f, 0);
        
        [SerializeField][Tooltip("RPM avatar URL or shortcode to load")] 
        private string avatarUrl;
        private GameObject avatar;
        private AvatarObjectLoader avatarObjectLoader;
        [SerializeField][Tooltip("Animator to use on loaded avatar")] 
        private RuntimeAnimatorController animatorController;
        [SerializeField][Tooltip("If true it will try to load avatar from avatarUrl on start")] 
        private bool loadOnStart = false;
        [SerializeField][Tooltip("Preview avatar to display until avatar loads. Will be destroyed after new avatar is loaded")]
        private GameObject previewAvatar;
        
        public event Action OnLoadComplete;
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // 只有Owner才能加载avatar
            if (!IsOwner)
            {
                enabled = false;
                return;
            }
            
            avatarObjectLoader = new AvatarObjectLoader();
            avatarObjectLoader.OnCompleted += OnLoadCompleted;
            avatarObjectLoader.OnFailed += OnLoadFailed;
            
            if (previewAvatar != null)
            {
                SetupAvatar(previewAvatar);
            }
            if (loadOnStart && !string.IsNullOrEmpty(avatarUrl))
            {
                LoadAvatar(avatarUrl);
            }
        }

        private void OnLoadFailed(object sender, FailureEventArgs args)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                return;
            }
            OnLoadComplete?.Invoke();
        }

        private void OnLoadCompleted(object sender, CompletionEventArgs args)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                return;
            }
            
            // 检查参数是否有效
            if (args == null || args.Avatar == null)
            {
                Debug.LogError("[NetworkThirdPersonLoader] OnLoadCompleted received null arguments");
                return;
            }
            
            if (previewAvatar != null)
            {
                Destroy(previewAvatar);
                previewAvatar = null;
            }
            SetupAvatar(args.Avatar);
            OnLoadComplete?.Invoke();
        }

        private void SetupAvatar(GameObject targetAvatar)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                Debug.LogWarning("[NetworkThirdPersonLoader] SetupAvatar called but object is destroyed");
                return;
            }
            
            // 检查参数是否有效
            if (targetAvatar == null)
            {
                Debug.LogError("[NetworkThirdPersonLoader] SetupAvatar called with null targetAvatar");
                return;
            }
            
            // 确保avatar被激活
            if (!targetAvatar.activeSelf)
            {
                targetAvatar.SetActive(true);
            }
            
            if (avatar != null)
            {
                Destroy(avatar);
            }
            
            avatar = targetAvatar;
            
            // 确保Player GameObject是激活的
            GameObject playerObj = gameObject.CompareTag("Player") ? gameObject : null;
            if (playerObj == null)
            {
                // 如果NetworkThirdPersonLoader不在Player上，尝试找到Player
                playerObj = GameObject.FindGameObjectWithTag("Player");
            }
            
            if (playerObj != null && !playerObj.activeSelf)
            {
                Debug.LogWarning("[NetworkThirdPersonLoader] Player GameObject is inactive, activating it...");
                playerObj.SetActive(true);
            }
            
            // Re-parent and reset transforms
            avatar.transform.parent = transform;
            avatar.transform.localPosition = avatarPositionOffset;
            avatar.transform.localRotation = Quaternion.Euler(0, 0, 0);
            avatar.transform.localScale = Vector3.one; // 确保缩放正确
            
            // 确保avatar及其所有子对象都是激活的
            SetActiveRecursively(avatar, true);
            
            Debug.Log($"[NetworkThirdPersonLoader] Avatar setup complete. Avatar: {avatar.name}, Parent: {transform.name}, Active: {avatar.activeSelf}");
            
            // 设置NetworkPlayerAnimationController（用于多人游戏）
            var networkAnimController = GetComponent<NetworkPlayerAnimationController>();
            if (networkAnimController != null)
            {
                networkAnimController.Setup(avatar, animatorController);
                Debug.Log("[NetworkThirdPersonLoader] NetworkPlayerAnimationController setup complete");
            }
            else
            {
                Debug.LogWarning("[NetworkThirdPersonLoader] NetworkPlayerAnimationController not found on " + gameObject.name);
            }
        }
        
        /// <summary>
        /// 递归设置GameObject及其所有子对象的激活状态
        /// </summary>
        private void SetActiveRecursively(GameObject obj, bool active)
        {
            if (obj == null) return;
            
            obj.SetActive(active);
            foreach (Transform child in obj.transform)
            {
                SetActiveRecursively(child.gameObject, active);
            }
        }
        
        private void OnDestroy()
        {
            // 取消订阅事件，防止在对象销毁后执行回调
            if (avatarObjectLoader != null)
            {
                avatarObjectLoader.OnCompleted -= OnLoadCompleted;
                avatarObjectLoader.OnFailed -= OnLoadFailed;
            }
        }

        /// <summary>
        /// 加载Avatar
        /// </summary>
        public void LoadAvatar(string url)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                Debug.LogWarning("[NetworkThirdPersonLoader] LoadAvatar called but object is destroyed");
                return;
            }
            
            // 检查是否是Owner
            if (!IsOwner)
            {
                Debug.LogWarning("[NetworkThirdPersonLoader] LoadAvatar called but not owner");
                return;
            }
            
            // 检查参数是否有效
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[NetworkThirdPersonLoader] LoadAvatar called with null or empty URL");
                return;
            }
            
            // 检查avatarObjectLoader是否已初始化
            if (avatarObjectLoader == null)
            {
                Debug.LogError("[NetworkThirdPersonLoader] AvatarObjectLoader is not initialized");
                return;
            }
            
            //remove any leading or trailing spaces
            avatarUrl = url.Trim(' ');
            avatarObjectLoader.LoadAvatar(avatarUrl);
        }
}

