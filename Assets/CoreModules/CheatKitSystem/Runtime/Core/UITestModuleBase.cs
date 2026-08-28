using UnityEngine;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// Base class cho UI Test Module.
    /// Implement common functionality để các module con chỉ override những gì cần thiết.
    /// 
    /// DESIGN PATTERN: Template Method
    /// - Base class định nghĩa skeleton (Initialize → OnEnable → OnUpdate → OnDisable → Cleanup)
    /// - Subclass override specific steps
    /// 
    /// USAGE:
    /// public class MyModule : UITestModuleBase
    /// {
    ///     protected override void OnInitialize() { ... }
    ///     public override void OnUpdate() { ... }
    /// }
    /// </summary>
    public abstract class UITestModuleBase : MonoBehaviour, IUITestModule
    {
        #region Serialized Fields
        [Header("Module Settings")]
        [SerializeField] protected string _moduleName = "Test Module";
        [SerializeField] protected bool _activeByDefault = true;
        [SerializeField] protected GameObject _moduleRoot;
        #endregion
        
        #region Private Fields
        protected bool _isInitialized = false;
        protected bool _isActive = false;
        #endregion
        
        #region IUITestModule Implementation
        public string ModuleName => _moduleName;
        public bool IsActive => _isActive;
        public GameObject ModuleRoot => _moduleRoot != null ? _moduleRoot : gameObject;
        
        public void Initialize()
        {
            if (_isInitialized)
            {
                LogWarning("Module already initialized, skipping.");
                return;
            }
            
            LogDebug($"Initializing module: {_moduleName}");
            
            // Auto-assign module root if not set
            if (_moduleRoot == null)
                _moduleRoot = gameObject;
            
            OnInitialize();
            
            _isInitialized = true;
            _isActive = _activeByDefault;
            
            SetVisibility(_activeByDefault);
            
            LogDebug($"Module initialized: {_moduleName}");
        }
        
        public void OnEnable()
        {
            if (!_isInitialized)
            {
                LogWarning("Attempting to enable uninitialized module, initializing first.");
                Initialize();
            }
            
            _isActive = true;
            OnModuleEnabled();
            LogDebug($"Module enabled: {_moduleName}");
        }
        
        public void OnDisable()
        {
            _isActive = false;
            OnModuleDisabled();
            LogDebug($"Module disabled: {_moduleName}");
        }
        
        public virtual void OnUpdate()
        {
            // Override in subclass nếu cần update mỗi frame
        }
        
        public void Cleanup()
        {
            LogDebug($"Cleaning up module: {_moduleName}");
            OnCleanup();
            _isInitialized = false;
            _isActive = false;
        }
        
        public void SetVisibility(bool visible)
        {
            if (ModuleRoot != null)
                ModuleRoot.SetActive(visible);
            
            LogDebug($"Module visibility set to {visible}: {_moduleName}");
        }
        #endregion
        
        #region Virtual Methods - Override in Subclass
        /// <summary>Override để implement initialization logic</summary>
        protected virtual void OnInitialize() { }
        
        /// <summary>Override để implement enable logic (subscribe events, etc.)</summary>
        protected virtual void OnModuleEnabled() { }
        
        /// <summary>Override để implement disable logic (unsubscribe events, etc.)</summary>
        protected virtual void OnModuleDisabled() { }
        
        /// <summary>Override để implement cleanup logic</summary>
        protected virtual void OnCleanup() { }
        #endregion
        
        #region Unity Lifecycle
        protected virtual void Awake()
        {
            // Subclass có thể override nếu cần
        }
        
        protected virtual void Start()
        {
            // Subclass có thể override nếu cần
        }
        
        protected virtual void OnDestroy()
        {
            if (_isInitialized)
                Cleanup();
        }
        #endregion
        
        #region Debug Helpers
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        protected void LogDebug(string message)
        {
            CheatLog.Info($"[{_moduleName}] {message}");
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        protected void LogWarning(string message)
        {
            CheatLog.Warning($"[{_moduleName}] {message}");
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        protected void LogError(string message)
        {
            CheatLog.Error($"[{_moduleName}] {message}");
        }
        #endregion
    }
}
