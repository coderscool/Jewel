using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CoreModules.CheatKit
{
    /// <summary>
    /// CheatFlags - Static flags để game logic có thể check cheats.
    /// 
    /// DESIGN PRINCIPLE:
    /// - Static Class: Dễ truy cập từ bất kỳ đâu trong game logic
    /// - Read-Only từ Game Logic: Game chỉ READ, UI Test Module SET
    /// - Boolean Flags: Simple và fast (không cần dictionary lookup)
    /// 
    /// USAGE (trong Game Logic):
    /// // Check if invincibility active
    /// if (CheatFlags.IsInvincible)
    /// {
    ///     // Don't trigger lose condition
    ///     return;
    /// }
    /// 
    /// // Check infinite moves
    /// if (!CheatFlags.HasInfiniteMoves && movesRemaining <= 0)
    /// {
    ///     TriggerLoseCondition();
    /// }
    /// 
    /// USAGE (trong UI Test Module):
    /// CheatFlags.IsInvincible = true;
    /// CheatFlags.HasInfiniteMoves = true;
    /// 
    /// INTEGRATION POINTS:
    /// - GameStateManager: Check IsInvincible trước khi trigger lose
    /// - MoveCounter: Check HasInfiniteMoves để skip decrement
    /// - UndoSystem: Check HasInfiniteUndo để skip limit
    /// - Shop: Check FreePurchases để skip coin deduction
    /// </summary>
    public static class CheatFlags
    {
        #region Gameplay Cheats
        /// <summary>Không bao giờ lose (invincible mode)</summary>
        public static bool IsInvincible { get; set; } = false;
        
        /// <summary>Không giới hạn số moves</summary>
        public static bool HasInfiniteMoves { get; set; } = false;
        
        /// <summary>Không giới hạn số undo</summary>
        public static bool HasInfiniteUndo { get; set; } = false;
        
        /// <summary>Bỏ qua time limit</summary>
        public static bool IgnoreTimeLimit { get; set; } = false;
        
        /// <summary>Auto-complete level (tự động win sau 1 khoảng thời gian)</summary>
        public static bool AutoComplete { get; set; } = false;
        #endregion
        
        #region Resource Cheats
        /// <summary>Không giới hạn coins</summary>
        public static bool HasInfiniteCoins { get; set; } = false;
        
        /// <summary>Không giới hạn hints</summary>
        public static bool HasInfiniteHints { get; set; } = false;
        
        /// <summary>Mua đồ free (không trừ coins)</summary>
        public static bool FreePurchases { get; set; } = false;
        #endregion
        
        #region Unlock Cheats
        /// <summary>Unlock tất cả levels</summary>
        public static bool AllLevelsUnlocked { get; set; } = false;
        
        /// <summary>Unlock tất cả skins/themes</summary>
        public static bool AllSkinsUnlocked { get; set; } = false;
        #endregion
        
        #region Debug/Visual Cheats
        /// <summary>Hiển thị grid overlay (debug visualization)</summary>
        public static bool ShowGridOverlay { get; set; } = false;
        
        /// <summary>Hiển thị collision bounds</summary>
        public static bool ShowCollisionBounds { get; set; } = false;
        
        /// <summary>Show FPS counter luôn (không cần mở UI Test Panel)</summary>
        public static bool ShowFPSAlways { get; set; } = false;
        
        /// <summary>Verbose logging (log tất cả game events)</summary>
        public static bool VerboseLogging { get; set; } = false;
        #endregion
        
        #region Utility Methods
        /// <summary>
        /// Reset tất cả cheats về false.
        /// Gọi khi bắt đầu level mới hoặc tắt UI Test Panel.
        /// </summary>
        public static void ResetAll()
        {
            IsInvincible = false;
            HasInfiniteMoves = false;
            HasInfiniteUndo = false;
            IgnoreTimeLimit = false;
            AutoComplete = false;
            
            HasInfiniteCoins = false;
            HasInfiniteHints = false;
            FreePurchases = false;
            
            AllLevelsUnlocked = false;
            AllSkinsUnlocked = false;
            
            ShowGridOverlay = false;
            ShowCollisionBounds = false;
            ShowFPSAlways = false;
            VerboseLogging = false;
            
            CheatLog.Info("[CheatFlags] All cheats reset");
        }
        
        /// <summary>
        /// Enable tất cả gameplay cheats (god mode).
        /// Dùng cho quick testing.
        /// </summary>
        public static void EnableAllCheats()
        {
            IsInvincible = true;
            HasInfiniteMoves = true;
            HasInfiniteUndo = true;
            IgnoreTimeLimit = true;
            
            HasInfiniteCoins = true;
            HasInfiniteHints = true;
            FreePurchases = true;
            
            AllLevelsUnlocked = true;
            AllSkinsUnlocked = true;
            
            CheatLog.Info("[CheatFlags] All cheats enabled (GOD MODE)");
        }
        
        /// <summary>
        /// Get danh sách cheats đang active (for debugging/logging).
        /// </summary>
        public static List<string> GetActiveCheats()
        {
            var activeCheats = new List<string>();
            
            if (IsInvincible) activeCheats.Add("Invincible");
            if (HasInfiniteMoves) activeCheats.Add("Infinite Moves");
            if (HasInfiniteUndo) activeCheats.Add("Infinite Undo");
            if (IgnoreTimeLimit) activeCheats.Add("No Time Limit");
            if (AutoComplete) activeCheats.Add("Auto Complete");
            
            if (HasInfiniteCoins) activeCheats.Add("Infinite Coins");
            if (HasInfiniteHints) activeCheats.Add("Infinite Hints");
            if (FreePurchases) activeCheats.Add("Free Purchases");
            
            if (AllLevelsUnlocked) activeCheats.Add("All Levels Unlocked");
            if (AllSkinsUnlocked) activeCheats.Add("All Skins Unlocked");
            
            if (ShowGridOverlay) activeCheats.Add("Grid Overlay");
            if (ShowCollisionBounds) activeCheats.Add("Collision Bounds");
            if (ShowFPSAlways) activeCheats.Add("FPS Always");
            if (VerboseLogging) activeCheats.Add("Verbose Logging");
            
            return activeCheats;
        }
        
        /// <summary>
        /// Check if ANY cheat is active.
        /// </summary>
        public static bool HasAnyCheatsActive()
        {
            return IsInvincible || HasInfiniteMoves || HasInfiniteUndo || 
                   IgnoreTimeLimit || AutoComplete ||
                   HasInfiniteCoins || HasInfiniteHints || FreePurchases ||
                   AllLevelsUnlocked || AllSkinsUnlocked ||
                   ShowGridOverlay || ShowCollisionBounds || ShowFPSAlways || VerboseLogging;
        }
        
        /// <summary>
        /// Log current cheat status (for debugging).
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void LogStatus()
        {
            var active = GetActiveCheats();
            if (active.Count > 0)
            {
                CheatLog.Info($"[CheatFlags] Active Cheats ({active.Count}): {string.Join(", ", active)}");
            }
            else
            {
                CheatLog.Info("[CheatFlags] No cheats active");
            }
        }
        #endregion
    }
}
