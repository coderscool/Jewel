using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

using CoreModules.CheatKit;

namespace CoreModules.CheatKit.Modules
{
    /// <summary>
    /// PerformanceStatsModule - Hiển thị performance metrics real-time.
    /// 
    /// FEATURES:
    /// - FPS counter (average + min + max)
    /// - Memory usage (allocated, reserved, GC)
    /// - Draw calls, triangles, vertices (Editor only)
    /// - Batch count
    /// - VSync toggle
    /// - Force GC button
    /// 
    /// USAGE:
    /// Gắn vào GameObject child của UITestPanel.
    /// Module tự động update mỗi frame khi active.
    /// </summary>
    public class PerformanceStatsModule : UITestModuleBase, ICheatModuleUi
    {
        #region Serialized Fields
        [Header("Performance Stats UI")]
        [Tooltip("Text hiển thị FPS")]
        [SerializeField] private Text _fpsText;
        
        [Tooltip("Text hiển thị memory usage")]
        [SerializeField] private Text _memoryText;
        
        [Tooltip("Text hiển thị rendering stats")]
        [SerializeField] private Text _renderingText;
        
        [Tooltip("Text hiển thị GC stats")]
        [SerializeField] private Text _gcText;
        
        [Header("Controls")]
        [Tooltip("Toggle VSync")]
        [SerializeField] private Toggle _vsyncToggle;
        
        [Tooltip("Button force GC")]
        [SerializeField] private Button _forceGCButton;
        
        [Tooltip("Toggle để hiển thị/ẩn module")]
        [SerializeField] private Toggle _showStatsToggle;
        
        [Header("Settings")]
        [Tooltip("Update frequency (seconds)")]
        [SerializeField] private float _updateInterval = 0.5f;
        
        [Tooltip("Number of samples for FPS averaging")]
        [SerializeField] private int _fpsSampleCount = 30;
        
        [Tooltip("Show detailed memory breakdown")]
        [SerializeField] private bool _showDetailedMemory = false;
        
        [Tooltip("Show rendering stats (Editor only)")]
        [SerializeField] private bool _showRenderingStats = true;
        #endregion
        
        #region Private Fields
        // FPS tracking
        private float[] _fpsSamples;
        private int _fpsSampleIndex = 0;
        private float _fpsSum = 0f;
        private float _minFps = float.MaxValue;
        private float _maxFps = 0f;
        
        // Update timing
        private float _timeSinceLastUpdate = 0f;
        
        // Memory tracking
        private long _lastAllocatedMemory = 0;
        private long _memoryDelta = 0;
        
        // GC tracking
        private int _lastGCCount = 0;
        private int _gcCountThisSession = 0;
        #endregion
        
        #region Module Lifecycle
        /// <summary>No-prefab path: dựng control + gán field nội bộ (Builder gọi trước Initialize).</summary>
        public void BuildDefaultUI(RectTransform section)
        {
            CheatUi.Label(section, "▼ PERFORMANCE", 26);
            _fpsText = CheatUi.Label(section, "FPS: —", 24, 64f);
            _memoryText = CheatUi.Label(section, "Memory: —", 22, 36f);

            var row = CheatUi.Row(section);
            _forceGCButton = CheatUi.Button(row, "Force GC", new Color(0.50f, 0.50f, 0.60f));
            _vsyncToggle = CheatUi.Toggle(section, "VSync");

            // Tắt các text chi tiết không dựng ở chế độ no-prefab (đã guard null trong code update).
            _showRenderingStats = false;
        }

        protected override void OnInitialize()
        {
            _moduleName = "Performance Stats";
            
            // Initialize FPS sample array
            _fpsSamples = new float[_fpsSampleCount];
            for (int i = 0; i < _fpsSampleCount; i++)
                _fpsSamples[i] = 60f; // Default value
            
            _fpsSum = 60f * _fpsSampleCount;
            
            // Get initial memory
            _lastAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            _lastGCCount = System.GC.CollectionCount(0);
            
            // Setup UI
            SetupUIListeners();
            
            // Set initial VSync state
            if (_vsyncToggle != null)
            {
                _vsyncToggle.isOn = QualitySettings.vSyncCount > 0;
            }
            
            LogDebug("Initialized");
        }
        
        protected override void OnModuleEnabled()
        {
            // Reset stats when enabled
            ResetStats();
        }
        
        protected override void OnModuleDisabled()
        {
            // Nothing to do
        }
        
        protected override void OnCleanup()
        {
            RemoveUIListeners();
        }
        
        public override void OnUpdate()
        {
            if (!IsActive) return;
            
            // Update FPS every frame
            UpdateFPS();
            
            // Update other stats based on interval
            _timeSinceLastUpdate += Time.unscaledDeltaTime;
            if (_timeSinceLastUpdate >= _updateInterval)
            {
                UpdateMemory();
                UpdateRendering();
                UpdateGC();
                UpdateDisplay();
                
                _timeSinceLastUpdate = 0f;
            }
        }
        #endregion
        
        #region UI Setup
        private void SetupUIListeners()
        {
            if (_vsyncToggle != null)
                _vsyncToggle.onValueChanged.AddListener(OnVSyncToggled);
            
            if (_forceGCButton != null)
                _forceGCButton.onClick.AddListener(OnForceGCClick);
            
            if (_showStatsToggle != null)
                _showStatsToggle.onValueChanged.AddListener(OnShowStatsToggled);
        }
        
        private void RemoveUIListeners()
        {
            if (_vsyncToggle != null)
                _vsyncToggle.onValueChanged.RemoveListener(OnVSyncToggled);
            
            if (_forceGCButton != null)
                _forceGCButton.onClick.RemoveListener(OnForceGCClick);
            
            if (_showStatsToggle != null)
                _showStatsToggle.onValueChanged.RemoveListener(OnShowStatsToggled);
        }
        #endregion
        
        #region UI Callbacks
        private void OnVSyncToggled(bool isOn)
        {
            QualitySettings.vSyncCount = isOn ? 1 : 0;
            LogDebug($"VSync: {(isOn ? "ON" : "OFF")}");
        }
        
        private void OnForceGCClick()
        {
            LogDebug("Forcing GC...");
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
            LogDebug("GC completed");
        }
        
        private void OnShowStatsToggled(bool isOn)
        {
            SetVisibility(isOn);
        }
        #endregion
        
        #region Performance Tracking
        /// <summary>Update FPS - gọi mỗi frame</summary>
        private void UpdateFPS()
        {
            float currentFPS = 1f / Time.unscaledDeltaTime;
            
            // Remove old sample from sum
            _fpsSum -= _fpsSamples[_fpsSampleIndex];
            
            // Add new sample
            _fpsSamples[_fpsSampleIndex] = currentFPS;
            _fpsSum += currentFPS;
            
            // Move to next sample
            _fpsSampleIndex = (_fpsSampleIndex + 1) % _fpsSampleCount;
            
            // Track min/max
            if (currentFPS < _minFps) _minFps = currentFPS;
            if (currentFPS > _maxFps) _maxFps = currentFPS;
        }
        
        /// <summary>Update memory stats</summary>
        private void UpdateMemory()
        {
            long currentMemory = Profiler.GetTotalAllocatedMemoryLong();
            _memoryDelta = currentMemory - _lastAllocatedMemory;
            _lastAllocatedMemory = currentMemory;
        }
        
        /// <summary>Update rendering stats</summary>
        private void UpdateRendering()
        {
            // Rendering stats only available in Editor via UnityStats
            // In build, sẽ không hiển thị hoặc hiển thị placeholder
        }
        
        /// <summary>Update GC stats</summary>
        private void UpdateGC()
        {
            int currentGCCount = System.GC.CollectionCount(0);
            if (currentGCCount > _lastGCCount)
            {
                _gcCountThisSession += (currentGCCount - _lastGCCount);
                _lastGCCount = currentGCCount;
            }
        }
        #endregion
        
        #region Display Update
        /// <summary>Update all display texts</summary>
        private void UpdateDisplay()
        {
            UpdateFPSDisplay();
            UpdateMemoryDisplay();
            UpdateRenderingDisplay();
            UpdateGCDisplay();
        }
        
        /// <summary>Update FPS display</summary>
        private void UpdateFPSDisplay()
        {
            if (_fpsText == null) return;
            
            float avgFPS = _fpsSum / _fpsSampleCount;
            
            _fpsText.text = $"FPS: {avgFPS:F1}\n" +
                           $"Min: {_minFps:F1} | Max: {_maxFps:F1}\n" +
                           $"Frame Time: {(1000f / avgFPS):F2}ms";
            
            // Color code based on FPS
            if (avgFPS >= 55f)
                _fpsText.color = Color.green;
            else if (avgFPS >= 30f)
                _fpsText.color = Color.yellow;
            else
                _fpsText.color = Color.red;
        }
        
        /// <summary>Update memory display</summary>
        private void UpdateMemoryDisplay()
        {
            if (_memoryText == null) return;
            
            long allocatedMB = _lastAllocatedMemory / (1024 * 1024);
            long reservedMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
            long unusedMB = Profiler.GetTotalUnusedReservedMemoryLong() / (1024 * 1024);
            long monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
            
            string deltaSign = _memoryDelta >= 0 ? "+" : "";
            float deltaMB = _memoryDelta / (1024f * 1024f);
            
            if (_showDetailedMemory)
            {
                _memoryText.text = $"Memory:\n" +
                                  $"Allocated: {allocatedMB} MB ({deltaSign}{deltaMB:F2})\n" +
                                  $"Reserved: {reservedMB} MB\n" +
                                  $"Unused: {unusedMB} MB\n" +
                                  $"Mono: {monoUsedMB} MB";
            }
            else
            {
                _memoryText.text = $"Memory: {allocatedMB} MB ({deltaSign}{deltaMB:F2})";
            }
        }
        
        /// <summary>Update rendering display</summary>
        private void UpdateRenderingDisplay()
        {
            if (_renderingText == null || !_showRenderingStats) return;
            
            #if UNITY_EDITOR
            // UnityStats only available in Editor
            int drawCalls = UnityEditor.UnityStats.drawCalls;
            int batches = UnityEditor.UnityStats.batches;
            int triangles = UnityEditor.UnityStats.triangles;
            int vertices = UnityEditor.UnityStats.vertices;
            
            _renderingText.text = $"Rendering:\n" +
                                 $"Draw Calls: {drawCalls}\n" +
                                 $"Batches: {batches}\n" +
                                 $"Triangles: {triangles / 1000}K\n" +
                                 $"Vertices: {vertices / 1000}K";
            #else
            _renderingText.text = "Rendering stats\n(Editor only)";
            #endif
        }
        
        /// <summary>Update GC display</summary>
        private void UpdateGCDisplay()
        {
            if (_gcText == null) return;
            
            _gcText.text = $"GC Collections:\n" +
                          $"Gen0: {System.GC.CollectionCount(0)}\n" +
                          $"Gen1: {System.GC.CollectionCount(1)}\n" +
                          $"Gen2: {System.GC.CollectionCount(2)}\n" +
                          $"Session: {_gcCountThisSession}";
        }
        #endregion
        
        #region Helper Methods
        /// <summary>Reset all stats</summary>
        private void ResetStats()
        {
            _minFps = float.MaxValue;
            _maxFps = 0f;
            _gcCountThisSession = 0;
            _lastGCCount = System.GC.CollectionCount(0);
            
            LogDebug("Stats reset");
        }
        #endregion
        
        #region Public API
        /// <summary>PUBLIC API: Reset stats programmatically</summary>
        public void ResetStatistics()
        {
            ResetStats();
        }
        
        /// <summary>PUBLIC API: Get current FPS</summary>
        public float GetCurrentFPS()
        {
            return _fpsSum / _fpsSampleCount;
        }
        
        /// <summary>PUBLIC API: Get allocated memory in MB</summary>
        public long GetAllocatedMemoryMB()
        {
            return _lastAllocatedMemory / (1024 * 1024);
        }
        #endregion
    }
}
