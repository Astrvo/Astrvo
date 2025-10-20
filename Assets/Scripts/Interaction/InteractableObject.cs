using UnityEngine;
using System;

namespace InteractionSystem
{
    /// <summary>
    /// 可交互对象的基础组件
    /// 所有需要交互的GameObject都应该添加此组件
    /// </summary>
    public class InteractableObject : MonoBehaviour
    {
        [Header("交互设置")]
        [SerializeField] private string interactionName = "交互";
        [SerializeField] private string interactionDescription = "点击进行交互";
        [SerializeField] private float interactionRange = 3f;
        [SerializeField] private bool requireLineOfSight = true;
        
        [Header("视觉提示")]
        [SerializeField] private GameObject highlightObject;
        [SerializeField] private bool showHighlight = true;
        
        [Header("调试")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 事件
        public static event Action<InteractableObject> OnPlayerEnterRange;
        public static event Action<InteractableObject> OnPlayerExitRange;
        public static event Action<InteractableObject> OnInteractionTriggered;
        
        // 属性
        public string InteractionName => interactionName;
        public string InteractionDescription => interactionDescription;
        public float InteractionRange => interactionRange;
        public bool RequireLineOfSight => requireLineOfSight;
        public bool IsPlayerInRange { get; private set; }
        public bool IsHighlighted { get; private set; }
        
        // 私有变量
        private Transform playerTransform;
        private bool isInitialized = false;
        
        private void Awake()
        {
            // 确保有Collider用于检测
            if (GetComponent<Collider>() == null)
            {
                var collider = gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = interactionRange;
            }
        }
        
        private void Start()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            // 查找玩家
            FindPlayer();
            
            // 设置高亮对象
            SetupHighlight();
            
            isInitialized = true;
            LogDebug($"交互对象初始化完成: {interactionName}");
        }
        
        private void FindPlayer()
        {
            // 尝试通过标签查找玩家
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                LogDebug("找到玩家对象");
            }
            else
            {
                LogDebug("警告: 未找到玩家对象，请确保玩家有'Player'标签");
            }
        }
        
        private void SetupHighlight()
        {
            if (highlightObject == null && showHighlight)
            {
                // 创建默认高亮效果
                CreateDefaultHighlight();
            }
            
            if (highlightObject != null)
            {
                highlightObject.SetActive(false);
            }
        }
        
        private void CreateDefaultHighlight()
        {
            // 创建一个简单的发光效果
            GameObject highlight = new GameObject($"{gameObject.name}_Highlight");
            highlight.transform.SetParent(transform);
            highlight.transform.localPosition = Vector3.zero;
            
            // 添加发光材质
            var renderer = highlight.AddComponent<MeshRenderer>();
            var filter = highlight.AddComponent<MeshFilter>();
            
            // 复制当前对象的网格
            var originalFilter = GetComponent<MeshFilter>();
            if (originalFilter != null)
            {
                filter.mesh = originalFilter.mesh;
            }
            
            // 创建发光材质
            Material highlightMaterial = new Material(Shader.Find("Standard"));
            highlightMaterial.color = new Color(1f, 1f, 0f, 0.3f);
            highlightMaterial.SetFloat("_Mode", 3); // Transparent mode
            highlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            highlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            highlightMaterial.SetInt("_ZWrite", 0);
            highlightMaterial.DisableKeyword("_ALPHATEST_ON");
            highlightMaterial.EnableKeyword("_ALPHABLEND_ON");
            highlightMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            highlightMaterial.renderQueue = 3000;
            
            renderer.material = highlightMaterial;
            highlightObject = highlight;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!isInitialized) return;
            
            if (other.CompareTag("Player"))
            {
                playerTransform = other.transform;
                OnPlayerEnterRange?.Invoke(this);
                IsPlayerInRange = true;
                LogDebug($"玩家进入交互范围: {interactionName}");
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!isInitialized) return;
            
            if (other.CompareTag("Player"))
            {
                OnPlayerExitRange?.Invoke(this);
                IsPlayerInRange = false;
                SetHighlight(false);
                LogDebug($"玩家离开交互范围: {interactionName}");
            }
        }
        
        /// <summary>
        /// 触发交互
        /// </summary>
        public virtual void TriggerInteraction()
        {
            if (!IsPlayerInRange)
            {
                LogDebug("玩家不在交互范围内");
                return;
            }
            
            // 检查视线
            if (requireLineOfSight && !HasLineOfSight())
            {
                LogDebug("没有视线，无法交互");
                return;
            }
            
            OnInteractionTriggered?.Invoke(this);
            LogDebug($"触发交互: {interactionName}");
        }
        
        /// <summary>
        /// 检查是否有视线
        /// </summary>
        private bool HasLineOfSight()
        {
            if (playerTransform == null) return false;
            
            Vector3 direction = (transform.position - playerTransform.position).normalized;
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            
            RaycastHit hit;
            if (Physics.Raycast(playerTransform.position, direction, out hit, distance))
            {
                return hit.collider.gameObject == gameObject;
            }
            
            return true;
        }
        
        /// <summary>
        /// 设置高亮状态
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            if (highlightObject != null)
            {
                highlightObject.SetActive(highlight);
                IsHighlighted = highlight;
            }
        }
        
        /// <summary>
        /// 设置交互名称
        /// </summary>
        public void SetInteractionName(string name)
        {
            interactionName = name;
        }
        
        /// <summary>
        /// 设置交互描述
        /// </summary>
        public void SetInteractionDescription(string description)
        {
            interactionDescription = description;
        }
        
        /// <summary>
        /// 设置交互范围
        /// </summary>
        public void SetInteractionRange(float range)
        {
            interactionRange = range;
            
            // 更新Collider
            var collider = GetComponent<Collider>();
            if (collider is SphereCollider sphereCollider)
            {
                sphereCollider.radius = range;
            }
        }
        
        protected void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[InteractableObject] {message}");
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // 在Scene视图中显示交互范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
