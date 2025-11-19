using System;
using ReadyPlayerMe.Core;
using UnityEngine;

namespace Astrvo.Space
{
    public class ThirdPersonLoader : MonoBehaviour
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
        [SerializeField][Tooltip("If true, this loader will be disabled if another ThirdPersonLoader is found on the Player GameObject")]
        private bool disableIfPlayerHasLoader = true;

        public event Action OnLoadComplete;
        
        private static ThirdPersonLoader activeLoader;
        
        private void Start()
        {
            // 检查是否有冲突
            bool isPlayerLoader = gameObject.CompareTag("Player");
            
            // 如果已经有活动的loader
            if (activeLoader != null && activeLoader != this)
            {
                bool activeIsPlayer = activeLoader.gameObject.CompareTag("Player");
                
                // 优先级规则：Player上的loader优先于非Player上的loader
                if (isPlayerLoader && !activeIsPlayer)
                {
                    // 这个在Player上，活动的在非Player上，禁用活动的
                    Debug.LogWarning($"[ThirdPersonLoader] Player上的loader优先级更高，禁用 {activeLoader.gameObject.name} 上的loader。");
                    activeLoader.enabled = false;
                    activeLoader = this;
                }
                else if (!isPlayerLoader && activeIsPlayer)
                {
                    // 活动的在Player上，这个不在，禁用这个
                    Debug.LogWarning($"[ThirdPersonLoader] Player上已有活动的loader，禁用 {gameObject.name} 上的loader。");
                    enabled = false;
                    return;
                }
                else if (isPlayerLoader && activeIsPlayer)
                {
                    // 两个都在Player上，保留第一个，禁用这个
                    Debug.LogWarning($"[ThirdPersonLoader] 检测到多个ThirdPersonLoader在Player上。禁用 {gameObject.name} 上的loader，使用 {activeLoader.gameObject.name} 上的loader。");
                    enabled = false;
                    return;
                }
                else
                {
                    // 两个都不在Player上，保留第一个，禁用这个
                    Debug.LogWarning($"[ThirdPersonLoader] 检测到多个ThirdPersonLoader。禁用 {gameObject.name} 上的loader，使用 {activeLoader.gameObject.name} 上的loader。");
                    enabled = false;
                    return;
                }
            }
            
            // 设置为活动loader
            activeLoader = this;
            
            avatarObjectLoader = new AvatarObjectLoader();
            avatarObjectLoader.OnCompleted += OnLoadCompleted;
            avatarObjectLoader.OnFailed += OnLoadFailed;
            
            if (previewAvatar != null)
            {
                SetupAvatar(previewAvatar);
            }
            if (loadOnStart)
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
                Debug.LogError("[ThirdPersonLoader] OnLoadCompleted received null arguments");
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

        private void SetupAvatar(GameObject  targetAvatar)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                Debug.LogWarning("[ThirdPersonLoader] SetupAvatar called but object is destroyed");
                return;
            }
            
            // 检查参数是否有效
            if (targetAvatar == null)
            {
                Debug.LogError("[ThirdPersonLoader] SetupAvatar called with null targetAvatar");
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
                // 如果ThirdPersonLoader不在Player上，尝试找到Player
                playerObj = GameObject.FindGameObjectWithTag("Player");
            }
            
            if (playerObj != null && !playerObj.activeSelf)
            {
                Debug.LogWarning("[ThirdPersonLoader] Player GameObject is inactive, activating it...");
                playerObj.SetActive(true);
            }
            
            // Re-parent and reset transforms
            avatar.transform.parent = transform;
            avatar.transform.localPosition = avatarPositionOffset;
            avatar.transform.localRotation = Quaternion.Euler(0, 0, 0);
            avatar.transform.localScale = Vector3.one; // 确保缩放正确
            
            // 确保avatar及其所有子对象都是激活的
            SetActiveRecursively(avatar, true);
            
            Debug.Log($"[ThirdPersonLoader] Avatar setup complete. Avatar: {avatar.name}, Parent: {transform.name}, Active: {avatar.activeSelf}");
            
            var controller = GetComponent<ThirdPersonController>();
            if (controller != null)
            {
                controller.Setup(avatar, animatorController);
                Debug.Log("[ThirdPersonLoader] ThirdPersonController setup complete");
            }
            else
            {
                Debug.LogWarning("[ThirdPersonLoader] ThirdPersonController not found on " + gameObject.name);
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
            
            // 如果这是活动loader，清除引用
            if (activeLoader == this)
            {
                activeLoader = null;
            }
        }
        
        /// <summary>
        /// 获取当前活动的ThirdPersonLoader
        /// </summary>
        public static ThirdPersonLoader GetActiveLoader()
        {
            return activeLoader;
        }

        public void LoadAvatar(string url)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                Debug.LogWarning("[ThirdPersonLoader] LoadAvatar called but object is destroyed");
                return;
            }
            
            // 检查参数是否有效
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("[ThirdPersonLoader] LoadAvatar called with null or empty URL");
                return;
            }
            
            // 检查avatarObjectLoader是否已初始化
            if (avatarObjectLoader == null)
            {
                Debug.LogError("[ThirdPersonLoader] AvatarObjectLoader is not initialized");
                return;
            }
            
            //remove any leading or trailing spaces
            avatarUrl = url.Trim(' ');
            avatarObjectLoader.LoadAvatar(avatarUrl);
        }

    }
}
