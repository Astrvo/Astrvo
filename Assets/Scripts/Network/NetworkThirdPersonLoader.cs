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
        
        [Header("加载设置")]
        [SerializeField] private float maxWaitTimeForUrl = 10f; // 非Owner客户端等待URL的最大时间
        [SerializeField] private float urlCheckInterval = 0.2f; // 检查URL的间隔时间
        [SerializeField] private int maxLoadRetries = 3; // 最大重试次数
        [SerializeField] private float retryDelay = 2f; // 重试延迟时间
        
        public event Action OnLoadComplete;
        
        private int _loadRetryCount = 0; // 当前重试次数
        private bool _isLoading = false; // 是否正在加载
        private System.Collections.IEnumerator _checkUrlCoroutine; // URL检查协程
        private bool _hasResetYPosition = false; // 是否已经复位过y坐标（只复位一次）
        
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
            
            // 重置重试计数和标志
            _loadRetryCount = 0;
            _isLoading = false;
            _hasResetYPosition = false; // 重置y坐标复位标志
            
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
                
                // 立即检查并修复y坐标（防止新加入的玩家掉下去）
                StartCoroutine(CheckAndFixYPositionForNewPlayer());
                
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
                    _checkUrlCoroutine = CheckAndLoadAvatarForNonOwner();
                    StartCoroutine(_checkUrlCoroutine);
                }
            }
            
            // 启动定期检查，确保avatar和玩家GameObject始终激活
            StartCoroutine(PeriodicActivationCheck());
        }
        
        /// <summary>
        /// 检查并修复新加入玩家的y坐标（防止掉下去）
        /// 注意：这个函数只在新玩家加入时调用，如果avatar还没加载，不会复位
        /// 真正的复位会在avatar加载完成后在EnsureAvatarVisibleAfterLoad中进行
        /// </summary>
        private System.Collections.IEnumerator CheckAndFixYPositionForNewPlayer()
        {
            // 等待一小段时间，确保NetworkTransform已经同步了初始位置
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForFixedUpdate();
            
            // 检查对象是否已被销毁
            if (this == null || gameObject == null)
            {
                yield break;
            }
            
            // 如果avatar还没加载，不进行复位（等待avatar加载完成后再复位）
            if (avatar == null)
            {
                LogDebug("Non-owner: Avatar not loaded yet, y position reset will happen after avatar loads");
                yield break;
            }
            
            // 如果已经复位过，不再复位
            if (_hasResetYPosition)
            {
                LogDebug("Non-owner: Y position already reset, skipping");
                yield break;
            }
            
            // 注意：这里不应该复位，因为avatar加载完成后会统一复位
            // 这个函数只是检查，真正的复位在EnsureAvatarVisibleAfterLoad中
            LogDebug("Non-owner: Avatar loaded, y position reset will be handled by EnsureAvatarVisibleAfterLoad");
        }
        
        /// <summary>
        /// 检查并加载avatar（仅用于非Owner客户端，延迟检查确保SyncVar已同步）
        /// </summary>
        private System.Collections.IEnumerator CheckAndLoadAvatarForNonOwner()
        {
            float elapsedTime = 0f;
            bool urlFound = false;
            
            // 先等待一小段时间，确保SyncVar有机会同步
            yield return new WaitForSeconds(0.1f);
            elapsedTime += 0.1f;
            
            // 循环检查URL，直到找到或超时
            while (elapsedTime < maxWaitTimeForUrl && !urlFound)
            {
                string syncedUrl = _syncedAvatarUrl.Value;
                
                if (!string.IsNullOrEmpty(syncedUrl))
                {
                    LogDebug($"Non-owner: Found synced avatar URL after {elapsedTime:F1}s: {syncedUrl}");
                    LoadAvatarForClient(syncedUrl);
                    urlFound = true;
                    yield break;
                }
                
                // 如果还没有URL，显示预览avatar（如果有且还没显示）
                if (previewAvatar != null && avatar == null)
                {
                    SetupAvatar(previewAvatar);
                }
                
                // 等待下一次检查
                yield return new WaitForSeconds(urlCheckInterval);
                elapsedTime += urlCheckInterval;
                
                // 每2秒记录一次日志
                if (Mathf.FloorToInt(elapsedTime) % 2 == 0 && Mathf.Approximately(elapsedTime % 1f, 0f))
                {
                    LogDebug($"Non-owner: Still waiting for avatar URL... (elapsed: {elapsedTime:F1}s)");
                }
            }
            
            // 如果超时还没找到URL
            if (!urlFound)
            {
                string syncedUrl = _syncedAvatarUrl.Value;
                LogWarning($"Non-owner: No avatar URL after {elapsedTime:F1}s. syncedUrl: '{syncedUrl}', prefab avatarUrl: '{avatarUrl}', NetworkObjectId: {NetworkObject.ObjectId}");
                
                // 尝试使用prefab中的默认值（作为后备方案）
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    LogWarning($"Non-owner: Using prefab avatarUrl as fallback: {avatarUrl}");
                    LoadAvatarForClient(avatarUrl);
                }
                else
                {
                    LogError($"Non-owner: No avatar URL available and no fallback. Player model may not load. NetworkObjectId: {NetworkObject.ObjectId}");
                }
            }
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            
            // 停止协程
            if (_checkUrlCoroutine != null)
            {
                StopCoroutine(_checkUrlCoroutine);
                _checkUrlCoroutine = null;
            }
            
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
            
            string errorMessage = args != null ? args.Message : "Unknown error";
            LogWarning($"Avatar load failed (IsOwner: {IsOwner}, retry: {_loadRetryCount}/{maxLoadRetries}). Error: {errorMessage}");
            
            _isLoading = false;
            
            // 如果还没达到最大重试次数，尝试重试
            if (_loadRetryCount < maxLoadRetries)
            {
                _loadRetryCount++;
                LogDebug($"Retrying avatar load ({_loadRetryCount}/{maxLoadRetries}) after {retryDelay}s...");
                StartCoroutine(RetryLoadAvatar());
            }
            else
            {
                LogError($"Avatar load failed after {maxLoadRetries} retries. Giving up.");
                OnLoadComplete?.Invoke();
            }
        }
        
        /// <summary>
        /// 重试加载avatar
        /// </summary>
        private System.Collections.IEnumerator RetryLoadAvatar()
        {
            yield return new WaitForSeconds(retryDelay);
            
            // 检查对象是否仍然有效
            if (this == null || gameObject == null)
            {
                yield break;
            }
            
            // 获取要加载的URL
            string urlToLoad = null;
            if (!string.IsNullOrEmpty(_syncedAvatarUrl.Value))
            {
                urlToLoad = _syncedAvatarUrl.Value;
            }
            else if (!string.IsNullOrEmpty(avatarUrl))
            {
                urlToLoad = avatarUrl;
            }
            
            if (!string.IsNullOrEmpty(urlToLoad))
            {
                LogDebug($"Retrying to load avatar from: {urlToLoad}");
                LoadAvatarForClient(urlToLoad);
            }
            else
            {
                LogError("Cannot retry: No avatar URL available");
                OnLoadComplete?.Invoke();
            }
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
            
            _isLoading = false;
            _loadRetryCount = 0; // 重置重试计数
            
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
            // 确保在设置parent之前，avatar的world position不会改变
            Vector3 worldPos = avatar.transform.position;
            avatar.transform.parent = transform;
            avatar.transform.localPosition = avatarPositionOffset;
            avatar.transform.localRotation = Quaternion.Euler(0, 0, 0);
            avatar.transform.localScale = Vector3.one; // 确保缩放正确
            
            // 对于非Owner客户端，先隐藏avatar，等待y坐标复位后再显示
            // 复位逻辑在EnsureAvatarVisibleAfterLoad中统一处理
            if (!IsOwner)
            {
                SetActiveRecursively(avatar, false);
                LogDebug($"Non-owner: Avatar hidden initially (y={transform.position.y}), will show after y position reset");
            }
            else
            {
                // Owner客户端正常显示
                SetActiveRecursively(avatar, true);
            }
            
            // 确保avatar的Renderer组件是启用的（可能被意外禁用）
            // 注意：即使avatar被隐藏，也要确保renderer是启用的，这样显示时才能正常渲染
            Renderer[] renderers = avatar.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && !renderer.enabled)
                {
                    LogDebug($"Enabling disabled renderer: {renderer.name}");
                    renderer.enabled = true;
                }
            }
            
            LogDebug($"Avatar setup complete. Avatar: {avatar.name}, Parent: {transform.name}, Active: {avatar.activeSelf}, IsOwner: {IsOwner}, Player pos: {transform.position}, Avatar localPos: {avatar.transform.localPosition}");
            
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
            
            // Avatar加载完成后，对于非Owner客户端，启动协程确保y坐标复位并显示avatar和username
            if (!IsOwner)
            {
                // 确保协程被启动，即使之前可能已经启动过
                StartCoroutine(EnsureAvatarVisibleAfterLoad());
            }
        }
        
        /// <summary>
        /// Avatar加载后确保可见性（用于非Owner客户端）
        /// 注意：只调整y坐标，x和z由NetworkTransform同步
        /// </summary>
        private System.Collections.IEnumerator EnsureAvatarVisibleAfterLoad()
        {
            // 等待几帧，确保avatar完全设置好
            yield return new WaitForSeconds(0.1f);
            yield return new WaitForFixedUpdate();
            
            // 检查对象是否已被销毁
            if (this == null || gameObject == null || avatar == null)
            {
                yield break;
            }
            
            LogDebug("Non-owner: Ensuring avatar visibility after load");
            
            // 确保玩家GameObject是激活的
            if (!gameObject.activeSelf)
            {
                LogWarning("Non-owner: Player GameObject became inactive, reactivating...");
                gameObject.SetActive(true);
            }
            
            // 确保avatar是激活的
            if (!avatar.activeSelf)
            {
                LogWarning("Non-owner: Avatar became inactive, reactivating...");
                SetActiveRecursively(avatar, true);
            }
            
            // 确保avatar的transform正确设置
            if (avatar.transform.parent != transform)
            {
                LogWarning("Non-owner: Avatar parent incorrect, fixing...");
                avatar.transform.parent = transform;
            }
            
            // 确保avatar的localPosition正确
            Vector3 expectedLocalPos = avatarPositionOffset;
            if (Vector3.Distance(avatar.transform.localPosition, expectedLocalPos) > 0.01f)
            {
                LogDebug($"Non-owner: Avatar localPosition incorrect ({avatar.transform.localPosition}), fixing to {expectedLocalPos}");
                avatar.transform.localPosition = expectedLocalPos;
            }
            
            // 确保avatar的localRotation正确
            if (avatar.transform.localRotation != Quaternion.identity)
            {
                LogDebug("Non-owner: Avatar localRotation incorrect, fixing...");
                avatar.transform.localRotation = Quaternion.identity;
            }
            
            // 确保avatar的localScale正确
            if (avatar.transform.localScale != Vector3.one)
            {
                LogDebug("Non-owner: Avatar localScale incorrect, fixing...");
                avatar.transform.localScale = Vector3.one;
            }
            
            // 获取nameTag引用（在协程开始时获取，确保能获取到）
            PlayerNameTag nameTag = GetComponent<PlayerNameTag>();
            
            // 只复位一次y坐标（avatar加载完成后）
            // 无论y坐标是否接近0，都要确保复位到精确的0
            if (!_hasResetYPosition)
            {
                _hasResetYPosition = true;
                
                // 先隐藏avatar和username，等待y坐标复位后再显示
                if (avatar != null)
                {
                    SetActiveRecursively(avatar, false);
                    LogDebug("Non-owner: Avatar hidden until y position is reset to 0");
                }
                
                // 隐藏username
                if (nameTag != null)
                {
                    nameTag.SetVisible(false);
                    LogDebug("Non-owner: Username hidden until y position is reset to 0");
                }
                
                // 将非Owner玩家的y坐标复位到0（avatar加载完成后，只复位一次）
                // 无论当前y坐标是多少，都复位到0
                Vector3 playerPos = transform.position;
                Vector3 fixedPos = new Vector3(playerPos.x, 0f, playerPos.z);
                
                CharacterController controller = GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                    transform.position = fixedPos;
                    controller.enabled = true;
                }
                else
                {
                    transform.position = fixedPos;
                }
                
                LogDebug($"Non-owner: Reset player y position to 0 after avatar load (ONCE): {playerPos} -> {fixedPos}");
                
                // 等待一帧，确保位置已经设置好，然后显示avatar和username
                yield return new WaitForFixedUpdate();
                
                // 再次确认y坐标是0（防止被NetworkTransform覆盖）
                Vector3 currentPos = transform.position;
                if (Mathf.Abs(currentPos.y) > 0.01f)
                {
                    LogWarning($"Non-owner: Y position was changed after reset ({currentPos.y}), resetting again...");
                    if (controller != null)
                    {
                        controller.enabled = false;
                        transform.position = new Vector3(currentPos.x, 0f, currentPos.z);
                        controller.enabled = true;
                    }
                    else
                    {
                        transform.position = new Vector3(currentPos.x, 0f, currentPos.z);
                    }
                    yield return new WaitForFixedUpdate();
                }
                
                // 显示avatar和username
                if (avatar != null)
                {
                    SetActiveRecursively(avatar, true);
                    LogDebug("Non-owner: Avatar shown after y position reset to 0");
                }
                
                if (nameTag != null)
                {
                    nameTag.SetVisible(true);
                    LogDebug("Non-owner: Username shown after y position reset to 0");
                }
            }
            else
            {
                // 如果y坐标已经复位过，但avatar刚加载完成，也要显示avatar和username
                // 同时检查y坐标是否还是0，如果不是，再次复位
                Vector3 currentPos = transform.position;
                if (Mathf.Abs(currentPos.y) > 0.01f)
                {
                    LogWarning($"Non-owner: Y position is not 0 ({currentPos.y}) even though reset flag is true, resetting again...");
                    Vector3 fixedPos = new Vector3(currentPos.x, 0f, currentPos.z);
                    
                    CharacterController controller = GetComponent<CharacterController>();
                    if (controller != null)
                    {
                        controller.enabled = false;
                        transform.position = fixedPos;
                        controller.enabled = true;
                    }
                    else
                    {
                        transform.position = fixedPos;
                    }
                    yield return new WaitForFixedUpdate();
                }
                
                LogDebug("Non-owner: Y position already reset, but ensuring avatar and username are visible after avatar load");
                
                // 确保avatar显示
                if (avatar != null)
                {
                    SetActiveRecursively(avatar, true);
                    LogDebug("Non-owner: Avatar shown (y position was already reset)");
                }
                
                // 确保username显示
                if (nameTag != null)
                {
                    nameTag.SetVisible(true);
                    LogDebug("Non-owner: Username shown (y position was already reset)");
                }
            }
            
            LogDebug($"Non-owner: Avatar visibility check complete. Player pos: {transform.position}, Avatar active: {avatar.activeSelf}, Avatar localPos: {avatar.transform.localPosition}");
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
            
            // 如果正在加载，跳过（避免重复加载）
            if (_isLoading)
            {
                LogDebug("Avatar is already loading, skipping duplicate load request");
                return;
            }
            
            _isLoading = true;
            LogDebug($"Loading avatar from URL: {url} (IsOwner: {IsOwner}, retry: {_loadRetryCount})");
            
            // 确保玩家GameObject是激活的
            if (!gameObject.activeSelf)
            {
                LogWarning("Player GameObject is inactive before loading avatar, activating...");
                gameObject.SetActive(true);
            }
            
            // 移除前后空格并加载
            string trimmedUrl = url.Trim(' ');
            avatarObjectLoader.LoadAvatar(trimmedUrl);
        }
        
        /// <summary>
        /// 定期检查并确保avatar和玩家GameObject始终激活
        /// </summary>
        private System.Collections.IEnumerator PeriodicActivationCheck()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f); // 每秒检查一次
                
                // 检查对象是否已被销毁
                if (this == null || gameObject == null)
                {
                    yield break;
                }
                
                // 确保玩家GameObject是激活的
                if (!gameObject.activeSelf)
                {
                    LogWarning("Player GameObject became inactive, reactivating...");
                    gameObject.SetActive(true);
                }
                
                // 如果avatar已加载，确保它是激活的并且transform正确
                if (avatar != null)
                {
                    // 对于非Owner客户端，检查y坐标是否已经复位
                    // 如果已经复位，确保avatar和username都显示
                    if (!IsOwner && _hasResetYPosition)
                    {
                        Vector3 playerPos = transform.position;
                        if (Mathf.Abs(playerPos.y) < 0.5f)
                        {
                            // y坐标已经复位，确保avatar和username都显示
                            if (!avatar.activeSelf)
                            {
                                SetActiveRecursively(avatar, true);
                                LogDebug("Non-owner: Avatar reactivated in periodic check (y position reset)");
                            }
                            
                            // 确保username也显示
                            PlayerNameTag nameTag = GetComponent<PlayerNameTag>();
                            if (nameTag != null)
                            {
                                nameTag.SetVisible(true);
                            }
                        }
                    }
                    else if (!avatar.activeSelf)
                    {
                        LogWarning("Avatar became inactive, reactivating...");
                        SetActiveRecursively(avatar, true);
                    }
                    
                    // 确保avatar的parent正确
                    if (avatar.transform.parent != transform)
                    {
                        LogWarning("Avatar parent incorrect, fixing...");
                        avatar.transform.parent = transform;
                        avatar.transform.localPosition = avatarPositionOffset;
                        avatar.transform.localRotation = Quaternion.identity;
                        avatar.transform.localScale = Vector3.one;
                    }
                    
                    // 确保avatar的Renderer组件是启用的
                    Renderer[] renderers = avatar.GetComponentsInChildren<Renderer>(true);
                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer != null && !renderer.enabled)
                        {
                            LogDebug($"Enabling disabled renderer in periodic check: {renderer.name}");
                            renderer.enabled = true;
                        }
                    }
                }
                
                
                // 如果avatar还没加载，且不是Owner，检查是否有URL但还没加载
                if (avatar == null && !IsOwner && !_isLoading)
                {
                    string syncedUrl = _syncedAvatarUrl.Value;
                    if (!string.IsNullOrEmpty(syncedUrl))
                    {
                        LogDebug("Non-owner: Found avatar URL but avatar not loaded, attempting to load...");
                        LoadAvatarForClient(syncedUrl);
                    }
                }
            }
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

