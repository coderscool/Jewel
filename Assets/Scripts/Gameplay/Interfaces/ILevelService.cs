using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;
using UnityEngine;

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

        /// Bảng màu của VIÊN NGỌC cho màn đang chơi — màu đất đã qua JewelTintConfig.
        ///
        /// Cùng số lượng và cùng thứ tự với CurrentGrid.Colors, nên một chỉ số dùng
        /// được cho cả hai bảng. Rỗng khi chưa nạp màn nào.
        ///
        /// Vì sao là một bảng dựng sẵn chứ không phải hàm tính từng màu: nơi tô ngọc là
        /// vòng lặp sinh hàng nghìn viên, còn bảng thì chỉ vài màu. Tính một lần lúc vào
        /// màn là xong, không đụng gì tới đường chạy nóng.
        IReadOnlyList<Color32> CurrentJewelColors { get; }

        /// Toàn bộ màn chơi, đúng thứ tự khai trong LevelManager. Popup bộ sưu tập
        /// duyệt danh sách này. Có thể chứa phần tử null nếu Inspector bỏ trống ô nào.
        IReadOnlyList<LevelConfig> Levels { get; }

        /// Màn đã mở khoá: id nhỏ hơn hoặc bằng màn đang chơi.
        bool IsUnlocked(int levelId);

        event Action<int> OnLevelStarted;
        event Action<int> OnLevelCompleted;

        /// Có LevelConfig nào mang id này không. Dùng để biết còn màn kế hay đã hết.
        bool HasLevel(int levelId);

        /// Màn đã TÔ XONG. Khác IsUnlocked ở đúng một màn: màn đang chơi dở đã mở khoá
        /// nhưng chưa hoàn thành.
        ///
        /// Hai câu hỏi khác nhau nên có hai hàm: "vào chơi được không" dùng IsUnlocked,
        /// "đã có trong bộ sưu tập chưa" dùng hàm này.
        bool IsCompleted(int levelId);

        void LoadLevel(int levelId);
        void CompleteCurrentLevel();
    }
}
