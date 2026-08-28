using System;
using UnityEngine;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// UITestEvents - Event bus cho UI Test Framework.
    /// 
    /// DESIGN PRINCIPLE:
    /// - Loose Coupling: Modules không biết về nhau, chỉ fire/listen events
    /// - Observer Pattern: Một event có thể có nhiều listeners
    /// - Static Class: Dễ truy cập từ bất kỳ đâu
    /// 
    /// USAGE:
    /// // Subscribe
    /// UITestEvents.OnLevelChanged += HandleLevelChanged;
    /// 
    /// // Fire
    /// UITestEvents.RaiseLevelChanged(5);
    /// 
    /// // Unsubscribe
    /// UITestEvents.OnLevelChanged -= HandleLevelChanged;
    /// 
    /// IMPORTANT:
    /// - LUÔN unsubscribe trong OnDisable/OnDestroy để tránh memory leak
    /// - Sử dụng null-conditional operator khi raise: OnLevelChanged?.Invoke(index);
    /// </summary>
    public static class UITestEvents
    {
        #region Panel Events
        /// <summary>Fired khi UI Test Panel được hiển thị</summary>
        public static event Action OnPanelShown;
        
        /// <summary>Fired khi UI Test Panel bị ẩn</summary>
        public static event Action OnPanelHidden;
        
        public static void RaisePanelShown() => OnPanelShown?.Invoke();
        public static void RaisePanelHidden() => OnPanelHidden?.Invoke();
        #endregion
        
        #region Level Events
        /// <summary>Fired khi level được chọn/thay đổi</summary>
        public static event Action<int> OnLevelChanged;
        
        public static void RaiseLevelChanged(int levelIndex) => OnLevelChanged?.Invoke(levelIndex);
        #endregion
        
        #region Game Control Events
        /// <summary>Fired khi force win được trigger</summary>
        public static event Action OnForceWinTriggered;
        
        /// <summary>Fired khi force lose được trigger</summary>
        public static event Action OnForceLoseTriggered;
        
        /// <summary>Fired khi time scale thay đổi</summary>
        public static event Action<float> OnTimeScaleChanged;
        
        public static void RaiseForceWinTriggered() => OnForceWinTriggered?.Invoke();
        public static void RaiseForceLoseTriggered() => OnForceLoseTriggered?.Invoke();
        public static void RaiseTimeScaleChanged(float timeScale) => OnTimeScaleChanged?.Invoke(timeScale);
        #endregion
        
        #region Cheat Events
        /// <summary>
        /// Fired khi cheat được toggle.
        /// cheatId: identifier của cheat (ví dụ: "invincibility", "infinite_moves")
        /// isActive: cheat có active không
        /// </summary>
        public static event Action<string, bool> OnCheatToggled;
        
        public static void RaiseCheatToggled(string cheatId, bool isActive) 
            => OnCheatToggled?.Invoke(cheatId, isActive);
        #endregion
        
        #region Performance Events
        /// <summary>Fired khi performance stats được reset</summary>
        public static event Action OnPerformanceStatsReset;
        
        public static void RaisePerformanceStatsReset() => OnPerformanceStatsReset?.Invoke();
        #endregion
        
        #region Utility Methods
        /// <summary>
        /// Clear tất cả events - gọi khi cleanup/unload scene.
        /// SAFETY: Tránh dangling references và memory leaks.
        /// </summary>
        public static void ClearAllEvents()
        {
            OnPanelShown = null;
            OnPanelHidden = null;
            OnLevelChanged = null;
            OnForceWinTriggered = null;
            OnForceLoseTriggered = null;
            OnTimeScaleChanged = null;
            OnCheatToggled = null;
            OnPerformanceStatsReset = null;
            
            CheatLog.Info("[UITestEvents] All events cleared");
        }
        #endregion
    }
}
