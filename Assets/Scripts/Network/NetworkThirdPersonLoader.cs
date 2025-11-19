using System;
using ReadyPlayerMe.Core;
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

/// <summary>
/// 网络第三人称加载器 - 用于多人游戏
/// 加载GLB avatar并设置NetworkPlayerAnimationController
/// </summary>
public class NetworkThirdPersonLoader : NetworkBehaviour
    {
        private readonly Vector3 avatarPositionOffset = new Vector3(0, -0.08f, 0);
        
        [SerializeField][Tooltip("RPM avatar URL or shortcode to load")] 
        private string avatarUrl;
        
        // 同步的avatar URL，所有客户端都能看到
        private readonly SyncVar<string> _syncedAvatarUrl = new SyncVar<string>();
        
        private GameObject avatar;
        private AvatarObjectLoader avatarObjectLoader;
        [SerializeField][Tooltip("Animator to use on loaded avatar")] 
        private RuntimeAnimatorController animatorController;
        [SerializeField][Tooltip("If true it will try to load avatar from avatarUrl on start")] 
        private bool loadOnStart = false;
        [SerializeField][Tooltip("Preview avatar to display until avatar loads. Will be destroyed after new avatar is loaded")]
        private GameObject previewAvatar;
        
        [Header("调试设置")]
        [SerializeField] private bool enableDebugLogs = true;
        
        public event Action OnLoadComplete;
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // 在服务器端，如果有预设的avatarUrl，设置到SyncVar
            // 这样所有客户端都能接收到初始值
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                _syncedAvatarUrl.Value = avatarUrl;
                LogDebug($"Server: Setting initial avatar URL: {avatarUrl}");
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // 所有客户端都需要初始化AvatarObjectLoader（不只是Owner）
            avatarObjectLoader = new AvatarObjectLoader();
            avatarObjectLoader.OnCompleted += OnLoadCompleted;
            avatarObjectLoader.OnFailed += OnLoadFailed;
            
            // 订阅avatar URL同步事件（必须在检查值之前订阅）
            _syncedAvatarUrl.OnChange += OnAvatarUrlChanged;
            
            // 如果是Owner，设置avatar URL并立即加载
            if (IsOwner)
            {
                // 如果有预设的avatarUrl，同步到所有客户端
                // 注意：Owner设置SyncVar会同步到服务器，然后服务器会同步到所有客户端
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    _syncedAvatarUrl.Value = avatarUrl;
                    LogDebug($"Owner: Setting avatar URL to SyncVar: {avatarUrl}");
                }
                
                // 显示预览avatar（如果有）
                if (previewAvatar != null)
                {
                    SetupAvatar(previewAvatar);
                }
                
                // 如果设置了loadOnStart，立即加载（Owner不需要等待）
                if (loadOnStart && !string.IsNullOrEmpty(avatarUrl))
                {
                    LogDebug($"Owner: Loading avatar immediately: {avatarUrl}");
                    LoadAvatarForClient(avatarUrl);
                }
            }
            else
            {
                // 非Owner：检查是否已经有同步的URL
                LogDebug($"Non-owner: Checking for synced avatar URL... (NetworkObjectId: {NetworkObject.ObjectId})");
                
                // 立即检查一次（可能SyncVar已经同步了）
                if (!string.IsNullOrEmpty(_syncedAvatarUrl.Value))
                {
                    LogDebug($"Non-owner: Found synced avatar URL immediately: {_syncedAvatarUrl.Value}");
                    LoadAvatarForClient(_syncedAvatarUrl.Value);
                }
                else
                {
                    // 如果没有，使用协程延迟检查
                    LogDebug("Non-owner: No URL yet, starting delayed check...");
                    StartCoroutine(CheckAndLoadAvatarForNonOwner());
                }
            }
        }
        
        /// <summary>
        /// 检查并加载avatar（仅用于非Owner客户端，延迟检查确保SyncVar已同步）
        /// </summary>
        private System.Collections.IEnumerator CheckAndLoadAvatarForNonOwner()
        {
            // 等待几帧，确保SyncVar已经同步
            yield return new WaitForSeconds(0.1f);
            
            // 检查是否有同步的URL
            string syncedUrl = _syncedAvatarUrl.Value;
            LogDebug($"Non-owner: After 0.1s wait, synced URL = '{syncedUrl}' (empty: {string.IsNullOrEmpty(syncedUrl)})");
            
            if (!string.IsNullOrEmpty(syncedUrl))
            {
                LogDebug($"Non-owner: Found synced avatar URL: {syncedUrl}");
                LoadAvatarForClient(syncedUrl);
                yield break;
            }
            
            LogDebug("Non-owner: No synced avatar URL yet, waiting longer...");
            
            // 如果还没有URL，显示预览avatar（如果有）
            if (previewAvatar != null)
            {
                SetupAvatar(previewAvatar);
            }
            
            // 再等待一下，如果还是没有，可能是URL还没同步
            yield return new WaitForSeconds(0.5f);
            
            syncedUrl = _syncedAvatarUrl.Value;
            LogDebug($"Non-owner: After 0.6s total wait, synced URL = '{syncedUrl}' (empty: {string.IsNullOrEmpty(syncedUrl)})");
            
            if (!string.IsNullOrEmpty(syncedUrl))
            {
                LogDebug($"Non-owner: Avatar URL synced after wait: {syncedUrl}");
                LoadAvatarForClient(syncedUrl);
            }
            else
            {
                LogWarning($"Non-owner: Still no avatar URL after waiting. syncedUrl: '{syncedUrl}', prefab avatarUrl: '{avatarUrl}', NetworkObjectId: {NetworkObject.ObjectId}");
                
                // 如果还是没有，尝试使用prefab中的默认值（作为后备方案）
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    LogWarning($"Non-owner: Using prefab avatarUrl as fallback: {avatarUrl}");
                    LoadAvatarForClient(avatarUrl);
                }
            }
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            // 取消订阅事件
            _syncedAvatarUrl.OnChange -= OnAvatarUrlChanged;
        }
        
        /// <summary>
        /// Avatar URL同步变化回调
        /// </summary>
        private void OnAvatarUrlChanged(string oldUrl, string newUrl, bool asServer)
        {
            LogDebug($"Avatar URL changed from '{oldUrl}' to '{newUrl}' (asServer: {asServer}, IsOwner: {IsOwner}, NetworkObjectId: {NetworkObject.ObjectId})");
            
            // 所有客户端在URL同步后都应该加载avatar（包括Owner，如果URL改变了）
            if (!string.IsNullOrEmpty(newUrl))
            {
                // 如果已经有avatar了，先销毁它（除非是Owner且URL相同，避免重复加载）
                if (avatar != null && avatar != previewAvatar)
                {
                    // 如果是Owner且URL相同，可能已经在加载了，跳过
                    if (IsOwner && newUrl == avatarUrl)
                    {
                        LogDebug("Owner: URL unchanged, skipping reload");
                        return;
                    }
                    
                    LogDebug("Destroying existing avatar before loading new one");
                    Destroy(avatar);
                    avatar = null;
                }
                
                LogDebug($"Loading avatar from synced URL: {newUrl} (IsOwner: {IsOwner})");
                LoadAvatarForClient(newUrl);
            }
        }

        private void OnLoadFailed(object sender, FailureEventArgs args)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                return;
            }
            
            LogWarning($"Avatar load failed (IsOwner: {IsOwner}). Error: {(args != null ? args.Message : "Unknown error")}");
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
                LogError("OnLoadCompleted received null arguments");
                return;
            }
            
            LogDebug($"Avatar loaded successfully (IsOwner: {IsOwner})");
            
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
                LogWarning("SetupAvatar called but object is destroyed");
                return;
            }
            
            // 检查参数是否有效
            if (targetAvatar == null)
            {
                LogError("SetupAvatar called with null targetAvatar");
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
                LogWarning("Player GameObject is inactive, activating it...");
                playerObj.SetActive(true);
            }
            
            // Re-parent and reset transforms
            avatar.transform.parent = transform;
            avatar.transform.localPosition = avatarPositionOffset;
            avatar.transform.localRotation = Quaternion.Euler(0, 0, 0);
            avatar.transform.localScale = Vector3.one; // 确保缩放正确
            
            // 确保avatar及其所有子对象都是激活的（所有客户端都需要）
            SetActiveRecursively(avatar, true);
            
            LogDebug($"Avatar setup complete. Avatar: {avatar.name}, Parent: {transform.name}, Active: {avatar.activeSelf}, IsOwner: {IsOwner}");
            
            // 设置NetworkPlayerAnimationController（用于多人游戏）
            // 注意：所有客户端都需要设置动画控制器，不只是Owner
            var networkAnimController = GetComponent<NetworkPlayerAnimationController>();
            if (networkAnimController != null)
            {
                networkAnimController.Setup(avatar, animatorController);
                LogDebug("NetworkPlayerAnimationController setup complete");
            }
            else
            {
                LogWarning("NetworkPlayerAnimationController not found on " + gameObject.name);
            }
            
            // Avatar加载完成后，如果是非Owner，也需要重置位置（确保y值正确）
            if (!IsOwner)
            {
                StartCoroutine(ResetPositionAfterAvatarLoad());
            }
        }
        
        /// <summary>
        /// Avatar加载后重置位置（用于非Owner客户端）
        /// </summary>
        private System.Collections.IEnumerator ResetPositionAfterAvatarLoad()
        {
            // 等待几帧，确保avatar完全设置好
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            
            // 获取PlayerPositionReset组件
            PlayerPositionReset positionReset = GetComponent<PlayerPositionReset>();
            if (positionReset != null)
            {
                // 调用重置位置方法（即使不是Owner，也需要调整位置）
                LogDebug("Non-owner: Resetting position after avatar load");
                ResetNonOwnerPosition();
            }
            else
            {
                // 如果没有PlayerPositionReset，直接调整y值
                LogDebug("Non-owner: No PlayerPositionReset found, adjusting y position directly");
                AdjustPlayerYPosition();
            }
        }
        
        /// <summary>
        /// 重置非Owner玩家的位置
        /// </summary>
        private void ResetNonOwnerPosition()
        {
            // 获取生成位置
            Vector3 spawnPosition = GetSpawnPosition();
            
            // 使用CharacterController的话，需要先禁用再设置位置
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                transform.position = spawnPosition;
                controller.enabled = true;
            }
            else
            {
                transform.position = spawnPosition;
            }
            
            LogDebug($"Non-owner: Position reset to {spawnPosition}");
        }
        
        /// <summary>
        /// 调整玩家Y位置（确保不在地面以下）
        /// </summary>
        private void AdjustPlayerYPosition()
        {
            Vector3 currentPos = transform.position;
            
            // 如果y值太小（可能在地面以下），调整到合理的高度
            if (currentPos.y < 0.5f)
            {
                Vector3 adjustedPos = new Vector3(currentPos.x, 1.0f, currentPos.z);
                
                CharacterController controller = GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                    transform.position = adjustedPos;
                    controller.enabled = true;
                }
                else
                {
                    transform.position = adjustedPos;
                }
                
                LogDebug($"Non-owner: Y position adjusted from {currentPos.y} to {adjustedPos.y}");
            }
        }
        
        /// <summary>
        /// 获取生成位置（从PlayerPositionReset或PlayerSpawner）
        /// </summary>
        private Vector3 GetSpawnPosition()
        {
            // 尝试从PlayerPositionReset获取
            PlayerPositionReset positionReset = GetComponent<PlayerPositionReset>();
            if (positionReset != null)
            {
                // 使用反射或公共方法获取spawn位置
                // 如果没有公共方法，尝试从PlayerSpawner获取
            }
            
            // 尝试从PlayerSpawner获取spawn points
            var spawner = FindObjectOfType<FishNet.Component.Spawning.PlayerSpawner>();
            if (spawner != null && spawner.Spawns != null && spawner.Spawns.Length > 0)
            {
                // 使用NetworkObject的OwnerId来选择spawn点
                int spawnIndex = (int)(NetworkObject.OwnerId % spawner.Spawns.Length);
                if (spawner.Spawns[spawnIndex] != null)
                {
                    return spawner.Spawns[spawnIndex].position;
                }
            }
            
            // 使用默认位置（保持x和z，只调整y）
            Vector3 currentPos = transform.position;
            return new Vector3(currentPos.x, 1.0f, currentPos.z);
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
        /// 加载Avatar（公共方法，通常由Owner调用）
        /// </summary>
        public void LoadAvatar(string url)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                LogWarning("LoadAvatar called but object is destroyed");
                return;
            }
            
            // 只有Owner才能调用此方法设置新的URL
            if (!IsOwner)
            {
                LogWarning("LoadAvatar called but not owner. Use LoadAvatarForClient for non-owners.");
                return;
            }
            
            // 检查参数是否有效
            if (string.IsNullOrEmpty(url))
            {
                LogError("LoadAvatar called with null or empty URL");
                return;
            }
            
            // 同步URL到所有客户端
            string trimmedUrl = url.Trim(' ');
            _syncedAvatarUrl.Value = trimmedUrl;
            avatarUrl = trimmedUrl;
            
            // Owner立即加载
            LoadAvatarForClient(trimmedUrl);
        }
        
        /// <summary>
        /// 为客户端加载Avatar（内部方法，所有客户端都可以调用）
        /// </summary>
        private void LoadAvatarForClient(string url)
        {
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                LogWarning("LoadAvatarForClient called but object is destroyed");
                return;
            }
            
            // 检查参数是否有效
            if (string.IsNullOrEmpty(url))
            {
                LogError("LoadAvatarForClient called with null or empty URL");
                return;
            }
            
            // 检查avatarObjectLoader是否已初始化
            if (avatarObjectLoader == null)
            {
                LogError("AvatarObjectLoader is not initialized");
                return;
            }
            
            LogDebug($"Loading avatar from URL: {url} (IsOwner: {IsOwner})");
            
            // 移除前后空格并加载
            string trimmedUrl = url.Trim(' ');
            avatarObjectLoader.LoadAvatar(trimmedUrl);
        }
        
        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[NetworkThirdPersonLoader] {message}");
            }
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[NetworkThirdPersonLoader] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[NetworkThirdPersonLoader] {message}");
        }
}

