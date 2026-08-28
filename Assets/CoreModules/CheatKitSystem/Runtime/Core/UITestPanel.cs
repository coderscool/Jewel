using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// UITestPanel - Main controller cho UI Test Framework.
    /// 
    /// RESPONSIBILITIES:
    /// - Manage lifecycle của tất cả modules
    /// - Toggle visibility (F1 key hoặc mobile gesture)
    /// - Coordinate giữa các modules
    /// - Singleton pattern để dễ truy cập từ bất kỳ đâu
    /// 
    /// ARCHITECTURE:
    /// - UITestPanel (1) --- manages ---> (N) IUITestModule
    /// - Loose coupling: Panel chỉ biết Interface, không biết implementation
    /// - Event-driven: Modules communicate qua UITestEvents (sẽ tạo sau)
    /// 
    /// USAGE:
    /// // Toggle panel
    /// UITestPanel.Instance.Toggle();
    /// 
    /// // Register module
    /// UITestPanel.Instance.RegisterModule(myModule);
    /// 
    /// // Check if panel visible
    /// if (UITestPanel.Instance.IsVisible) { ... }
    /// </summary>
    public class UITestPanel : MonoBehaviour
    {
        #region Singleton
        private static UITestPanel _instance;
        
        /// <summary>Singleton instance</summary>
        public static UITestPanel Instance => _instance;
        
        /// <summary>Check if instance exists (không tạo mới nếu chưa có)</summary>
        public static bool HasInstance => _instance != null;
        #endregion
        
        #region Serialized Fields
        [Header("Panel Settings")]
        [Tooltip("Root GameObject chứa toàn bộ UI Test Panel")]
        [SerializeField] private GameObject _panelRoot;
        
        [Tooltip("Phím MỞ/đóng panel (Editor/WebGL). Mặc định F1.")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;

        [Header("Reveal Gesture (mở panel)")]
        [Tooltip("Số lần chạm liên tiếp ở vùng top-center để MỞ panel.")]
        [SerializeField] private int _revealTaps = 5;

        [Tooltip("Khoảng thời gian (giây) cho phép hoàn tất chuỗi chạm.")]
        [SerializeField] private float _revealWindow = 1.5f;

        [Header("Module Container")]
        [Tooltip("Container chứa các modules")]
        [SerializeField] private Transform _moduleContainer;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = true;
        [SerializeField] private bool _startVisible = false;
        #endregion
        
        #region Private Fields
        private List<IUITestModule> _modules = new List<IUITestModule>();
        private bool _isVisible = false;
        private bool _isInitialized = false;

        // Cử chỉ mở panel (Strategy) — pluggable, mặc định ScreenTapReveal (5 tap top-center).
        private IPanelRevealGesture _reveal;
        #endregion
        
        #region Properties
        /// <summary>Panel có đang visible không</summary>
        public bool IsVisible => _isVisible;
        
        /// <summary>Số lượng modules đã đăng ký</summary>
        public int ModuleCount => _modules.Count;
        
        /// <summary>Container chứa modules</summary>
        public Transform ModuleContainer => _moduleContainer;
        #endregion
        
        #region Unity Lifecycle
        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            InitializePanel();
        }
        
        private void Update()
        {
            // Phím tắt mở/đóng (Editor/WebGL tiện debug).
            if (Input.GetKeyDown(_toggleKey))
                Toggle();

            // Cử chỉ bí mật CHỈ để MỞ (khi đang ẩn). Đóng = nút close trên panel.
            if (!_isVisible && _reveal != null && _reveal.Poll())
                Show();

            if (_isVisible)
                UpdateModules();
        }
        
        private void OnDestroy()
        {
            CleanupPanel();
        }
        #endregion
        
        #region Initialization & Cleanup
        /// <summary>
        /// Gán panel root TRƯỚC khi Initialize (dùng cho runtime builder): root nên là một CHILD
        /// container — KHÔNG phải GameObject chứa chính UITestPanel — để khi ẩn panel, Update/gesture
        /// vẫn chạy và mở lại được. Bỏ qua nếu đã initialize.
        /// </summary>
        public void SetPanelRootBeforeInit(GameObject panelRoot)
        {
            if (_isInitialized)
            {
                LogWarning("SetPanelRootBeforeInit gọi sau khi đã initialize — bỏ qua.");
                return;
            }
            _panelRoot = panelRoot;
        }

        /// <summary>Đặt trạng thái hiện/ẩn ban đầu TRƯỚC Initialize (runtime builder). Bỏ qua nếu đã init.</summary>
        public void SetStartVisibleBeforeInit(bool startVisible)
        {
            if (_isInitialized)
            {
                LogWarning("SetStartVisibleBeforeInit gọi sau khi đã initialize — bỏ qua.");
                return;
            }
            _startVisible = startVisible;
        }

        /// <summary>Initialize panel</summary>
        private void InitializePanel()
        {
            LogDebug("🎮 Initializing UITestPanel...");
            
            // Auto-detect panel root if not assigned
            if (_panelRoot == null)
                _panelRoot = gameObject;

            // Cử chỉ mở mặc định nếu chưa được set từ ngoài (builder có thể override qua SetRevealGesture).
            if (_reveal == null)
                _reveal = new ScreenTapReveal(_revealTaps, _revealWindow);

            // Auto-detect module container if not assigned
            if (_moduleContainer == null)
            {
                var containerObj = transform.Find("ModuleContainer");
                _moduleContainer = containerObj != null ? containerObj : transform;
            }
            
            // Auto-discover modules in children
            AutoDiscoverModules();
            
            // Initialize all modules
            InitializeModules();
            
            // Set initial visibility
            SetVisibility(_startVisible);
            
            _isInitialized = true;
            LogDebug($"✅ UITestPanel initialized ({_modules.Count} modules registered)");
        }
        
        /// <summary>Cleanup panel resources</summary>
        private void CleanupPanel()
        {
            if (!_isInitialized) return;
            
            LogDebug("🧹 Cleaning up UITestPanel...");
            
            // Cleanup all modules
            foreach (var module in _modules)
            {
                module?.Cleanup();
            }
            
            _modules.Clear();
            _isInitialized = false;
            
            LogDebug("✅ UITestPanel cleaned up");
        }
        #endregion
        
        #region Module Management
        /// <summary>
        /// Auto-discover modules in children
        /// Tìm tất cả components implement IUITestModule trong children
        /// </summary>
        private void AutoDiscoverModules()
        {
            var discoveredModules = GetComponentsInChildren<IUITestModule>(true);
            
            foreach (var module in discoveredModules)
            {
                RegisterModule(module);
            }
            
            LogDebug($"🔍 Auto-discovered {discoveredModules.Length} modules");
        }
        
        /// <summary>
        /// Register module to panel.
        /// PUBLIC API - có thể gọi từ bên ngoài để add module dynamically.
        /// </summary>
        /// <param name="module">Module to register</param>
        public void RegisterModule(IUITestModule module)
        {
            if (module == null)
            {
                LogWarning("Attempted to register null module");
                return;
            }
            
            if (_modules.Contains(module))
            {
                LogWarning($"Module already registered: {module.ModuleName}");
                return;
            }
            
            _modules.Add(module);
            LogDebug($"➕ Registered module: {module.ModuleName}");
        }
        
        /// <summary>
        /// Unregister module from panel
        /// </summary>
        public void UnregisterModule(IUITestModule module)
        {
            if (_modules.Remove(module))
            {
                module?.Cleanup();
                LogDebug($"➖ Unregistered module: {module.ModuleName}");
            }
        }
        
        /// <summary>Initialize all modules</summary>
        private void InitializeModules()
        {
            foreach (var module in _modules)
            {
                try
                {
                    module?.Initialize();
                }
                catch (Exception ex)
                {
                    LogError($"Failed to initialize module {module?.ModuleName}: {ex.Message}");
                }
            }
        }
        
        /// <summary>Update all active modules (gọi mỗi frame)</summary>
        private void UpdateModules()
        {
            foreach (var module in _modules)
            {
                if (module != null && module.IsActive)
                {
                    try
                    {
                        module.OnUpdate();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error updating module {module.ModuleName}: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>Get module by name</summary>
        public T GetModule<T>() where T : class, IUITestModule
        {
            foreach (var module in _modules)
            {
                if (module is T typedModule)
                    return typedModule;
            }
            return null;
        }
        #endregion
        
        #region Visibility Control
        /// <summary>
        /// Toggle panel visibility
        /// PUBLIC API - gọi từ bất kỳ đâu
        /// </summary>
        public void Toggle()
        {
            SetVisibility(!_isVisible);
        }
        
        /// <summary>Show panel</summary>
        public void Show()
        {
            SetVisibility(true);
        }
        
        /// <summary>Hide panel</summary>
        public void Hide()
        {
            SetVisibility(false);
        }
        
        /// <summary>Set visibility của panel</summary>
        public void SetVisibility(bool visible)
        {
            _isVisible = visible;
            
            if (_panelRoot != null)
                _panelRoot.SetActive(visible);
            
            // Fire event để các module khác biết
            if (visible)
            {
                _reveal?.Reset(); // mở xong → xoá chuỗi tap dở để lần sau bắt đầu sạch
                UITestEvents.RaisePanelShown();
                EnableAllModules();
            }
            else
            {
                UITestEvents.RaisePanelHidden();
                DisableAllModules();
            }
            
            // Save state to PlayerPrefs
            PlayerPrefs.SetInt("UITestPanel_Visible", visible ? 1 : 0);
            
            LogDebug($"Panel visibility: {visible}");
        }
        
        /// <summary>Enable all modules</summary>
        private void EnableAllModules()
        {
            foreach (var module in _modules)
            {
                module?.OnEnable();
            }
        }
        
        /// <summary>Disable all modules</summary>
        private void DisableAllModules()
        {
            foreach (var module in _modules)
            {
                module?.OnDisable();
            }
        }
        #endregion
        
        #region Reveal Gesture
        /// <summary>
        /// Thay chiến lược cử chỉ MỞ panel (Strategy/OCP). Gọi trước hoặc sau Initialize đều được.
        /// null = vô hiệu hoá mở-bằng-cử-chỉ (chỉ còn phím tắt / API).
        /// </summary>
        public void SetRevealGesture(IPanelRevealGesture gesture) => _reveal = gesture;
        #endregion
        
        #region Debug Helpers
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogDebug(string message)
        {
            if (_showDebugLogs)
                CheatLog.Info($"[UITestPanel] {message}");
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogWarning(string message)
        {
            if (_showDebugLogs)
                CheatLog.Warning($"[UITestPanel] {message}");
        }
        
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogError(string message)
        {
            CheatLog.Error($"[UITestPanel] {message}");
        }
        
        #if UNITY_EDITOR
        [ContextMenu("Debug: Toggle Panel")]
        private void DebugToggle()
        {
            Toggle();
        }
        
        [ContextMenu("Debug: List Modules")]
        private void DebugListModules()
        {
            CheatLog.Info($"[UITestPanel] Registered Modules ({_modules.Count}):");
            foreach (var module in _modules)
            {
                CheatLog.Info($"  - {module?.ModuleName} (Active: {module?.IsActive})");
            }
        }
        #endif
        #endregion
    }
}
