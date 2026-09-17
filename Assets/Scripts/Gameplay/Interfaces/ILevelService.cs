using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;
using UnityEngine;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract quản lý màn chơi.
    public interface ILevelService
    {
        int CurrentLevel { get; }

        LevelConfig CurrentConfig { get; }

        LevelGridData CurrentGrid { get; }

        IReadOnlyList<Color32> CurrentJewelColors { get; }

        IReadOnlyList<LevelConfig> Levels { get; }

        /// Màn đã mở khoá: id nhỏ hơn hoặc bằng màn đang chơi.
        bool IsUnlocked(int levelId);

        event Action<int> OnLevelLoadStarted;

        event Action<int> OnLevelStarted;

        event Action<int> OnLevelCompleted;

        /// Có LevelConfig nào mang id này không.
        bool HasLevel(int levelId);

        /// Màn đã tô xong.
        bool IsCompleted(int levelId);

        void LoadLevel(int levelId);
        void CompleteCurrentLevel();

        /// Ghi nhận một màn đã tô xong mà không nhích tiến trình.
        void MarkLevelFinished(int levelId);
    }
}
