using System;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract do Gameplay tự định nghĩa. UI phụ thuộc interface này,
    /// Gameplay không bao giờ using ngược lên UI.
    public interface ILevelService
    {
        int CurrentLevel { get; }

        /// Cấu hình của màn đang chơi. null nếu chưa nạp màn nào.
        LevelConfig CurrentConfig { get; }

        /// Dữ liệu lưới của màn đang chơi. null nếu màn chưa nạp hoặc chưa gán GridData.
        LevelGridData CurrentGrid { get; }

        event Action<int> OnLevelStarted;
        event Action<int> OnLevelCompleted;

        /// Có LevelConfig nào mang id này không. Dùng để biết còn màn kế hay đã hết.
        bool HasLevel(int levelId);

        void LoadLevel(int levelId);
        void CompleteCurrentLevel();
    }
}
